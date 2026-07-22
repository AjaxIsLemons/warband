using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;

namespace Warband.Run
{
    public sealed class FightOutcome
    {
        public bool Won;
        public int EnemiesKilled;
        public int EnemyCount;
        public int BaseIncome;
        public int KillPayout;                   // per-kill slice of the pot (ADR 0007)
        public int WinBonus;                     // tier-scaled, only on a win
        public int GoldEarned => BaseIncome + KillPayout + WinBonus;
        public BattleResult Battle = null!;      // full log — playback, folds, inspection
    }

    /// <summary>
    /// The run state machine (roadmap 1a; ADRs 0002/0006/0007/0008). Node loop:
    /// pick tier → fight/event → payout → shop tick → next node; act close = ghost boss,
    /// snapshot capture, slot offer. Pure and deterministic: state + choices in, state out.
    /// </summary>
    public sealed class RunController
    {
        private const ulong SaltMap = 1, SaltEncounter = 2, SaltBattle = 3, SaltGhost = 4;
        private const int EnemyIdBase = 100;     // player heroes are 0..5, so no collision

        public RunState State { get; }
        private readonly IRunContent _content;
        private readonly RunConfig _cfg;

        public RunController(ulong seed, IRunContent content, IEnumerable<HeroInstance> warband,
                             RunConfig? config = null)
        {
            _content = content;
            _cfg = config ?? new RunConfig();
            State = new RunState { Seed = seed, FieldSlots = _cfg.StartingFieldSlots };
            State.Field.AddRange(warband);
            if (State.Field.Count < 1 || State.Field.Count > _cfg.StartingFieldSlots)
                throw new ArgumentException($"starting warband must be 1..{_cfg.StartingFieldSlots} heroes");
            State.ActMaps = GenerateMaps();
        }

        public bool AtBoss => State.NodeIndex == _cfg.NodesPerAct;

        public NodeKind CurrentNodeKind
        {
            get
            {
                Require(State.Phase == RunPhase.Node, "no current node outside the Node phase");
                return AtBoss ? NodeKind.Boss : State.ActMaps[State.Act - 1][State.NodeIndex];
            }
        }

        // ---- Node resolution -------------------------------------------------------

        /// <summary>Wager fight (ADR 0007): placement[i] positions Field[i], own half (rows 0-3).</summary>
        public FightOutcome ResolveFight(FightTier tier, IReadOnlyList<Hex> placement)
        {
            Require(State.Phase == RunPhase.Node && !AtBoss, "not at a node");
            Require(CurrentNodeKind == NodeKind.Fight, "current node is not a fight");

            var enemies = _content.Encounter(State.Act, State.NodeIndex, tier,
                                             RngFor(SaltEncounter, State.Act, State.NodeIndex));
            var outcome = RunBattle(placement, enemies, pot: _cfg.Pot(State.Act, tier),
                                    killSharePct: _cfg.TierKillSharePct[(int)tier]);
            State.Gold += outcome.GoldEarned;
            EnterShop(bossJustClosed: false);
            return outcome;
        }

        /// <summary>Events are undesigned (placeholder doctrine): base income only, for now.</summary>
        public int ResolveEvent()
        {
            Require(State.Phase == RunPhase.Node && !AtBoss, "not at a node");
            Require(CurrentNodeKind == NodeKind.Event, "current node is not an event");
            int gold = _cfg.BaseIncome(State.Act);
            State.Gold += gold;
            EnterShop(bossJustClosed: false);
            return gold;
        }

        /// <summary>Act-boss ghost fight (ADR 0002). Captures the player's board into the
        /// ghost pool regardless of result. Boss reward beyond the record is an open
        /// question — base income only, for now.</summary>
        public FightOutcome ResolveBoss(IReadOnlyList<Hex> placement)
        {
            Require(State.Phase == RunPhase.Node && AtBoss, "not at the act boss");

            var ghost = _content.BossGhost(State.Act, State.BossWins, RngFor(SaltGhost, State.Act));
            var enemies = new List<(UnitDef Def, Hex Pos)>();
            foreach (var g in ghost.Units)
                enemies.Add((ComposeDef(g.Hero), MirrorToEnemyHalf(g.Pos)));

            CaptureSnapshot(placement);
            var outcome = RunBattle(placement, enemies, pot: 0, killSharePct: 0);
            outcome.BaseIncome = _cfg.BaseIncome(State.Act);
            State.Gold += outcome.GoldEarned;
            if (outcome.Won) State.BossWins++; else State.BossLosses++;   // draw = not a win
            EnterShop(bossJustClosed: true);
            return outcome;
        }

