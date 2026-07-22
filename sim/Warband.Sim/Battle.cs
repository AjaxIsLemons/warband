using System;
using System.Collections.Generic;
using System.Linq;

namespace Warband.Sim
{
    public enum Winner { Team0, Team1, Draw }

    public sealed class BattleResult
    {
        public Winner Winner;
        public int EndTick;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public ulong FinalHash;
        public List<PlaybackUnit> InitialUnits = new List<PlaybackUnit>();
        public List<ulong> TickViewHashes = new List<ulong>();  // guardrail: fold must match, every tick
    }

    /// <summary>
    /// The deterministic engine (ADR 0004). Two nested time layers:
    /// - Tick layer: frozen-read decide → apply. Base attacks/moves/casts are decided
    ///   from start-of-phase state and applied together — mirror fairness is structural.
    /// - Cascade layer: mutations emit events onto a FIFO queue; Drain matches triggers
    ///   by ascending unit id then declaration order, applies effects immediately,
    ///   children join the tail. Bounds: depth ≤ MaxCascadeDepth, drain ≤ MaxEventsPerDrain.
    /// - Deaths are batched (death phase) after the queue settles — no mid-cascade removal.
    /// Determinism law: integer math only, iteration by id, explicit tie-breaks, no
    /// wall-clock/float/unordered iteration anywhere in this file.
    /// </summary>
    public sealed class Battle
    {
        public const int FP = 1000;
        public const int ManaPerAttack = 10;
        public const int ManaPerHitTaken = 5;
        public const int PulseInterval = 10;       // 1s: regen/DoT/trickle cadence
        public const int ManaTrickle = 1;
        public const int OvertimeStartTick = 900;  // ~90s at 10 ticks/s
        public const int StormRampInterval = 30;
        public const int SafetyCapTick = 20_000;
        public const int MaxCascadeDepth = 8;
        public const int MaxEventsPerDrain = 50_000;

        public const int MaxFields = 32;

        private readonly List<UnitState> _units;
        private readonly List<(int Team, Trigger T)> _teamTriggers;
        private readonly List<Field> _fields = new List<Field>();
        private readonly List<BattleEvent> _log = new List<BattleEvent>();
        private readonly Queue<BattleEvent> _queue = new Queue<BattleEvent>();
        private int _tick;
        private int _nextFieldId = 1000;   // separate id space from units

        private readonly List<PlaybackUnit> _initialView;
        private readonly List<ulong> _tickViewHashes = new List<ulong>();

        public const int BoardRows = 8;
        public const int BoardCols = 6;

        private readonly List<(FieldDef Def, Hex Center, int OwnerTeam)> _initialFields;
        private readonly Rng _rng;

        public Battle(IEnumerable<UnitState> units,
                      IEnumerable<(int Team, Trigger T)>? teamTriggers = null,
                      IEnumerable<(FieldDef Def, Hex Center, int OwnerTeam)>? initialFields = null,
                      ulong seed = 1)
        {
            _units = units.OrderBy(u => u.Id).ToList();
            _teamTriggers = teamTriggers?.ToList() ?? new List<(int, Trigger)>();
            _initialFields = initialFields?.ToList() ?? new List<(FieldDef, Hex, int)>();
            _rng = new Rng(seed);
            _initialView = _units.Select(ViewOf).ToList();
        }

        public static bool InBounds(Hex h) =>
            h.Row >= 0 && h.Row < BoardRows && h.Col >= 0 && h.Col < BoardCols;

        private void AddField(FieldDef def, Hex center, int ownerId, int ownerTeam)
        {
            if (_fields.Count >= MaxFields) return; // deterministic cap
            bool attached = def.AttachToOwner && ownerId >= 0;
            var field = new Field
            {
                Id = _nextFieldId++, OwnerId = ownerId, OwnerTeam = ownerTeam,
                AttachedUnitId = attached ? ownerId : -1,
                TicksLeft = def.Ticks, Def = def,
            };
            if (!attached)
                field.StaticHexes = Hex.Range(center, def.Radius);
            _fields.Add(field);
            Emit(new BattleEvent
            {
                Kind = EventKind.FieldCreated, Source = ownerId, Target = field.Id,
                Amount = def.IsWall ? 1 : 0, Aux = field.AttachedUnitId, Aux2 = def.Radius,
            });
            foreach (var h in field.StaticHexes)
                Emit(new BattleEvent { Kind = EventKind.FieldHex, Target = field.Id, Amount = h.Q, Aux = h.R });
        }

