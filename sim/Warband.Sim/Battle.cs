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
        /// <summary>The rule-id table TriggerFired/RuleChanged index into (Battle.BuildRuleTable).
        /// Part of the snapshot, not the event stream — ids are strings and BattleEvent is all ints.</summary>
        public List<string> RuleIds = new List<string>();
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
        public const int ManaPerAttack = 10;      // the default; a weapon may author its own (WeaponDef.ManaPerSwing)
        /// <summary>Frenzy's attack-speed bonus: +300%, i.e. 4× swing rate for the window's swings.
        /// It used to bypass the interval entirely ("a swing every tick"), which quietly made a
        /// Frenzy window worth 4 × weapon Damage with NO interval cost — so the heaviest weapon in
        /// the game was always the correct Frenzy weapon (musket 64 vs the Berserker's own daggers
        /// 24) and his dagger specialization was a trap. As a speed multiplier the burst still
        /// scales with weapon weight, but light weapons now win the window on damage per tick.</summary>
        public const int FrenzySpeedFp = FP * 3;
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
            // BEFORE the initial view: ViewOf projects each unit's rule span, so a table built after
            // it would leave the tick-0 snapshot at -1 while every later projection carried the real
            // base — and the fold guardrail would fail on the first tick of every fight.
            BuildRuleTable();
            _initialView = _units.Select(ViewOf).ToList();
        }

        /// <summary>
        /// Intern every rule id this battle could name into one flat, ordered table, and record each
        /// unit's offsets into it. TriggerFired/RuleChanged then carry an INDEX, which is what lets
        /// a passive name itself on an event model that is deliberately all ints.
        ///
        /// <para>One table rather than per-unit lists, because team rules — today's legacy Banners,
        /// tomorrow's Inscriptions (item 5a) — are owned by no unit and are exactly the layer this
        /// whole feature exists to make visible. A flat index space covers unit triggers, unit stat
        /// rules and team rules with one lookup on the client.</para>
        ///
        /// <para>Order is (unit id, triggers, stat rules) then team triggers in declaration order —
        /// deterministic, and derived from `_units` which is already sorted by id.</para>
        /// </summary>
        private void BuildRuleTable()
        {
            foreach (var u in _units)
            {
                u.TriggerBase = RuleIds.Count;
                foreach (var t in u.Def.Triggers) RuleIds.Add(t.RuleId ?? "");
                u.StatRuleBase = RuleIds.Count;
                foreach (var r in u.Def.StatRules) RuleIds.Add(r.RuleId ?? "");
            }
            _teamRuleBase = RuleIds.Count;
            foreach (var (_, t) in _teamTriggers) RuleIds.Add(t.RuleId ?? "");
        }

        /// <summary>The battle-wide rule-id table — see <see cref="BuildRuleTable"/>. Rides the
        /// replay so the client can resolve an index to a name without the sim.</summary>
        public List<string> RuleIds { get; } = new List<string>();
        private int _teamRuleBase;

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
                Aux3 = (int)def.Flavor(),
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

        private static PlaybackUnit ViewOf(UnitState u) => PlaybackUnit.From(u);

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
                        RuleIds = RuleIds,
                    };
                    _log.Add(new BattleEvent { Tick = _tick, Kind = EventKind.End, Amount = (int)result.Winner });
                    return result;
                }
                Step();
            }
        }

        /// <summary>
        /// Land every committed step whose arrival tick has come — BEFORE anything decides, so a
        /// unit that arrives this tick may depart again this tick. That back-to-back handoff is what
        /// makes a chase one continuous slide instead of a stutter every MoveInterval ticks.
        /// Deliberately blind to Root/Stun: control gates STARTING a step, never finishing one.
        /// </summary>
        private void Arrivals()
        {
            foreach (var u in _units)
            {
                if (!u.Alive || !u.Walking || _tick < u.StepEnd) continue;
                u.Pos = u.StepTo;
                u.StepStart = u.StepEnd = _tick;   // Walking false ⇒ StepTo folds back to Pos
                Emit(new BattleEvent { Kind = EventKind.Move, Source = u.Id, Amount = u.Pos.Q, Aux = u.Pos.R });
            }
        }

        private void Step()
        {
            Arrivals();
            EnginePhase();
            Drain();
            DeathPhase();

            // ---- decide (frozen state after engine phase) ----
            var attacks = new List<(UnitState u, UnitState target)>();
            var casts = new List<UnitState>();
            var moves = new List<(UnitState u, Hex dest)>();
            // A walker blocks BOTH hexes: the one it still stands on and the one it has reserved.
            // That single rule is what stops two units sliding through each other or converging on
            // the same tile mid-walk — the thing hex-teleporting never had to think about.
            var bodies = new HashSet<Hex>();
            foreach (var x in _units)
                if (x.Alive) { bodies.Add(x.Pos); bodies.Add(x.StepTo); }
            var walls = new HashSet<Hex>();
            foreach (var f in _fields)
                if (f.Def.IsWall)
                    walls.UnionWith(HexesOf(f));
            var occupied = new HashSet<Hex>(bodies);
            occupied.UnionWith(walls);

            // Routing reads the board FROZEN at the top of the phase: a unit's route must not
            // change because an ally decided behind it. `occupied` keeps growing with this tick's
            // reservations and governs only which hex may actually be stepped into.
            Pathing.EnterCost enter = h => walls.Contains(h) ? -1
                                         : bodies.Contains(h) ? Pathing.BodyCost : 1;
            var routes = new Dictionary<(int Target, int Range), int[]>();

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
                bool inRange = dist <= u.Def.Range;

                // Block-then-adapt: a target this unit already learned is wall-blocked is not
                // satisfiable from here. Re-check with resolution's exact verdict (shared
                // TraceProjectile) — a still-blocked line falls through to the move branch, where
                // the engage ring only contains hexes with a CLEAR line and the route field walks
                // it to one; a cleared line (or melee, dist < 2, which never blocks) drops the
                // flag and resumes fire. Keyed to the target id, so any retarget (death, Taunt,
                // Phase re-acquire) self-invalidates it — no extra clearing logic.
                if (inRange && u.BlockedTargetId == target.Id)
                {
                    if (TraceProjectile(u.Team, u.Pos, target.Pos, out _, out _) != null)
                        inRange = false;
                    else
                        u.BlockedTargetId = -1;
                }

                if (inRange)
                {
                    bool ready = _tick >= u.NextAttackTick;
                    if (ready && !u.Has(StatusKind.Disarm))
                        attacks.Add((u, target));

                    // Standoff: a unit with a preferred fighting distance gives ground when its
                    // target closes inside it, one hex per step, and only to hexes it can still
                    // shoot from — so it never retreats itself out of the fight. It keeps swinging
                    // while it withdraws (ADR 0018 clause 6). This terminates: the retreat stops the
                    // moment the target is at or beyond Standoff, or when the board edge / a body
                    // leaves nowhere farther to stand.
                    if (u.Def.Standoff > 0 && dist < u.Def.Standoff && !u.Walking && !u.Has(StatusKind.Root))
                    {
                        Hex? back = null;
                        int bestDist = dist;
                        for (int d = 0; d < 6; d++)
                        {
                            Hex n = u.Pos.Neighbor(d);
                            if (!InBounds(n) || occupied.Contains(n)) continue;
                            int nd = Hex.Distance(n, target.Pos);
                            if (nd > u.Def.Range || nd > u.Def.Standoff) continue;
                            if (nd > bestDist) { bestDist = nd; back = n; }
                        }
                        if (back != null)
                        {
                            occupied.Add(back.Value);
                            moves.Add((u, back.Value));
                        }
                    }
                }
                else
                {
                    // Route, don't hill-climb. The flow field runs out from every hex this unit
                    // could ATTACK the target from — clear projectile line included — so closing,
                    // walking around a crowd and hunting a firing angle are all one behaviour, and
                    // a body in the one closing direction is a detour instead of a life sentence.
                    // Origins still stay blocked for the tick: no same-tick train-following.
                    Hex? best = null;
                    if (!u.Walking && !u.Has(StatusKind.Root))
                    {
                        var field = RouteTo(routes, target, u.Def.Range, walls, enter);
                        best = Pathing.Step(field, u.Pos, n => !occupied.Contains(n));
                    }
                    if (best != null)
                    {
                        occupied.Add(best.Value);
                        moves.Add((u, best.Value));
                    }
                    else if (!u.Walking && !u.Has(StatusKind.Taunt))
                    {
                        // The engagement law. A unit that can neither strike its pick nor take a
                        // step toward it fights whatever it CAN strike from where it stands —
                        // otherwise a body boxed in by enemies stands and dies while the thing it
                        // wanted is on the far side of them, which is exactly what a diver that
                        // lands in a full backline used to do. Retargeting (rather than a free
                        // swing at someone else) keeps ONE intent per unit: the signature, the
                        // renderer and DistanceToTarget all read the enemy it is really fighting.
                        // Taunt is exempt — a taunt is not negotiable.
                        var reachable = u.Def.HealAutos ? LowestHpAllyInReach(u) : BestEnemyInReach(u);
                        if (reachable != null)
                        {
                            if (!u.Def.HealAutos) u.TargetId = reachable.Id;
                            if (_tick >= u.NextAttackTick && !u.Has(StatusKind.Disarm))
                                attacks.Add((u, reachable));
                        }
                    }
                }
            }

            // ---- apply ----
            // Commit, don't teleport: the unit departs now and lands MoveInterval ticks later (see
            // Arrivals). Cadence is untouched — depart T, arrive T+MI, free to depart again at T+MI
            // is still one hex per MoveInterval — but the walk now occupies real time, so the
            // renderer has an exact window to move through and never has to invent one.
            foreach (var (u, dest) in moves)
            {
                u.StepTo = dest;
                u.StepStart = _tick;
                u.StepEnd = _tick + (u.Def.MoveInterval < 1 ? 1 : u.Def.MoveInterval);
                Emit(new BattleEvent
                {
                    Kind = EventKind.MoveStart, Source = u.Id,
                    Amount = dest.Q, Aux = dest.R, Aux2 = u.StepEnd - u.StepStart,
                });
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
                    GainMana(u, u.Def.ManaPerSwing);
                    DecrementSwingCharges(u);
                    continue;
                }

                // Projectile rule: any attack over ≥2 hexes traces the hex line; fields
                // along the interior of the path may block or modify it (render-contract:
                // resolution stays instant, the PATH is the gameplay).
                Hex? blockedAt = TraceProjectile(u.Team, u.Pos, target.Pos, out int bonus, out List<EffectDef>? riders);

                if (blockedAt != null)
                {
                    // Block-then-adapt: the fizzle is information (render-contract §6). Mark this
                    // target wall-blocked so the DECISION phase stops standing here whiffing and
                    // advances until the line clears. The swing still burns (NextAttackTick above).
                    u.BlockedTargetId = target.Id;
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
                GainMana(u, u.Def.ManaPerSwing);
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
            SampleStatRules();
            Drain();   // log the transitions; they fire no triggers, so this is a no-op otherwise
            _tickViewHashes.Add(PlaybackState.HashView(_units.Select(ViewOf).ToList(), FieldViews()));
            _tick++;
        }

        /// <summary>
        /// The StatRule transition sweep — the reason a conditional passive can be seen at all.
        ///
        /// <para>A StatRule is a read-time predicate ("while below half HP: +6 attack"), evaluated
        /// fresh at every stat read and never cached, so there is no activation moment to hook and
        /// nothing ever reached the wire. The client may not evaluate the condition itself
        /// (render-contract law #1: it runs zero combat logic, ever), so the sim samples every rule
        /// once per tick and emits the EDGES. This is Underlords' "make the threshold a discrete
        /// event" applied to passives — see Design/passive-legibility.md.</para>
        ///
        /// <para>Runs after DeathPhase so a corpse's rules go offline in the same tick it dies.
        /// Order is (ascending unit id, declaration order), matching every other iteration here.
        /// Uses the SAME <see cref="CondsOk"/> call <see cref="RuleBonus"/> makes, so the badge and
        /// the damage number can never disagree about whether a rule is live.</para>
        /// </summary>
        private void SampleStatRules()
        {
            foreach (var u in _units)
            {
                var rules = u.Def.StatRules;
                if (rules.Count == 0) continue;
                if (u.RuleActive == null || u.RuleActive.Length != rules.Count)
                    u.RuleActive = new bool[rules.Count];
                for (int i = 0; i < rules.Count; i++)
                {
                    bool now = u.Alive && CondsOk(u, rules[i].When, NullEvent);
                    if (now == u.RuleActive[i]) continue;
                    u.RuleActive[i] = now;
                    Emit(new BattleEvent
                    {
                        Kind = EventKind.RuleChanged, Source = u.Id,
                        Aux = u.StatRuleBase + i, Amount = now ? 1 : 0,
                        Aux2 = now ? RuleValue(u, rules[i]) : 0,
                    });
                }
            }
        }

        private List<PlaybackField> FieldViews()
        {
            var views = new List<PlaybackField>();
            foreach (var f in _fields)
                views.Add(new PlaybackField
                {
                    Id = f.Id, IsWall = f.Def.IsWall, Flavor = f.Def.Flavor(),
                    AttachedTo = f.AttachedUnitId, Radius = f.Def.Radius,
                    Hexes = f.StaticHexes,
                });
            return views;
        }

        /// <summary>The projectile-path trace (render-contract §4: resolution is instant, the
        /// PATH is the gameplay). A shot over ≥2 hexes walks its hex line; each interior field
        /// (endpoints excluded) acts once — a wall blocks (returns where it stopped), a
        /// friendly/enemy field adds ProjectileBonus + riders. The SAME trace backs attack
        /// resolution AND the block-then-adapt decision re-check, so the block verdict the two
        /// see can never drift.</summary>
        private Hex? TraceProjectile(int shooterTeam, Hex from, Hex to, out int bonus, out List<EffectDef>? riders)
        {
            bonus = 0;
            riders = null;
            if (Hex.Distance(from, to) < 2) return null;
            var path = Hex.Line(from, to);
            var footprints = _fields.Select(f => (f, Hexes: HexesOf(f))).ToList();
            var crossed = new HashSet<int>(); // each field acts once, not per hex
            for (int i = 1; i < path.Count - 1; i++)
                foreach (var (f, hexes) in footprints)
                {
                    if (!hexes.Contains(path[i]) || !crossed.Add(f.Id)) continue;
                    if (f.Def.IsWall) return path[i];
                    if (Field.TeamMatches(f.Def.ProjectileAffects, f.OwnerTeam, shooterTeam))
                    {
                        bonus += f.Def.ProjectileBonus;
                        if (f.Def.ProjectileRiders.Count > 0)
                            (riders ??= new List<EffectDef>()).AddRange(f.Def.ProjectileRiders);
                    }
                }
            return null;
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
                                Emit(new BattleEvent { Kind = EventKind.StatusExpired, Target = u.Id, Amount = s.Mag, Aux = (int)StatusKind.Burn, Cause = Cause.Burn });
                            }
                            else
                                // The pool is one merged instance, so the only way its magnitude
                                // reaches a log-folding client is an event. Cause.Burn marks this as
                                // the decay pulse rather than a fresh stack.
                                Emit(new BattleEvent { Kind = EventKind.StatusApplied, Target = u.Id, Amount = s.Mag, Aux = (int)StatusKind.Burn, Aux2 = Countdown(s.TicksLeft), Aux3 = Countdown(s.SwingsLeft), Cause = Cause.Burn });
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
                            var granted = new Status { Kind = kind, Mag = mag, TicksLeft = -1, SourceId = f.Id };
                            u.Statuses.Add(granted);
                            Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = f.OwnerId, Target = u.Id, Amount = mag, Aux = (int)kind, Aux2 = Countdown(granted.TicksLeft), Aux3 = Countdown(granted.SwingsLeft) });
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

        /// <summary>StatusApplied's duration slots (Aux2 = ticks, Aux3 = swings): -1 means "not on
        /// this clock", so a client can tell a running countdown from a permanent hold without
        /// knowing which statuses are which.</summary>
        private static int Countdown(int left) => left > 0 ? left : -1;

        private void Drain()
        {
            int budget = MaxEventsPerDrain;
            while (_queue.Count > 0)
            {
                var ev = _queue.Dequeue();
                _log.Add(ev);
                // Presentation-only events are logged and then dropped: they fire no triggers AND
                // spend no cascade budget. That second half is the load-bearing one — without it,
                // announcing a passive would consume MaxEventsPerDrain faster than before and could,
                // in a deep enough cascade, change which triggers still got to fire. This feature
                // must be provably incapable of altering a fight's outcome, and this is where that
                // is enforced. Gate: `make baseline` byte-identical.
                if (ev.Kind == EventKind.TriggerFired || ev.Kind == EventKind.RuleChanged) continue;
                if (budget-- <= 0 || ev.Depth >= MaxCascadeDepth) continue; // logged, no further triggers

                foreach (var owner in _units)
                {
                    if (!owner.Alive) continue;
                    for (int i = 0; i < owner.Def.Triggers.Count; i++)
                        FireIfMatch(owner, owner.Def.Triggers[i], ev, owner.TriggerBase + i);
                }
                for (int team = 0; team <= 1; team++)
                    for (int i = 0; i < _teamTriggers.Count; i++)
                    {
                        var (t, trig) = _teamTriggers[i];
                        if (t != team) continue;
                        foreach (var owner in _units)
                            if (owner.Alive && owner.Team == team)
                            { FireIfMatch(owner, trig, ev, _teamRuleBase + i); break; } // once per team, lowest-id rep
                    }
            }
        }

        private void FireIfMatch(UnitState owner, Trigger trig, BattleEvent ev, int ruleIndex)
        {
            if (trig.On != ev.Kind || !CondsOk(owner, trig.When, ev)) return;
            // Announce the passive BEFORE its effects, so the tell is the cause of the children that
            // follow it in drain order rather than an afterthought behind them. Target is what the
            // triggering event was about, which is the far end of the attribution spark-link.
            Emit(new BattleEvent
            {
                Kind = EventKind.TriggerFired, Source = owner.Id,
                Target = ev.Target >= 0 ? ev.Target : ev.Source,
                Aux = ruleIndex, Depth = ev.Depth + 1, Root = owner.Id,
            });
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
                    total += RuleValue(u, rule);
            return total;
        }

        /// <summary>A single StatRule's contribution right now, conditions ASSUMED already checked.
        /// Split out of <see cref="RuleBonus"/> so <see cref="SampleStatRules"/> can put the same
        /// number on the wire that the stat read will use — one implementation, so the badge and the
        /// damage it explains cannot drift apart.</summary>
        private int RuleValue(UnitState u, StatRule rule)
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
            return rule.Amount * mult;
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
            GainMana(owner, owner.Def.ManaPerSwing);
        }

        private bool RollCrit(UnitState u)
        {
            if (u.Has(StatusKind.NextSwingCrit)) return true;
            int chance = u.Def.CritChance + u.Sum(StatusKind.CritUp);
            return chance > 0 && _rng.Next(100u) < (uint)chance;
        }

        private static int CritMult(UnitState u) => u.Def.CritMultFp + u.Sum(StatusKind.CritMultUp);

        /// <summary>Teleport the leaper to the first free in-bounds hex adjacent to the target
        /// (fixed direction order = deterministic), and make that target its fight. No free hex =
        /// no leap.
        ///
        /// The leap used to clear TargetId and re-acquire by preference from the landing hex, which
        /// pre-dates TargetPref and inverts every diver that has one: a Farthest-seeking stalker
        /// leaps into your backline and immediately re-acquires the far side of the board — the
        /// front line it just jumped over — so it turns round and walks back. The selector already
        /// chose who to jump on; landing on someone and then hunting someone else is not a dive.</summary>
        private void LeapTo(UnitState leaper, UnitState target)
        {
            var occupied = new HashSet<Hex>();
            foreach (var x in _units)
                if (x.Alive) { occupied.Add(x.Pos); occupied.Add(x.StepTo); } // reserved hexes are taken
            foreach (var f in _fields)
                if (f.Def.IsWall)
                    occupied.UnionWith(HexesOf(f));
            Hex from = leaper.Pos;
            for (int d = 0; d < 6; d++)
            {
                Hex n = target.Pos.Neighbor(d);
                if (!InBounds(n) || occupied.Contains(n)) continue;
                leaper.Pos = n;
                // Displacement outranks a walk: drop the commitment and release the reserved hex, so
                // the Move below stands alone. A Move with no MoveStart is exactly the renderer's
                // teleport signal — a leap blinks, it does not slide.
                leaper.StepStart = leaper.StepEnd = _tick;
                leaper.TargetId = target.Id;
                Emit(new BattleEvent { Kind = EventKind.Move, Source = leaper.Id, Amount = n.Q, Aux = n.R });
                // Distinct Leap event: Pikewall's landing punish and Leap banners hook
                // this without parsing Moves. It carries BOTH endpoints — the renderer arcs the
                // body from `from` to `n`, and by the time it sees this the fold has already
                // applied the landing, so the take-off would otherwise be unrecoverable.
                Emit(new BattleEvent
                {
                    Kind = EventKind.Leap, Source = leaper.Id, Target = target.Id,
                    Amount = n.Q, Aux = n.R, Aux2 = from.Q, Aux3 = from.R,
                });
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
                        else target.Statuses.Add(pool = new Status { Kind = StatusKind.Burn, Mag = amount, TicksLeft = -1, SourceId = sourceId });
                        // Absolute pool, not the delta: a merged instance has no identity of its
                        // own, so the total is the only value a log-folding client can SET (ADR
                        // 0004 — clients never accumulate).
                        Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = sourceId, Target = target.Id, Amount = pool.Mag, Aux = (int)StatusKind.Burn, Aux2 = Countdown(pool.TicksLeft), Aux3 = Countdown(pool.SwingsLeft), Depth = depth, Root = root });
                        break;
                    }
                    var applied = new Status { Kind = eff.Status, Mag = amount, TicksLeft = eff.StatusTicks, SwingsLeft = eff.StatusSwings, SourceId = sourceId };
                    target.Statuses.Add(applied);
                    Emit(new BattleEvent { Kind = EventKind.StatusApplied, Source = sourceId, Target = target.Id, Amount = amount, Aux = (int)eff.Status, Aux2 = Countdown(applied.TicksLeft), Aux3 = Countdown(applied.SwingsLeft), Depth = depth, Root = root });
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
                GainMana(target, target.Def.ManaPerHitTaken);
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
                    u.StepStart = u.StepEnd = _tick; // a corpse never arrives, and frees its reserved hex
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

        /// <summary>ADR 0013 targeting law: acquire by the unit's <see cref="TargetPref"/>
        /// (Nearest unless the kit says otherwise) → sticky until death, untargetability
        /// (Phase) or Taunt. Taunt forces the target to the taunter while it lasts
        /// (last-applied instance wins — deterministic list order).</summary>
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

                // Whose fight is it? Nearest is ADR 0013's default and stays the default; a kit
                // may declare otherwise (combat-grammar.md's long-promised "kits override"). Only
                // ACQUISITION is preference-driven — stickiness, Phase and Taunt above are
                // untouched. Every comparison is strict, and iteration is by ascending id, so
                // ties always fall to the lowest id: order-independent, no rng, no floats.
                UnitState? best = null;
                int bestScore = 0;
                foreach (var e in _units)
                {
                    if (!Targetable(e) || e.Team == u.Team) continue;
                    int score = TargetScore(u, e);
                    if (best == null || score > bestScore) { bestScore = score; best = e; }
                }
                u.TargetId = best?.Id ?? -1;
            }
        }

        /// <summary>How much this unit wants to fight that one — higher wins, ties fall to the
        /// lowest id because iteration is by ascending id and every comparison is strict.</summary>
        private int TargetScore(UnitState u, UnitState e) => u.Def.TargetPref switch
        {
            TargetPref.Farthest => Hex.Distance(u.Pos, e.Pos),
            TargetPref.LowestHp => -(e.Hp + e.Shield),
            TargetPref.HighestHp => e.Hp + e.Shield,
            _ => -Hex.Distance(u.Pos, e.Pos),   // Nearest
        };

        /// <summary>The engage ring: every in-bounds hex this unit could attack `target` from —
        /// inside weapon reach, not inside a wall, and with a projectile line that actually
        /// arrives. Cached per (target, range) for the tick: the ring depends on neither the
        /// shooter's identity nor its team, because a wall blocks every team's shot alike.</summary>
        private int[] RouteTo(Dictionary<(int Target, int Range), int[]> cache, UnitState target,
                              int range, HashSet<Hex> walls, Pathing.EnterCost enter)
        {
            var key = (target.Id, range);
            if (cache.TryGetValue(key, out var cached)) return cached;

            var ring = new List<Hex>();
            foreach (var h in Hex.Range(target.Pos, range))
            {
                if (!InBounds(h) || walls.Contains(h)) continue;
                // Only worth tracing when a wall exists at all; nothing else stops a shot.
                if (walls.Count > 0 && TraceProjectile(target.Team, h, target.Pos, out _, out _) != null)
                    continue;
                ring.Add(h);
            }
            var field = Pathing.Field(ring, enter);
            cache[key] = field;
            return field;
        }

        /// <summary>Best enemy strikeable from exactly where this unit stands, by its own
        /// TargetPref — the engagement law's shortlist.</summary>
        private UnitState? BestEnemyInReach(UnitState u)
        {
            UnitState? best = null;
            int bestScore = 0;
            foreach (var e in _units)
            {
                if (!Targetable(e) || e.Team == u.Team) continue;
                if (Hex.Distance(u.Pos, e.Pos) > u.Def.Range) continue;
                if (TraceProjectile(u.Team, u.Pos, e.Pos, out _, out _) != null) continue;
                int score = TargetScore(u, e);
                if (best == null || score > bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        /// <summary>The censer's version of the engagement law (ADR 0012): a boxed-in heal-auto
        /// unit still mends whoever it can actually reach.</summary>
        private UnitState? LowestHpAllyInReach(UnitState u)
        {
            UnitState? best = null;
            foreach (var a in _units)
                if (a.Alive && a.Team == u.Team && Hex.Distance(u.Pos, a.Pos) <= u.Def.Range)
                    if (best == null || a.Hp < best.Hp)
                        best = a;
            return best;
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
