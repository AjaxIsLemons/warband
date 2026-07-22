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
        private const ulong SaltMap = 1, SaltEncounter = 2, SaltBattle = 3, SaltGhost = 4, SaltShop = 5;
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
            var outcome = RunBattle(placement, enemies, pot: 0, killSharePct: 0,
                                    enemyBanners: ghost.BannerIds);
            outcome.BaseIncome = _cfg.BaseIncome(State.Act);
            State.Gold += outcome.GoldEarned;
            if (outcome.Won) State.BossWins++; else State.BossLosses++;
            EnterShop(bossJustClosed: true);
            return outcome;
        }

        // ---- Shop phase (ADR 0006 cadence, ADR 0009 stock) --------------------------

        /// <summary>Buy the offer at a slot. Hero card of an owned chassis = dupe: rank-up
        /// fires and the 1-of-2 spec choice must be resolved before any other shop action.</summary>
        public void BuyOffer(int index)
        {
            RequireShopActionable();
            var offer = OfferAt(index);
            Require(State.Gold >= offer.Price, "not enough gold");
            switch (offer.Kind)
            {
                case OfferKind.Hero: BuyHero(offer); break;
                case OfferKind.Weapon:
                    State.Inventory.Add(new ItemRef { Kind = ItemKind.Weapon, Id = offer.Id }); break;
                case OfferKind.Trinket:
                    State.Inventory.Add(new ItemRef { Kind = ItemKind.Trinket, Id = offer.Id }); break;
                case OfferKind.Banner: State.Banners.Add(offer.Id); break;
            }
            State.Gold -= offer.Price;
            State.ShopOffers[index] = null;
        }

        public void Reroll()
        {
            RequireShopActionable();
            Require(State.Gold >= _cfg.RerollCost, "not enough gold to reroll");
            State.Gold -= _cfg.RerollCost;
            GenerateShop();
        }

        public void ToggleFreeze(int index)
        {
            RequireShopActionable();
            var offer = OfferAt(index);
            offer.Frozen = !offer.Frozen;
        }

        /// <summary>Resolve the pending rank-up choice: 0 = OptionA, 1 = OptionB.</summary>
        public void ChooseSpec(int which)
        {
            Require(State.Phase == RunPhase.Shop && State.PendingSpec != null, "no spec choice pending");
            var p = State.PendingSpec!;
            var hero = Zone(p.Zone)[p.Index];
            string chosen = which == 0 ? p.OptionA : p.OptionB;
            hero.SpecNodeIds.Add(chosen);
            if (p.ForRank == Rank.B) hero.PathId = chosen;   // the fork sets the path
            State.PendingSpec = null;
        }

        public void SellHero(RosterZone zone, int index)
        {
            RequireShopActionable();
            var list = Zone(zone);
            var hero = list[index];
            UnequipWeapon(zone, index);
            while (hero.TrinketIds.Count > 0) UnequipTrinket(zone, index);
            State.Gold += hero.GoldSpent * _cfg.SellPct / 100;
            list.RemoveAt(index);
        }

        public void SellItem(int invIndex)
        {
            RequireShopActionable();
            var item = State.Inventory[invIndex];
            State.Gold += ItemPrice(item.Kind) * _cfg.SellPct / 100;
            State.Inventory.RemoveAt(invIndex);
        }

        public void EquipWeapon(RosterZone zone, int index, int invIndex)
        {
            RequireShopActionable();
            var item = State.Inventory[invIndex];
            Require(item.Kind == ItemKind.Weapon, "not a weapon");
            var hero = Zone(zone)[index];
            State.Inventory.RemoveAt(invIndex);
            if (hero.WeaponId != null)
                State.Inventory.Add(new ItemRef { Kind = ItemKind.Weapon, Id = hero.WeaponId });
            hero.WeaponId = item.Id;
        }

        public void EquipTrinket(RosterZone zone, int index, int invIndex)
        {
            RequireShopActionable();
            var item = State.Inventory[invIndex];
            Require(item.Kind == ItemKind.Trinket, "not a trinket");
            var hero = Zone(zone)[index];
            State.Inventory.RemoveAt(invIndex);
            if (hero.TrinketIds.Count > 0)                   // one trinket slot (heroes.md)
            {
                State.Inventory.Add(new ItemRef { Kind = ItemKind.Trinket, Id = hero.TrinketIds[0] });
                hero.TrinketIds.Clear();
            }
            hero.TrinketIds.Add(item.Id);
        }

        public void UnequipWeapon(RosterZone zone, int index)
        {
            RequireShopActionable();
            var hero = Zone(zone)[index];
            if (hero.WeaponId == null) return;               // starter isn't an item
            State.Inventory.Add(new ItemRef { Kind = ItemKind.Weapon, Id = hero.WeaponId });
            hero.WeaponId = null;
        }

        public void UnequipTrinket(RosterZone zone, int index)
        {
            RequireShopActionable();
            var hero = Zone(zone)[index];
            if (hero.TrinketIds.Count == 0) return;
            State.Inventory.Add(new ItemRef { Kind = ItemKind.Trinket, Id = hero.TrinketIds[0] });
            hero.TrinketIds.RemoveAt(0);
        }

        public bool SlotOfferOpen => State.Phase == RunPhase.Shop && State.SlotOfferPending;

        public int SlotOfferCost
        {
            get { Require(SlotOfferOpen, "no slot offer open"); return _cfg.SlotCost(State.SlotsBought); }
        }

        public void BuySlot()
        {
            RequireShopActionable();
            Require(SlotOfferOpen, "no slot offer open");
            int cost = _cfg.SlotCost(State.SlotsBought);
            Require(State.Gold >= cost, "not enough gold for the slot");
            State.Gold -= cost;
            State.FieldSlots++;
            State.SlotsBought++;
            State.SlotOfferPending = false;
        }

        public bool HasRoomForRecruit =>
            State.Field.Count < State.FieldSlots || State.Bench.Count < _cfg.BenchSlots;

        public void BenchToField(int benchIndex)
        {
            RequireShopActionable();
            Require(State.Field.Count < State.FieldSlots, "field is full");
            State.Field.Add(State.Bench[benchIndex]);
            State.Bench.RemoveAt(benchIndex);
        }

        public void FieldToBench(int fieldIndex)
        {
            RequireShopActionable();
            Require(State.Bench.Count < _cfg.BenchSlots, "bench is full");
            State.Bench.Add(State.Field[fieldIndex]);
            State.Field.RemoveAt(fieldIndex);
        }

        /// <summary>Advance to the next node / act / run end. Leaving declines any open slot offer.</summary>
        public void LeaveShop()
        {
            Require(State.Phase == RunPhase.Shop, "not in the shop phase");
            Require(State.PendingSpec == null, "resolve the spec choice before leaving");
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
            GenerateShop();
        }

        /// <summary>Roll the shop: frozen offers keep their slots, the rest regenerate.
        /// Slots 0..HeroSlots-1 are hero cards, the remainder item cards (ADR 0009).</summary>
        private void GenerateShop()
        {
            var rng = RngFor(SaltShop, State.ShopRolls++);
            int slots = _cfg.HeroSlots + _cfg.ItemSlots;
            var offers = new List<ShopOffer?>();
            for (int i = 0; i < slots; i++)
            {
                var old = i < State.ShopOffers.Count ? State.ShopOffers[i] : null;
                if (old != null && old.Frozen) { offers.Add(old); continue; }
                offers.Add(i < _cfg.HeroSlots ? RollHero(rng) : RollItem(rng));
            }
            State.ShopOffers = offers;
        }

        private ShopOffer? RollHero(Rng rng)
        {
            var pool = new List<string>();
            foreach (var id in _content.HeroPool(State.Act))
                if (!OwnedAtMaxRank(id)) pool.Add(id);
            if (pool.Count == 0) return null;
            return new ShopOffer
            {
                Kind = OfferKind.Hero, Id = pool[rng.Next(pool.Count)], Price = _cfg.HeroPrice,
            };
        }

        private ShopOffer? RollItem(Rng rng)
        {
            // Draw order is fixed (banner roll, then kind, then id) — determinism law.
            bool tryBanner = rng.Next(100) < _cfg.BannerChancePct;
            if (tryBanner)
            {
                var banners = new List<string>();
                foreach (var id in _content.BannerPool(State.Act))
                    if (!State.Banners.Contains(id)) banners.Add(id);
                if (banners.Count > 0)
                    return new ShopOffer
                    {
                        Kind = OfferKind.Banner, Id = banners[rng.Next(banners.Count)],
                        Price = _cfg.BannerPrice,
                    };
            }
            var weapons = _content.WeaponPool(State.Act);
            var trinkets = _content.TrinketPool(State.Act);
            bool weapon = weapons.Count > 0 && (trinkets.Count == 0 || rng.Next(2) == 0);
            if (weapon)
                return new ShopOffer
                {
                    Kind = OfferKind.Weapon, Id = weapons[rng.Next(weapons.Count)],
                    Price = _cfg.WeaponPrice,
                };
            if (trinkets.Count == 0) return null;
            return new ShopOffer
            {
                Kind = OfferKind.Trinket, Id = trinkets[rng.Next(trinkets.Count)],
                Price = _cfg.TrinketPrice,
            };
        }

        private void BuyHero(ShopOffer offer)
        {
            for (int z = 0; z < 2; z++)
            {
                var zone = (RosterZone)z;
                var list = Zone(zone);
                for (int i = 0; i < list.Count; i++)
                    if (list[i].ChassisId == offer.Id)
                    {
                        var hero = list[i];
                        Require(hero.Rank < Rank.S, "chassis already at max rank");
                        hero.Rank++;
                        hero.GoldSpent += offer.Price;
                        var (a, b) = _content.SpecOptions(hero.ChassisId, hero.Rank, hero.PathId);
                        State.PendingSpec = new PendingSpec
                        {
                            Zone = zone, Index = i, ForRank = hero.Rank, OptionA = a, OptionB = b,
                        };
                        return;
                    }
            }
            var recruit = new HeroInstance { ChassisId = offer.Id, GoldSpent = offer.Price };
            if (State.Field.Count < State.FieldSlots) State.Field.Add(recruit);
            else if (State.Bench.Count < _cfg.BenchSlots) State.Bench.Add(recruit);
            else throw new InvalidOperationException("no room — field and bench are full");
        }

        private bool OwnedAtMaxRank(string chassisId) =>
            State.Field.Concat(State.Bench).Any(h => h.ChassisId == chassisId && h.Rank == Rank.S);

        private List<HeroInstance> Zone(RosterZone zone) =>
            zone == RosterZone.Field ? State.Field : State.Bench;

        private ShopOffer OfferAt(int index)
        {
            Require(index >= 0 && index < State.ShopOffers.Count && State.ShopOffers[index] != null,
                    "no offer in that slot");
            return State.ShopOffers[index]!;
        }

        private int ItemPrice(ItemKind kind) =>
            kind == ItemKind.Weapon ? _cfg.WeaponPrice : _cfg.TrinketPrice;

        private void RequireShopActionable()
        {
            Require(State.Phase == RunPhase.Shop, "only available in the shop phase");
            Require(State.PendingSpec == null, "resolve the pending spec choice first");
        }

        private FightOutcome RunBattle(IReadOnlyList<Hex> placement,
                                       List<(UnitDef Def, Hex Pos)> enemies,
                                       int pot, int killSharePct,
                                       List<string>? enemyBanners = null)
        {
            ValidatePlacement(placement);
            var teamTriggers = new List<(int Team, Trigger T)>();
            foreach (var id in State.Banners)
                foreach (var t in _content.Banner(id).TeamTriggers)
                    teamTriggers.Add((0, t));
            if (enemyBanners != null)
                foreach (var id in enemyBanners)
                    foreach (var t in _content.Banner(id).TeamTriggers)
                        teamTriggers.Add((1, t));
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

            var result = new Battle(units, teamTriggers,
                                    seed: Mix(State.Seed, SaltBattle,
                                              (ulong)State.Act, (ulong)State.NodeIndex)).Run();

            int killed = result.Events.Count(e => e.Kind == EventKind.Death && e.Target >= EnemyIdBase);
            // Draws count as wins (Jake, 2026-07-22): your board wasn't beaten. Applies to
            // both the boss record and the wager bonus.
            bool won = result.Winner != Winner.Team1;

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
            snap.BannerIds.AddRange(State.Banners);
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