        // ---- Shop phase (ADR 0006; offer/reroll stock arrives with roadmap 1b) ------

        public bool SlotOfferOpen => State.Phase == RunPhase.Shop && State.SlotOfferPending;

        public int SlotOfferCost
        {
            get { Require(SlotOfferOpen, "no slot offer open"); return _cfg.SlotCost(State.SlotsBought); }
        }

        public void BuySlot()
        {
            Require(SlotOfferOpen, "no slot offer open");
            int cost = _cfg.SlotCost(State.SlotsBought);
            Require(State.Gold >= cost, "not enough gold for the slot");
            State.Gold -= cost;
            State.FieldSlots++;
            State.SlotsBought++;
            State.SlotOfferPending = false;
        }

        public void BenchToField(int benchIndex)
        {
            Require(State.Phase == RunPhase.Shop, "roster moves only in the shop phase");
            Require(State.Field.Count < State.FieldSlots, "field is full");
            State.Field.Add(State.Bench[benchIndex]);
            State.Bench.RemoveAt(benchIndex);
        }

        public void FieldToBench(int fieldIndex)
        {
            Require(State.Phase == RunPhase.Shop, "roster moves only in the shop phase");
            Require(State.Bench.Count < _cfg.BenchSlots, "bench is full");
            State.Bench.Add(State.Field[fieldIndex]);
            State.Field.RemoveAt(fieldIndex);
        }

        /// <summary>Advance to the next node / act / run end. Leaving declines any open slot offer.</summary>
        public void LeaveShop()
        {
            Require(State.Phase == RunPhase.Shop, "not in the shop phase");
            State.SlotOfferPending = false;
            if (State.JustClosedBoss)
            {
                State.JustClosedBoss = false;
                if (State.Act == _cfg.Acts) { State.Phase = RunPhase.Complete; return; }
                State.Act++;
                State.NodeIndex = 0;
            }
            else
                State.NodeIndex++;
            State.Phase = RunPhase.Node;
        }

        // ---- Internals -------------------------------------------------------------

        private void EnterShop(bool bossJustClosed)
        {
            State.Phase = RunPhase.Shop;
            State.JustClosedBoss = bossJustClosed;
            State.SlotOfferPending = bossJustClosed && State.FieldSlots < _cfg.MaxFieldSlots;
        }

        private FightOutcome RunBattle(IReadOnlyList<Hex> placement,
                                       List<(UnitDef Def, Hex Pos)> enemies,
                                       int pot, int killSharePct)
        {
            ValidatePlacement(placement);
            var units = new List<UnitState>();
            for (int i = 0; i < State.Field.Count; i++)
            {
                var hero = State.Field[i];
                var composed = Compose(hero);
                units.Add(Loadout.Spawn(i, 0, composed, placement[i], hero.Earned));
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var (def, pos) = enemies[i];
                if (!Battle.InBounds(pos) || pos.Row < Battle.BoardRows / 2)
                    throw new InvalidOperationException("content placed an enemy outside the enemy half");
                units.Add(UnitState.Spawn(EnemyIdBase + i, 1, def, pos));
            }

            var result = new Battle(units, seed: Mix(State.Seed, SaltBattle,
                                    (ulong)State.Act, (ulong)State.NodeIndex)).Run();

            int killed = result.Events.Count(e => e.Kind == EventKind.Death && e.Target >= EnemyIdBase);
            bool won = result.Winner == Winner.Team0;

            for (int i = 0; i < State.Field.Count; i++)
            {
                var hero = State.Field[i];
                if (hero.RunBonuses.Count > 0)
                    hero.Earned.AddRange(ProgressionFold.Earned(result.Events, i, hero.RunBonuses));
            }

            return new FightOutcome
            {
                Won = won,
                EnemiesKilled = killed,
                EnemyCount = enemies.Count,
                BaseIncome = _cfg.BaseIncome(State.Act),
                KillPayout = enemies.Count > 0 ? pot * killSharePct * killed / (100 * enemies.Count) : 0,
                WinBonus = won ? pot * (100 - killSharePct) / 100 : 0,
                Battle = result,
            };
        }

