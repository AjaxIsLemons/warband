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
        public const int RecentWindow = 30;        // ticks: the "damage taken recently" window (Phase entry)

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

                if (u.Def.ManaMax > 0 && u.Mana >= u.Def.ManaMax && !IsSilenced(u))
                    casts.Add(u);

                // Heal-auto units (censer law, ADR 0012) measure range/movement against
                // the lowest-HP ally; everyone else against their combat target.
                var target = u.Def.HealAutos ? LowestHpAllyOf(u) : ById(u.TargetId);
                if (target == null) continue;

                int dist = Hex.Distance(u.Pos, target.Pos);
                if (dist <= u.Def.Range)
                {
                    bool ready = _tick >= u.NextAttackTick || u.Has(StatusKind.Frenzied);
                    if (ready && !u.Has(StatusKind.Disarm))
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

                if (u.Def.HealAutos && target.Team == u.Team)
                {
                    // Censer swing: heals the lowest ally, builds mana, crits legally
                    // (attacks-only crit law — heal crits are big heals, ADR 0012).
                    u.SwingCount++;
                    Emit(new BattleEvent { Kind = EventKind.Attack, Source = u.Id, Target = target.Id, Cause = Cause.Attack });
                    int healAmt = u.EffAttack(RuleBonus(u, StatKind.AttackFlat));
                    bool healCrit = RollCrit(u);
                    if (healCrit) healAmt = healAmt * CritMult(u) / FP;
                    Heal(u.Id, target, healAmt, 0, u.Id);
                    GainMana(u, ManaPerAttack);
                    DecrementSwingCharges(u);
                    continue;
                }

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

                u.SwingCount++; // before the emit: Nth-swing conds see this swing at drain time
                Emit(new BattleEvent { Kind = EventKind.Attack, Source = u.Id, Target = target.Id, Cause = Cause.Attack });
                int damage = u.EffAttack(RuleBonus(u, StatKind.AttackFlat)) + bonus;
                bool crit = RollCrit(u);
                if (crit) damage = damage * CritMult(u) / FP;
                int amp = u.Sum(StatusKind.SwingAmpPct);
                if (amp > 0) damage = damage * (100 + amp) / 100;
                DealDamage(u.Id, target, damage, Cause.Attack, 0, u.Id, crit);

                // Cleave (greataxe shape, ADR 0015): the swing also hits enemies adjacent
                // to the TARGET at CleavePct. Rider-hits land at depth 1 — content can
                // tell them from the main strike (IsRootEvent).
                if (u.Def.CleavePct > 0)
                    foreach (var e in _units)
                        if (Targetable(e) && e.Team != u.Team && e.Id != target.Id
                            && Hex.Distance(e.Pos, target.Pos) == 1)
                            DealDamage(u.Id, e, damage * u.Def.CleavePct / 100, Cause.Attack, 1, u.Id, crit);

                // MultiShot (Volleyer law): while a window is open, Sum(ramp) extra arrows
                // strike the enemies nearest the target; the deficit re-strikes the target.
                if (u.Has(StatusKind.MultiShotWindow))
                {
                    int arrows = u.Sum(StatusKind.MultiShotRamp);
                    int pct = u.Sum(StatusKind.MultiShotWindow);
                    if (pct <= 0) pct = u.Def.ExtraArrowPct;
                    var pool = _units
                        .Where(e => Targetable(e) && e.Team != u.Team && e.Id != target.Id)
                        .OrderBy(e => Hex.Distance(e.Pos, target.Pos)).ThenBy(e => e.Id)
                        .ToList();
                    for (int i = 0; i < arrows; i++)
                    {
                        var victim = i < pool.Count ? pool[i] : target;
                        DealDamage(u.Id, victim, damage * pct / 100, Cause.Attack, 1, u.Id, crit); // depth 1: extras are rider-hits
                    }
                }

                if (riders != null)
                    foreach (var eff in riders)
                        ApplyToTarget(u.Id, target, eff, Cause.Field, 0, u.Id, eff.Amount);
                GainMana(u, ManaPerAttack);
                DecrementSwingCharges(u);
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

                u.RecentDamage.RemoveAll(d => d.Tick < _tick - RecentWindow);

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
                        else if (s.Kind == StatusKind.Burn)
                        {
                            // The Burn law (Pyro dive): tick = current stacks (amped by
                            // BurnAmp), then the pool decays by 1. No durations.
                            int tickDmg = s.Mag * (100 + u.Sum(StatusKind.BurnAmp)) / 100;
                            DealDamage(s.SourceId, u, tickDmg, Cause.Burn, 0, s.SourceId);
                            if (--s.Mag <= 0)
                            {
                                u.Statuses.Remove(s);
                                Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = 0, Aux = (int)StatusKind.Burn });
                            }
                        }
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
                                    ApplyToTarget(f.OwnerId, u, eff, Cause.Field, 0, f.OwnerId, eff.Amount);
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
                {
                    int mult = 1;
                    if (rule.ScaleBy == StatScale.DistanceToTarget)
                    {
                        // Full Draw (Sharpshot): the gradient innate — per hex to target.
                        var t = ById(u.TargetId);
                        mult = t == null ? 0 : Hex.Distance(u.Pos, t.Pos);
                    }
                    else if (rule.ScaleBy == StatScale.MissingHpPct10)
                    {
                        // Burning Hours (Berserker): per 10% of max HP missing.
                        mult = (u.Def.MaxHp - u.Hp) * 100 / u.Def.MaxHp / 10;
                    }
                    else if (rule.ScaleBy == StatScale.ShieldPer10)
                    {
                        // Grudgekeeper (Bulwark): the wall swings with its own weight.
                        mult = u.Shield / 10;
                    }
                    total += rule.Amount * mult;
                }
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
                    case CondKind.TargetBelowHpPct:
                    {
                        var t = Raw(ev.Target);
                        ok = t != null && t.Hp * 100 < c.Amount * t.Def.MaxHp; break;
                    }
                    case CondKind.TargetAtRangeOfOwner:
                    {
                        var t = ById(ev.Target);
                        ok = t != null && Hex.Distance(owner.Pos, t.Pos) == c.Amount; break;
                    }
                    case CondKind.NoEnemyWithinHexesOfOwner:
                    {
                        ok = true;
                        foreach (var e in _units)
                            if (e.Alive && e.Team != owner.Team && Hex.Distance(owner.Pos, e.Pos) <= c.Amount)
                            { ok = false; break; }
                        break;
                    }
                    case CondKind.TargetAdjacentToAllyOfOwner:
                    {
                        var t = Raw(ev.Target);
                        ok = false;
                        if (t != null)
                            foreach (var a in _units)
                                if (a.Alive && a.Team == owner.Team && a.Id != owner.Id
                                    && Hex.Distance(a.Pos, t.Pos) == 1)
                                { ok = true; break; }
                        break;
                    }
                    case CondKind.AnyEnemyTauntedByOwner:
                    {
                        ok = false;
                        foreach (var e in _units)
                        {
                            if (!e.Alive || e.Team == owner.Team) continue;
                            foreach (var s in e.Statuses)
                                if (s.Kind == StatusKind.Taunt && s.SourceId == owner.Id) { ok = true; break; }
                            if (ok) break;
                        }
                        break;
                    }
                    case CondKind.OwnerHasStatus: ok = owner.Has(c.Status); break;
                    case CondKind.TargetHasStatus:
                    {
                        var t = Raw(ev.Target);
                        ok = t != null && t.Has(c.Status); break;
                    }
                    case CondKind.SourceHasStatus:
                    {
                        var s2 = Raw(ev.Source);
                        ok = s2 != null && s2.Has(c.Status); break;
                    }
                    case CondKind.EveryNthSwingOfOwner:
                        ok = c.Amount > 0 && owner.SwingCount > 0 && owner.SwingCount % c.Amount == 0; break;
                    case CondKind.StatusIs: ok = ev.Aux == (int)c.Status; break;
                    case CondKind.IsRootEvent: ok = ev.Depth == 0; break;
                    case CondKind.AnyEnemyHasStatus:
                    {
                        ok = false;
                        foreach (var e in _units)
                            if (e.Alive && e.Team != owner.Team && e.Has(c.Status)) { ok = true; break; }
                        break;
                    }
                    case CondKind.TargetInFieldOfOwner:
                    {
                        var t = Raw(ev.Target);
                        ok = false;
                        if (t != null)
                            foreach (var f in _fields)
                                if (f.OwnerId == owner.Id && HexesOf(f).Contains(t.Pos)) { ok = true; break; }
                        break;
                    }
                    case CondKind.OwnerRecentDamageAbovePct:
                    {
                        int sum = 0;
                        foreach (var (tick, amt) in owner.RecentDamage)
                            if (tick >= _tick - RecentWindow) sum += amt;
                        ok = sum * 100 >= c.Amount * owner.Def.MaxHp; break;
                    }
                    default: ok = false; break;
                }
                if (ok == c.Not) return false;
            }
            return true;
        }

        private void ApplyEffectDef(UnitState owner, EffectDef eff, BattleEvent ctx, Cause cause, int depth, int root)
        {
            int index = 0;
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
                else if (eff.Kind == EffectKind.Swing)
                {
                    PerformEffectSwing(owner, target, eff, depth);
                }
                else if (eff.Kind == EffectKind.Recast)
                {
                    // Re-run the owner's signature anchored on the resolved target:
                    // CurrentTarget selects are remapped to the new anchor. Depth+1 —
                    // the cascade bound is the chain limit (Dying Star).
                    var ctx2 = new BattleEvent { Kind = ctx.Kind, Source = owner.Id, Target = target.Id, Depth = depth };
                    foreach (var sig in owner.Def.Signature)
                        ApplyEffectDef(owner, RemapToEventTarget(sig), ctx2, Cause.Ability, depth + 1, root);
                }
                else
                {
                    int amount = eff.Amount;
                    if (eff.PctOfEventAmount > 0) amount = ctx.Amount * eff.PctOfEventAmount / 100;
                    if (eff.ScaleByTargetStatus) amount = amount * target.Sum(eff.ScaleStatus);
                    if (eff.ScaleByEventTargetStatus)
                    {
                        var evTgt = Raw(ctx.Target);           // the corpse's pool (Contagion)
                        amount = evTgt == null ? 0 : amount * evTgt.Sum(eff.ScaleStatus);
                    }
                    if (eff.EscalatePctPerIndex > 0)           // farther down the line, harder
                        amount = amount * (100 + eff.EscalatePctPerIndex * index) / 100;
                    ApplyToTarget(owner.Id, target, eff, cause, depth, root, amount);
                }
                index++;
            }
        }

        private static EffectDef RemapToEventTarget(EffectDef eff)
        {
            if (eff.Select.Kind != SelKind.CurrentTarget) return eff;
            return new EffectDef
            {
                Kind = eff.Kind, Amount = eff.Amount, Status = eff.Status,
                StatusTicks = eff.StatusTicks, StatusSwings = eff.StatusSwings,
                Field = eff.Field, PctOfEventAmount = eff.PctOfEventAmount,
                ScaleByTargetStatus = eff.ScaleByTargetStatus, ScaleStatus = eff.ScaleStatus,
                AsCounter = eff.AsCounter,
                Select = new Selector { Kind = SelKind.EventTarget },
            };
        }

        /// <summary>A free swing granted by content (Twin Nock's second arrow, every
        /// Counter). AsCounter applies the directional law (Phalanx dive): strike the
        /// attacker if within reach, else the first enemy within reach on the line
        /// toward them; a clear line cuts air.</summary>
        private void PerformEffectSwing(UnitState owner, UnitState intended, EffectDef eff, int depth)
        {
            if (!owner.Alive || owner.Has(StatusKind.Disarm) || owner.Has(StatusKind.Stun)) return;
            UnitState? victim = intended;
            var cause = eff.AsCounter ? Cause.Counter : Cause.Attack;
            if (eff.AsCounter && Hex.Distance(owner.Pos, intended.Pos) > owner.Def.Range)
            {
                victim = null;
                var path = Hex.Line(owner.Pos, intended.Pos);
                for (int i = 1; i < path.Count && victim == null; i++)
                {
                    if (Hex.Distance(owner.Pos, path[i]) > owner.Def.Range) break;
                    foreach (var e in _units)
                        if (Targetable(e) && e.Team != owner.Team && e.Pos.Equals(path[i]))
                        { victim = e; break; }
                }
            }
            if (victim == null || !Targetable(victim) || victim.Team == owner.Team) return;

            // Effect swings never advance SwingCount or burn swing charges: a counter
            // must not eat your Volley window, and Nth-swing cadence tracks OWNED swings.
            int pct = eff.Amount > 0 ? eff.Amount : 100;
            Emit(new BattleEvent { Kind = EventKind.Attack, Source = owner.Id, Target = victim.Id, Cause = cause, Depth = depth });
            int damage = owner.EffAttack(RuleBonus(owner, StatKind.AttackFlat)) * pct / 100;
            bool crit = RollCrit(owner);
            if (crit) damage = damage * CritMult(owner) / FP;
            DealDamage(owner.Id, victim, damage, cause, depth, owner.Id, crit);
            GainMana(owner, ManaPerAttack);
        }

        private bool RollCrit(UnitState u)
        {
            if (u.Has(StatusKind.NextSwingCrit)) return true;
            int chance = u.Def.CritChance + u.Sum(StatusKind.CritUp);
            return chance > 0 && _rng.Next(100u) < (uint)chance;
        }

        private static int CritMult(UnitState u) => u.Def.CritMultFp + u.Sum(StatusKind.CritMultUp);

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
                // Distinct Leap event: Pikewall's landing punish and Leap banners hook
                // this without parsing Moves.
                Emit(new BattleEvent { Kind = EventKind.Leap, Source = leaper.Id, Target = target.Id, Amount = n.Q, Aux = n.R });
                return;
            }
        }

        /// <summary>The single mutation dispatcher — trigger effects, signature effects,
        /// field pulses and projectile riders all land here. Amount arrives pre-resolved
        /// (PctOfEventAmount / ScaleByTargetStatus are computed by the caller).</summary>
        private void ApplyToTarget(int sourceId, UnitState target, EffectDef eff, Cause cause, int depth, int root, int amount)
        {
            if (!target.Alive && eff.Kind != EffectKind.RemoveStatus) return; // corpses only host field spawns
            switch (eff.Kind)
            {
                case EffectKind.Damage: DealDamage(sourceId, target, amount, cause, depth, root); break;
                case EffectKind.Heal: Heal(sourceId, target, amount, depth, root); break;
                case EffectKind.GrantShield:
                    target.Shield += amount;
                    Emit(new BattleEvent { Kind = EventKind.ShieldChanged, Source = sourceId, Target = target.Id, Amount = amount, Depth = depth, Root = root, PostShield = target.Shield });
                    break;
                case EffectKind.GrantMana: GainMana(target, amount, sourceId, depth, root); break;
                case EffectKind.ApplyStatus:
                    if (eff.Status == StatusKind.Burn)
                    {
                        // The one-pool law (Pyro dive): all Burn merges into a single
                        // instance per unit — the number on the unit IS the threat.
                        Status? pool = null;
                        foreach (var s in target.Statuses)
                            if (s.Kind == StatusKind.Burn) { pool = s; break; }
                        if (pool != null) pool.Mag += amount;
                        else target.Statuses.Add(new Status { Kind = StatusKind.Burn, Mag = amount, TicksLeft = -1, SourceId = sourceId });
                        Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = sourceId, Target = target.Id, Amount = amount, Aux = (int)StatusKind.Burn, Depth = depth, Root = root });
                        break;
                    }
                    target.Statuses.Add(new Status { Kind = eff.Status, Mag = amount, TicksLeft = eff.StatusTicks, SwingsLeft = eff.StatusSwings, SourceId = sourceId });
                    Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = sourceId, Target = target.Id, Amount = amount, Aux = (int)eff.Status, Depth = depth, Root = root });
                    break;
                case EffectKind.RemoveStatus:
                    for (int i = target.Statuses.Count - 1; i >= 0; i--)
                        if (target.Statuses[i].Kind == eff.Status)
                        {
                            var s = target.Statuses[i];
                            target.Statuses.RemoveAt(i);
                            Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = target.Id, Amount = s.Mag, Aux = (int)s.Kind, Depth = depth, Root = root });
                        }
                    break;
                case EffectKind.Execute:
                    // Kill outright, through Shield (Reaper law: dead or nothing).
                    DealDamage(sourceId, target, target.Hp + target.Shield, cause, depth, root);
                    break;
            }
        }

        private List<UnitState> ResolveSelector(UnitState owner, Selector sel, BattleEvent ctx)
        {
            var result = new List<UnitState>();

            // Anchor: distance-measured kinds measure from the owner, the event source
            // ("around the Leaper"), or the event target ("around the corpse/victim").
            Hex anchor = owner.Pos;
            int anchorId = owner.Id;
            var anchorUnit = sel.AnchorEventTarget ? Raw(ctx.Target)
                           : sel.AnchorEvent ? Raw(ctx.Source) : null;
            if (anchorUnit != null) { anchor = anchorUnit.Pos; anchorId = anchorUnit.Id; }

            bool StatusOk(UnitState u) =>
                (sel.MustHave == null || u.Has(sel.MustHave.Value))
                && (sel.BelowHpPct <= 0 || u.Hp * 100 < sel.BelowHpPct * u.Def.MaxHp)
                && !(sel.ExcludeAnchorUnit && u.Id == anchorId);
            void AddIf(UnitState? u) { if (u != null && u.Alive && StatusOk(u)) result.Add(u); }

            switch (sel.Kind)
            {
                case SelKind.Self: AddIf(owner); break;
                case SelKind.EventSource: AddIf(ById(ctx.Source)); break;
                case SelKind.EventTarget:
                {
                    // Dead units resolve here on purpose: Death-triggered field spawns
                    // ignite the corpse hex (Inferno spread). Mutating effects guard.
                    var t = Raw(ctx.Target);
                    if (t != null && StatusOk(t)) result.Add(t);
                    break;
                }
                case SelKind.CurrentTarget: AddIf(ById(owner.TargetId)); break;
                case SelKind.NearestEnemy:
                {
                    UnitState? best = null; int bestDist = int.MaxValue;
                    foreach (var u in _units)
                        if (Targetable(u) && u.Team != owner.Team && StatusOk(u))
                        {
                            int d = Hex.Distance(anchor, u.Pos);
                            if (d < bestDist) { bestDist = d; best = u; }
                        }
                    if (best != null) result.Add(best);
                    break;
                }
                case SelKind.FarthestEnemy:
                {
                    UnitState? best = null; int bestDist = -1;
                    foreach (var u in _units)
                        if (Targetable(u) && u.Team != owner.Team && StatusOk(u))
                        {
                            int d = Hex.Distance(anchor, u.Pos);
                            if (d > bestDist) { bestDist = d; best = u; }
                        }
                    if (best != null) result.Add(best);
                    break;
                }
                case SelKind.LowestHpAlly:
                {
                    UnitState? best = null;
                    foreach (var u in _units)
                        if (u.Alive && u.Team == owner.Team && !(sel.ExcludeSelf && u.Id == owner.Id) && StatusOk(u))
                            if (best == null || u.Hp < best.Hp)
                                best = u;
                    if (best != null) result.Add(best);
                    break;
                }
                case SelKind.AlliesWithin:
                    foreach (var u in _units)
                        if (u.Alive && u.Team == owner.Team && !(sel.ExcludeSelf && u.Id == owner.Id)
                            && StatusOk(u) && Hex.Distance(anchor, u.Pos) <= sel.Range)
                            result.Add(u);
                    break;
                case SelKind.EnemiesWithin:
                    foreach (var u in _units)
                        if (Targetable(u) && u.Team != owner.Team && StatusOk(u)
                            && Hex.Distance(anchor, u.Pos) <= sel.Range)
                            result.Add(u);
                    break;
                case SelKind.EnemiesOnLineThroughTarget:
                case SelKind.EnemiesOnLineThroughFarthest:
                {
                    // The pierce line (Piercing Bolt, Lancer lunge, Sniper's bolt): from
                    // the owner, through the anchor enemy, extended onward.
                    // Range = max length in hexes; 0 = the whole board (Sarissa).
                    UnitState? through;
                    if (sel.Kind == SelKind.EnemiesOnLineThroughFarthest)
                    {
                        through = null; int best = -1;
                        foreach (var u in _units)
                            if (Targetable(u) && u.Team != owner.Team)
                            {
                                int d = Hex.Distance(owner.Pos, u.Pos);
                                if (d > best) { best = d; through = u; }
                            }
                    }
                    else
                        through = ById(ctx.Target) ?? ById(owner.TargetId);
                    if (through == null) break;
                    int maxLen = sel.Range > 0 ? sel.Range : BoardRows + BoardCols;
                    var far = new Hex(
                        owner.Pos.Q + (through.Pos.Q - owner.Pos.Q) * 16,
                        owner.Pos.R + (through.Pos.R - owner.Pos.R) * 16);
                    var path = Hex.Line(owner.Pos, far);
                    for (int i = 1; i < path.Count && i <= maxLen; i++)
                        foreach (var u in _units)
                            if (Targetable(u) && u.Team != owner.Team && StatusOk(u) && u.Pos.Equals(path[i])
                                && !(sel.SkipCtxTarget && u.Id == through.Id))
                                result.Add(u);
                    break;
                }
            }
            return result;
        }

        // ---- mutation primitives (every one emits with absolute post-state) ----

        private void DealDamage(int sourceId, UnitState target, int amount, Cause cause, int depth, int root, bool crit = false)
        {
            if (amount <= 0 || !target.Alive || target.Has(StatusKind.Phase)) return; // Phase = immune
            int taken = 100 + target.Sum(StatusKind.DamageTakenUp) - target.Sum(StatusKind.DamageTakenDown);
            if (taken != 100) amount = amount * (taken < 0 ? 0 : taken) / 100;
            if (amount <= 0) return;
            int absorbed = Math.Min(target.Shield, amount);
            target.Shield -= absorbed;
            target.Hp -= amount - absorbed;
            if (sourceId >= 0) target.LastDamagedBy = sourceId;   // killer attribution
            target.RecentDamage.Add((_tick, amount));             // Phase-entry window
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
            if (healed > 0)
            {
                target.Hp += healed;
                Emit(new BattleEvent
                {
                    Kind = EventKind.Heal, Source = sourceId, Target = target.Id,
                    Amount = healed, Depth = depth, Root = root, PostHp = target.Hp,
                });
            }
            int overflow = amount - healed;
            if (overflow > 0 && target.Has(StatusKind.OverhealToShield))
            {
                target.Shield += overflow;
                Emit(new BattleEvent
                {
                    Kind = EventKind.ShieldChanged, Source = sourceId, Target = target.Id,
                    Amount = overflow, Depth = depth, Root = root, PostShield = target.Shield,
                });
            }
        }

        private void GainMana(UnitState u, int amount, int sourceId = -1, int depth = 0, int root = -1)
        {
            if (u.Def.ManaMax == 0 || IsSilenced(u) || !u.Alive) return;
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
                    {
                        if (u.Has(StatusKind.CheatDeath))
                        {
                            // Deathless law (Berserker dive): consume ALL charges, refuse
                            // the death, stand at 1 HP. Emits its own event — banner hook.
                            for (int i = u.Statuses.Count - 1; i >= 0; i--)
                                if (u.Statuses[i].Kind == StatusKind.CheatDeath)
                                {
                                    var s = u.Statuses[i];
                                    u.Statuses.RemoveAt(i);
                                    Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)s.Kind });
                                }
                            u.Hp = 1;
                            Emit(new BattleEvent { Kind = EventKind.CheatDeath, Target = u.Id, PostHp = 1 });
                            continue;
                        }
                        newly.Add(u);
                    }
                if (newly.Count == 0) return;
                foreach (var u in newly)
                {
                    u.Dead = true;
                    // Source = killer, Amount = overkill — on-kill triggers and
                    // overkill-carry riders read straight off the event.
                    Emit(new BattleEvent
                    {
                        Kind = EventKind.Death, Target = u.Id, Source = u.LastDamagedBy,
                        Amount = u.Hp < 0 ? -u.Hp : 0, PostHp = u.Hp,
                    });
                }
                Drain();
            }
        }

        // ---- lookups / bookkeeping ----

        /// <summary>ADR 0013 targeting law: acquire nearest → sticky until death,
        /// untargetability (Phase) or Taunt. Taunt forces the target to the taunter
        /// while it lasts (last-applied instance wins — deterministic list order).</summary>
        private void AcquireTargets()
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;

                UnitState? taunter = null;
                foreach (var s in u.Statuses)
                    if (s.Kind == StatusKind.Taunt)
                    {
                        var t = ById(s.SourceId);
                        if (t != null && !t.Has(StatusKind.Phase)) taunter = t;
                    }
                if (taunter != null) { u.TargetId = taunter.Id; continue; }

                var cur = ById(u.TargetId);
                if (cur != null && !cur.Has(StatusKind.Phase)) continue; // sticky
                UnitState? best = null;
                int bestDist = int.MaxValue;
                foreach (var e in _units)
                {
                    if (!Targetable(e) || e.Team == u.Team) continue;
                    int d = Hex.Distance(u.Pos, e.Pos);
                    if (d < bestDist) { bestDist = d; best = e; }
                }
                u.TargetId = best?.Id ?? -1;
            }
        }

        private static bool Targetable(UnitState u) => u.Alive && !u.Has(StatusKind.Phase);

        /// <summary>Taunt bundles Silence behavior (Bulwark dive): no casting, no mana.</summary>
        private static bool IsSilenced(UnitState u) => u.Has(StatusKind.Silence) || u.Has(StatusKind.Taunt);

        private UnitState? LowestHpAllyOf(UnitState u)
        {
            UnitState? best = null;
            foreach (var a in _units)
                if (a.Alive && a.Team == u.Team)
                    if (best == null || a.Hp < best.Hp)
                        best = a;
            return best;
        }

        /// <summary>Swing-scoped charges (the next-N-swings shape — 4 dive votes) burn
        /// down one per completed swing and expire on their last.</summary>
        private void DecrementSwingCharges(UnitState u)
        {
            for (int i = u.Statuses.Count - 1; i >= 0; i--)
            {
                var s = u.Statuses[i];
                if (s.SwingsLeft > 0 && --s.SwingsLeft == 0)
                {
                    u.Statuses.RemoveAt(i);
                    Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)s.Kind });
                }
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
                Mix(u.SwingCount); Mix(u.LastDamagedBy);
                foreach (var s in u.Statuses) { Mix((int)s.Kind); Mix(s.Mag); Mix(s.TicksLeft); Mix(s.SwingsLeft); }
            }
            return h;
        }
    }
}