        /// <summary>Current footprint: static fields keep their hexes; auras derive from
        /// the anchor's live position (same Hex.Range the fold uses — shared geometry).</summary>
        private List<Hex> HexesOf(Field f)
        {
            if (f.AttachedUnitId < 0) return f.StaticHexes;
            var owner = Raw(f.AttachedUnitId);
            return owner != null && owner.Alive ? Hex.Range(owner.Pos, f.Def.Radius) : new List<Hex>();
        }

        private void RemoveField(Field f)
        {
            _fields.Remove(f);
            foreach (var u in _units)
                for (int i = u.Statuses.Count - 1; i >= 0; i--)
                    if (u.Statuses[i].SourceId == f.Id)
                    {
                        var s = u.Statuses[i];
                        u.Statuses.RemoveAt(i);
                        Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)s.Kind });
                    }
            Emit(new BattleEvent { Kind = EventKind.FieldExpired, Target = f.Id });
        }

        private static PlaybackUnit ViewOf(UnitState u)
        {
            var view = new PlaybackUnit
            {
                Id = u.Id, Team = u.Team, Name = u.Def.Name, MaxHp = u.Def.MaxHp,
                Hp = u.Hp, Shield = u.Shield, Mana = u.Mana, ManaMax = u.Def.ManaMax,
                Pos = u.Pos, Dead = u.Dead,
            };
            foreach (var s in u.Statuses)
                view.Statuses.Add((s.Kind, s.Mag));
            return view;
        }

        public BattleResult Run()
        {
            foreach (var (def, center, team) in _initialFields)
                AddField(def, center, ownerId: -1, ownerTeam: team);
            Emit(new BattleEvent { Kind = EventKind.BattleStart });
            Drain();
            DeathPhase();

            while (true)
            {
                Winner? done = CheckEnd();
                if (done != null || _tick >= SafetyCapTick)
                {
                    var result = new BattleResult
                    {
                        Winner = done ?? Winner.Draw,
                        EndTick = _tick,
                        Events = _log,
                        FinalHash = StateHash(),
                        InitialUnits = _initialView,
                        TickViewHashes = _tickViewHashes,
                    };
                    _log.Add(new BattleEvent { Tick = _tick, Kind = EventKind.End, Amount = (int)result.Winner });
                    return result;
                }
                Step();
            }
        }

        private void Step()
        {
            EnginePhase();
            Drain();
            DeathPhase();

            // ---- decide (frozen state after engine phase) ----
            var attacks = new List<(UnitState u, UnitState target)>();
            var casts = new List<UnitState>();
            var moves = new List<(UnitState u, Hex dest)>();
            var occupied = new HashSet<Hex>(_units.Where(x => x.Alive).Select(x => x.Pos));
            foreach (var f in _fields)
                if (f.Def.IsWall)
                    occupied.UnionWith(HexesOf(f));

            AcquireTargets();
            foreach (var u in _units)
            {
                if (!u.Alive || u.Has(StatusKind.Stun)) continue;
                var target = ById(u.TargetId);
                if (target == null) continue;

                if (u.Def.ManaMax > 0 && u.Mana >= u.Def.ManaMax && !u.Has(StatusKind.Silence))
                    casts.Add(u);

                int dist = Hex.Distance(u.Pos, target.Pos);
                if (dist <= u.Def.Range)
                {
                    if (_tick >= u.NextAttackTick && !u.Has(StatusKind.Disarm))
                        attacks.Add((u, target));
                }
                else if (_tick >= u.NextMoveTick && !u.Has(StatusKind.Root))
                {
                    // Greedy step: strictly closer, unoccupied (origins stay blocked this
                    // tick — no same-tick train-following), fixed direction order ties.
                    Hex? best = null;
                    int bestDist = dist;
                    for (int d = 0; d < 6; d++)
                    {
                        Hex n = u.Pos.Neighbor(d);
                        if (!InBounds(n) || occupied.Contains(n)) continue;
                        int nd = Hex.Distance(n, target.Pos);
                        if (nd < bestDist) { bestDist = nd; best = n; }
                    }
                    if (best != null)
                    {
                        occupied.Add(best.Value);
                        moves.Add((u, best.Value));
                        u.NextMoveTick = _tick + u.Def.MoveInterval;
                    }
                }
            }

            // ---- apply ----
            foreach (var (u, dest) in moves)
            {
                u.Pos = dest;
                Emit(new BattleEvent { Kind = EventKind.Move, Source = u.Id, Amount = dest.Q, Aux = dest.R });
            }
            foreach (var (u, target) in attacks)
            {
                u.NextAttackTick = _tick + u.EffAttackInterval(RuleBonus(u, StatKind.AttackSpeed));

                // Projectile rule: any attack over ≥2 hexes traces the hex line; fields
                // along the interior of the path may block or modify it (render-contract:
                // resolution stays instant, the PATH is the gameplay).
                int bonus = 0;
                List<EffectDef>? riders = null;
                Hex? blockedAt = null;
                if (Hex.Distance(u.Pos, target.Pos) >= 2)
                {
                    var path = Hex.Line(u.Pos, target.Pos);
                    var footprints = _fields.Select(f => (f, Hexes: HexesOf(f))).ToList();
                    var crossed = new HashSet<int>(); // each field acts once, not per hex
                    for (int i = 1; i < path.Count - 1 && blockedAt == null; i++)
                        foreach (var (f, hexes) in footprints)
                        {
                            if (!hexes.Contains(path[i]) || !crossed.Add(f.Id)) continue;
                            if (f.Def.IsWall) { blockedAt = path[i]; break; }
                            if (Field.TeamMatches(f.Def.ProjectileAffects, f.OwnerTeam, u.Team))
                            {
                                bonus += f.Def.ProjectileBonus;
                                if (f.Def.ProjectileRiders.Count > 0)
                                    (riders ??= new List<EffectDef>()).AddRange(f.Def.ProjectileRiders);
                            }
                        }
                }

                if (blockedAt != null)
                {
                    Emit(new BattleEvent
                    {
                        Kind = EventKind.AttackBlocked, Source = u.Id, Target = target.Id,
                        Amount = blockedAt.Value.Q, Aux = blockedAt.Value.R,
                    });
                    continue; // shot wasted: no damage, no attack mana
                }

                Emit(new BattleEvent { Kind = EventKind.Attack, Source = u.Id, Target = target.Id, Cause = Cause.Attack });
                int damage = u.EffAttack(RuleBonus(u, StatKind.AttackFlat)) + bonus;
                bool crit = u.Def.CritChance > 0 && _rng.Next(100u) < (uint)u.Def.CritChance;
                if (crit) damage = damage * u.Def.CritMultFp / FP;
                DealDamage(u.Id, target, damage, Cause.Attack, 0, u.Id, crit);
                if (riders != null)
                    foreach (var eff in riders)
                        ApplyToTarget(u.Id, target, eff, Cause.Field, 0, u.Id);
                GainMana(u, ManaPerAttack);
            }
            foreach (var u in casts)
            {
                u.Mana = 0;
                Emit(new BattleEvent { Kind = EventKind.Cast, Source = u.Id, Cause = Cause.Ability, PostMana = 0 });
                var ctx = new BattleEvent { Kind = EventKind.Cast, Source = u.Id, Target = u.TargetId };
                foreach (var eff in u.Def.Signature)
                    ApplyEffectDef(u, eff, ctx, Cause.Ability, 0, u.Id);
            }

            Drain();
            DeathPhase();
            _tickViewHashes.Add(PlaybackState.HashView(_units.Select(ViewOf).ToList(), FieldViews()));
            _tick++;
        }

        private List<PlaybackField> FieldViews()
        {
            var views = new List<PlaybackField>();
            foreach (var f in _fields)
                views.Add(new PlaybackField
                {
                    Id = f.Id, IsWall = f.Def.IsWall,
                    AttachedTo = f.AttachedUnitId, Radius = f.Def.Radius,
                    Hexes = f.StaticHexes,
                });
            return views;
        }

        private void EnginePhase()
        {
            bool pulse = _tick > 0 && _tick % PulseInterval == 0;
            foreach (var u in _units)
            {
                if (!u.Alive) continue;

                for (int i = u.Statuses.Count - 1; i >= 0; i--)
                {
                    var s = u.Statuses[i];
                    if (s.TicksLeft > 0 && --s.TicksLeft == 0)
                    {
                        u.Statuses.RemoveAt(i);
                        Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)s.Kind });
                    }
                }

                if (pulse)
                {
                    foreach (var s in u.Statuses.ToArray())
                    {
                        if (s.Kind == StatusKind.Regen) Heal(s.SourceId, u, s.Mag, 0, s.SourceId);
                        else if (s.Kind == StatusKind.Dot) DealDamage(s.SourceId, u, s.Mag, Cause.Dot, 0, s.SourceId);
                    }
                    GainMana(u, ManaTrickle);
                }

                if (_tick >= OvertimeStartTick)
                    DealDamage(-1, u, 1 + (_tick - OvertimeStartTick) / StormRampInterval, Cause.Storm, 0, -1);
            }

            // Presence sweep: units inside a field carry its presence statuses, tagged
            // with the FIELD's id as SourceId; entering grants, leaving strips. This is
            // the continuous aura mechanic (ADR 0004: auras ARE fields).
            foreach (var f in _fields)
            {
                if (f.Def.Presence.Count == 0) continue;
                var hexes = HexesOf(f);
                foreach (var u in _units)
                {
                    if (!u.Alive) continue;
                    bool inside = hexes.Contains(u.Pos) && Field.TeamMatches(f.Def.PresenceAffects, f.OwnerTeam, u.Team);
                    bool has = false;
                    foreach (var s in u.Statuses)
                        if (s.SourceId == f.Id) { has = true; break; }
                    if (inside && !has)
                        foreach (var (kind, mag) in f.Def.Presence)
                        {
                            u.Statuses.Add(new Status { Kind = kind, Mag = mag, TicksLeft = -1, SourceId = f.Id });
                            Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = f.OwnerId, Target = u.Id, Amount = mag, Aux = (int)kind });
                        }
                    else if (!inside && has)
                        for (int i = u.Statuses.Count - 1; i >= 0; i--)
                            if (u.Statuses[i].SourceId == f.Id)
                            {
                                var s = u.Statuses[i];
                                u.Statuses.RemoveAt(i);
                                Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)s.Kind });
                            }
                }
            }

            // Field pulses (creation order = id order), then lifetime bookkeeping.
            if (pulse)
                foreach (var f in _fields)
                    if (f.Def.Pulse.Count > 0)
                    {
                        var hexes = HexesOf(f);
                        foreach (var u in _units)
                            if (u.Alive && hexes.Contains(u.Pos) && Field.TeamMatches(f.Def.PulseAffects, f.OwnerTeam, u.Team))
                                foreach (var eff in f.Def.Pulse)
                                    ApplyToTarget(f.OwnerId, u, eff, Cause.Field, 0, f.OwnerId);
                    }

            for (int i = _fields.Count - 1; i >= 0; i--)
            {
                var f = _fields[i];
                bool anchorGone = f.AttachedUnitId >= 0 && ById(f.AttachedUnitId) == null;
                if (anchorGone || (f.TicksLeft > 0 && --f.TicksLeft == 0))
                    RemoveField(f);
            }
        }

        // ---- the cascade layer ----

        private void Emit(BattleEvent ev)
        {
            ev.Tick = _tick;
            _queue.Enqueue(ev);
        }

        private void Drain()
        {
            int budget = MaxEventsPerDrain;
            while (_queue.Count > 0)
            {
                var ev = _queue.Dequeue();
                _log.Add(ev);
                if (budget-- <= 0 || ev.Depth >= MaxCascadeDepth) continue; // logged, no further triggers

                foreach (var owner in _units)
                {
                    if (!owner.Alive) continue;
                    foreach (var trig in owner.Def.Triggers)
                        FireIfMatch(owner, trig, ev);
                }
                for (int team = 0; team <= 1; team++)
                    foreach (var (t, trig) in _teamTriggers)
                        if (t == team)
                            foreach (var owner in _units)
                                if (owner.Alive && owner.Team == team)
                                { FireIfMatch(owner, trig, ev); break; } // once per team, lowest-id rep
            }
        }

        private void FireIfMatch(UnitState owner, Trigger trig, BattleEvent ev)
        {
            if (trig.On != ev.Kind || !CondsOk(owner, trig.When, ev)) return;
            foreach (var eff in trig.Do)
                ApplyEffectDef(owner, eff, ev, Cause.Trigger, ev.Depth + 1, owner.Id);
        }

        private static readonly BattleEvent NullEvent = new BattleEvent { Source = -1, Target = -1 };

        /// <summary>Sum of a unit's StatRules whose conditions hold right now.</summary>
        private int RuleBonus(UnitState u, StatKind stat)
        {
            int total = 0;
            foreach (var rule in u.Def.StatRules)
                if (rule.Stat == stat && CondsOk(u, rule.When, NullEvent))
                    total += rule.Amount;
            return total;
        }

        private bool CondsOk(UnitState owner, List<Cond> when, BattleEvent ev)
        {
            foreach (var c in when)
            {
                bool ok;
                switch (c.Kind)
                {
                    case CondKind.SourceIsOwner: ok = ev.Source == owner.Id; break;
                    case CondKind.TargetIsOwner: ok = ev.Target == owner.Id; break;
                    case CondKind.SourceIsEnemyOfOwner:
                        var src = Raw(ev.Source);
                        ok = src != null && src.Team != owner.Team; break;
                    case CondKind.TargetIsAllyOfOwner:
                        var ally = Raw(ev.Target);
                        ok = ally != null && ally.Team == owner.Team && ally.Id != owner.Id; break;
                    case CondKind.SourceWithinHexesOfOwner:
                        var srcNear = ById(ev.Source);
                        ok = srcNear != null && Hex.Distance(owner.Pos, srcNear.Pos) <= c.Amount; break;
                    case CondKind.CauseIs: ok = ev.Cause == c.Cause; break;
                    case CondKind.OwnerBelowHpPct:
                        ok = owner.Hp * 100 < c.Amount * owner.Def.MaxHp; break;
                    case CondKind.TargetWithinHexesOfOwner:
                        var tgt = ById(ev.Target);
                        ok = tgt != null && Hex.Distance(owner.Pos, tgt.Pos) <= c.Amount; break;
                    case CondKind.IsCrit: ok = ev.Crit; break;
                    default: ok = false; break;
                }
                if (ok == c.Not) return false;
            }
            return true;
        }

        private void ApplyEffectDef(UnitState owner, EffectDef eff, BattleEvent ctx, Cause cause, int depth, int root)
        {
            foreach (var target in ResolveSelector(owner, eff.Select, ctx))
            {
                if (eff.Kind == EffectKind.CreateField)
                {
                    if (eff.Field != null)
                        AddField(eff.Field, target.Pos, owner.Id, owner.Team);
                }
                else if (eff.Kind == EffectKind.Leap)
                {
                    LeapTo(owner, target);
                }
                else
                {
                    ApplyToTarget(owner.Id, target, eff, cause, depth, root);
                }
            }
        }

        /// <summary>Teleport the leaper to the first free in-bounds hex adjacent to the
        /// target (fixed direction order = deterministic); drop its sticky target so it
        /// re-acquires nearest from the new position. No free hex = no leap.</summary>
        private void LeapTo(UnitState leaper, UnitState target)
        {
            var occupied = new HashSet<Hex>(_units.Where(x => x.Alive).Select(x => x.Pos));
            foreach (var f in _fields)
                if (f.Def.IsWall)
                    occupied.UnionWith(HexesOf(f));
            for (int d = 0; d < 6; d++)
            {
                Hex n = target.Pos.Neighbor(d);
                if (!InBounds(n) || occupied.Contains(n)) continue;
                leaper.Pos = n;
                leaper.TargetId = -1;
                Emit(new BattleEvent { Kind = EventKind.Move, Source = leaper.Id, Amount = n.Q, Aux = n.R });
                return;
            }
        }

        /// <summary>The single mutation dispatcher — trigger effects, signature effects,
        /// field pulses and projectile riders all land here.</summary>
        private void ApplyToTarget(int sourceId, UnitState target, EffectDef eff, Cause cause, int depth, int root)
        {
            switch (eff.Kind)
            {
                case EffectKind.Damage: DealDamage(sourceId, target, eff.Amount, cause, depth, root); break;
                case EffectKind.Heal: Heal(sourceId, target, eff.Amount, depth, root); break;
                case EffectKind.GrantShield:
                    target.Shield += eff.Amount;
                    Emit(new BattleEvent { Kind = EventKind.ShieldChanged, Source = sourceId, Target = target.Id, Amount = eff.Amount, Depth = depth, Root = root, PostShield = target.Shield });
                    break;
                case EffectKind.GrantMana: GainMana(target, eff.Amount, sourceId, depth, root); break;
                case EffectKind.ApplyStatus:
                    target.Statuses.Add(new Status { Kind = eff.Status, Mag = eff.Amount, TicksLeft = eff.StatusTicks, SourceId = sourceId });
                    Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = sourceId, Target = target.Id, Amount = eff.Amount, Aux = (int)eff.Status, Depth = depth, Root = root });
                    break;
            }
        }

        private List<UnitState> ResolveSelector(UnitState owner, Selector sel, BattleEvent ctx)
        {
            var result = new List<UnitState>();
            void AddIf(UnitState? u) { if (u != null && u.Alive) result.Add(u); }

            switch (sel.Kind)
            {
                case SelKind.Self: AddIf(owner); break;
                case SelKind.EventSource: AddIf(ById(ctx.Source)); break;
                case SelKind.EventTarget: AddIf(ById(ctx.Target)); break;
                case SelKind.CurrentTarget: AddIf(ById(owner.TargetId)); break;
                case SelKind.NearestEnemy:
                {
                    UnitState? best = null; int bestDist = int.MaxValue;
                    foreach (var u in _units)
                        if (u.Alive && u.Team != owner.Team)
                        {
                            int d = Hex.Distance(owner.Pos, u.Pos);
                            if (d < bestDist) { bestDist = d; best = u; }
                        }
                    AddIf(best); break;
                }
                case SelKind.FarthestEnemy:
                {
                    UnitState? best = null; int bestDist = -1;
                    foreach (var u in _units)
                        if (u.Alive && u.Team != owner.Team)
                        {
                            int d = Hex.Distance(owner.Pos, u.Pos);
                            if (d > bestDist) { bestDist = d; best = u; }
                        }
                    AddIf(best); break;
                }
                case SelKind.LowestHpAlly:
                {
                    UnitState? best = null;
                    foreach (var u in _units)
                        if (u.Alive && u.Team == owner.Team && !(sel.ExcludeSelf && u.Id == owner.Id))
                            if (best == null || u.Hp < best.Hp)
                                best = u;
                    AddIf(best); break;
                }
                case SelKind.AlliesWithin:
                    foreach (var u in _units)
                        if (u.Alive && u.Team == owner.Team && !(sel.ExcludeSelf && u.Id == owner.Id)
                            && Hex.Distance(owner.Pos, u.Pos) <= sel.Range)
                            result.Add(u);
                    break;
                case SelKind.EnemiesWithin:
                    foreach (var u in _units)
                        if (u.Alive && u.Team != owner.Team && Hex.Distance(owner.Pos, u.Pos) <= sel.Range)
                            result.Add(u);
                    break;
            }
            return result;
        }

        // ---- mutation primitives (every one emits with absolute post-state) ----

        private void DealDamage(int sourceId, UnitState target, int amount, Cause cause, int depth, int root, bool crit = false)
        {
            if (amount <= 0 || !target.Alive) return;
            int absorbed = Math.Min(target.Shield, amount);
            target.Shield -= absorbed;
            target.Hp -= amount - absorbed;
            Emit(new BattleEvent
            {
                Kind = EventKind.DamageDealt, Source = sourceId, Target = target.Id,
                Amount = amount, Aux = absorbed, Cause = cause, Depth = depth, Root = root, Crit = crit,
                PostHp = target.Hp, PostShield = target.Shield,
            });
            if (cause != Cause.Storm)
                GainMana(target, ManaPerHitTaken);
        }

        private void Heal(int sourceId, UnitState target, int amount, int depth, int root)
        {
            if (amount <= 0 || !target.Alive) return;
            int healed = Math.Min(amount, target.Def.MaxHp - target.Hp);
            if (healed <= 0) return;
            target.Hp += healed;
            Emit(new BattleEvent
            {
                Kind = EventKind.Heal, Source = sourceId, Target = target.Id,
                Amount = healed, Depth = depth, Root = root, PostHp = target.Hp,
            });
        }

        private void GainMana(UnitState u, int amount, int sourceId = -1, int depth = 0, int root = -1)
        {
            if (u.Def.ManaMax == 0 || u.Has(StatusKind.Silence) || !u.Alive) return;
            int gained = Math.Min(amount, u.Def.ManaMax - u.Mana);
            if (gained <= 0) return;
            u.Mana += gained;
            Emit(new BattleEvent
            {
                Kind = EventKind.ManaChanged, Source = sourceId, Target = u.Id,
                Amount = gained, Depth = depth, Root = root, PostMana = u.Mana,
            });
        }

        private void DeathPhase()
        {
            while (true)
            {
                var newly = new List<UnitState>();
                foreach (var u in _units)
                    if (!u.Dead && u.Hp <= 0)
                        newly.Add(u);
                if (newly.Count == 0) return;
                foreach (var u in newly)
                {
                    u.Dead = true;
                    Emit(new BattleEvent { Kind = EventKind.Death, Target = u.Id, PostHp = u.Hp });
                }
                Drain();
            }
        }

        // ---- lookups / bookkeeping ----

        private void AcquireTargets()
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                if (ById(u.TargetId) != null) continue; // sticky until the target dies
                UnitState? best = null;
                int bestDist = int.MaxValue;
                foreach (var e in _units)
                {
                    if (!e.Alive || e.Team == u.Team) continue;
                    int d = Hex.Distance(u.Pos, e.Pos);
                    if (d < bestDist) { bestDist = d; best = e; }
                }
                u.TargetId = best?.Id ?? -1;
            }
        }

        private UnitState? ById(int id)
        {
            var u = Raw(id);
            return u != null && u.Alive ? u : null;
        }

        private UnitState? Raw(int id)
        {
            if (id < 0) return null;
            foreach (var u in _units)
                if (u.Id == id)
                    return u;
            return null;
        }

        private Winner? CheckEnd()
        {
            bool t0 = false, t1 = false;
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                if (u.Team == 0) t0 = true; else t1 = true;
            }
            if (t0 && t1) return null;
            if (t0) return Winner.Team0;
            if (t1) return Winner.Team1;
            return Winner.Draw;
        }

        /// <summary>FNV-1a over living-unit state — the determinism/replay guardrail.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            void Mix(int v) { unchecked { h ^= (uint)v; h *= 1099511628211UL; } }
            Mix(_tick);
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                Mix(u.Id); Mix(u.Pos.Q); Mix(u.Pos.R); Mix(u.Hp); Mix(u.Shield); Mix(u.Mana);
                foreach (var s in u.Statuses) { Mix((int)s.Kind); Mix(s.Mag); Mix(s.TicksLeft); }
            }
            return h;
        }
    }
}