        private ComposedLoadout Compose(HeroInstance hero) => Loadout.Compose(
            _content.Chassis(hero.ChassisId),
            hero.WeaponId == null ? null : _content.Weapon(hero.WeaponId),
            hero.TrinketIds.Select(_content.Trinket),
            hero.SpecNodeIds.Select(_content.Node));

        private UnitDef ComposeDef(HeroInstance hero)
        {
            var composed = Compose(hero);
            // Ghost run-earned bonuses ride along the same way the owner's did.
            foreach (var s in hero.Earned)
                composed.SpawnStatuses.Add(new Status { Kind = s.Kind, Mag = s.Mag, TicksLeft = -1 });
            return composed.Def;
        }

        /// <summary>Ghost placements are stored in owner-half rows (0-3); flip the row to
        /// field them as team 1. Col kept as-is — placeholder mirroring, revisit if hex
        /// parity ever makes flipped boards feel wrong.</summary>
        private static Hex MirrorToEnemyHalf(Hex h) =>
            Hex.FromRowCol(Battle.BoardRows - 1 - h.Row, h.Col);

        private void CaptureSnapshot(IReadOnlyList<Hex> placement)
        {
            ValidatePlacement(placement);
            var snap = new GhostSnapshot
            {
                Act = State.Act,
                WinsAtCapture = State.BossWins,
                LossesAtCapture = State.BossLosses,
            };
            for (int i = 0; i < State.Field.Count; i++)
                snap.Units.Add(new GhostUnit { Hero = State.Field[i].Clone(), Pos = placement[i] });
            State.CapturedGhosts.Add(snap);
        }

        private void ValidatePlacement(IReadOnlyList<Hex> placement)
        {
            if (State.Field.Count == 0)
                throw new InvalidOperationException("cannot fight with an empty field");
            if (placement.Count != State.Field.Count)
                throw new ArgumentException($"placement must position all {State.Field.Count} fielded heroes");
            var seen = new HashSet<Hex>();
            foreach (var h in placement)
            {
                if (!Battle.InBounds(h) || h.Row >= Battle.BoardRows / 2)
                    throw new ArgumentException($"hex {h} is not in the player half");
                if (!seen.Add(h))
                    throw new ArgumentException($"duplicate placement hex {h}");
            }
        }

        private NodeKind[][] GenerateMaps()
        {
            var maps = new NodeKind[_cfg.Acts][];
            for (int act = 1; act <= _cfg.Acts; act++)
            {
                var rng = RngFor(SaltMap, act);
                var nodes = new NodeKind[_cfg.NodesPerAct];
                for (int e = 0; e < _cfg.EventsPerAct; e++)
                {
                    int slot;
                    do { slot = rng.Next(_cfg.NodesPerAct); } while (nodes[slot] == NodeKind.Event);
                    nodes[slot] = NodeKind.Event;
                }
                maps[act - 1] = nodes;
            }
            return maps;
        }

        private Rng RngFor(ulong salt, int act, int node = 0) =>
            new Rng(Mix(State.Seed, salt, (ulong)act, (ulong)node));

        /// <summary>Stateless rng derivation (ADR 0008): nothing to persist, no ordering
        /// coupling between decisions — splitmix64 over (seed, purpose, act, node).</summary>
        private static ulong Mix(ulong a, ulong b, ulong c = 0, ulong d = 0)
        {
            static ulong Split(ulong z)
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
            return Split(Split(Split(a ^ Split(b)) + c) + d);
        }

        private static void Require(bool cond, string message)
        {
            if (!cond) throw new InvalidOperationException(message);
        }
    }
}
