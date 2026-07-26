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
        public int KillPayout;                   // compatibility: terminal losses no longer pay
        public int WinBonus;                     // compatibility: reward is now one visible amount
        public int GoldEarned => BaseIncome + KillPayout + WinBonus;
        public int SandEarned => GoldEarned;
        public BattleResult Battle = null!;      // full log — playback, folds, inspection
    }

    /// <summary>
    /// The run state machine. Planning is one persistent phase: market, roster, intel, loadout,
    /// and deployment remain available around the current beat. A successful beat advances the
    /// authored act track and refreshes unfrozen market stock; defeat is terminal.
    /// </summary>
    public sealed class RunController
    {
        private const ulong SaltMap = 1, SaltEncounter = 2, SaltBattle = 3, SaltShop = 5,
                            SaltInterlude = 6, SaltBossReward = 7;
        private const int EnemyIdBase = 100;     // player heroes are 0..5, so no collision

        public RunState State { get; }
        private readonly IRunContent _content;
        private readonly RunConfig _cfg;

        public RunController(ulong seed, IRunContent content, IEnumerable<HeroInstance> warband,
                             RunConfig? config = null)
        {
            _content = content;
            _cfg = config ?? new RunConfig();
            ValidateConfig();
            State = new RunState
            {
                Seed = seed,
                Gold = _cfg.StartingSand,
                FieldSlots = _cfg.StartingFieldSlots,
                UnlockedFieldSlots = _cfg.StartingFieldSlots,
                Phase = RunPhase.Planning,
            };
            State.Field.AddRange(warband);
            if (State.Field.Count < 1 || State.Field.Count > _cfg.StartingFieldSlots)
                throw new ArgumentException($"starting warband must be 1..{_cfg.StartingFieldSlots} heroes");
            State.ActMaps = GenerateMaps();
            GenerateShop();
        }

        public bool AtBoss => State.NodeIndex == _cfg.NodesPerAct;

        public NodeKind CurrentNodeKind
        {
            get
            {
                Require(State.Phase == RunPhase.Planning, "no current beat outside Planning");
                return AtBoss ? NodeKind.Boss : State.ActMaps[State.Act - 1][State.NodeIndex];
            }
        }

        // ---- Node resolution -------------------------------------------------------

        /// <summary>
        /// Resolve the selected risk variant. A win pays its one visible Sand reward; a loss ends
        /// the run and pays nothing because there is no later market in which to spend it.
        /// </summary>
        public FightOutcome ResolveFight(FightTier tier, IReadOnlyList<Hex> placement)
        {
            Require(State.Phase == RunPhase.Planning && !AtBoss, "not at a normal encounter");
            Require(CurrentNodeKind == NodeKind.Fight, "current node is not a fight");

            var enemies = _content.Encounter(State.Act, State.NodeIndex, tier,
                                             RngFor(SaltEncounter, State.Act, State.NodeIndex));
            var outcome = RunBattle(placement, enemies);
            if (!outcome.Won)
            {
                State.Phase = RunPhase.Defeated;
                return outcome;
            }

            outcome.BaseIncome = _cfg.FightReward(State.Act, tier);
            State.Gold += outcome.SandEarned;
            AdvanceAfterBeat();
            return outcome;
        }

        /// <summary>
        /// Compatibility convenience for old automated drivers. New clients preview the full
        /// Interlude and call <see cref="ResolveInterlude"/> with the player's chosen path.
        /// </summary>
        public int ResolveEvent()
        {
            return ResolveInterlude(InterludePath.Treasury).Sand;
        }

        /// <summary>
        /// Deterministic, fully visible Interlude reward groups. Armory and Hourstone each expose
        /// up to three distinct content choices; Treasury is the fixed Sand alternative.
        /// </summary>
        public InterludePreview PreviewInterlude()
        {
            Require(State.Phase == RunPhase.Planning && !AtBoss, "not at an Interlude");
            Require(CurrentNodeKind == NodeKind.Event, "current beat is not an Interlude");

            var preview = new InterludePreview { TreasurySand = _cfg.InterludeTreasurySand };
            var rng = RngFor(SaltInterlude, State.Act, State.NodeIndex);

            var armory = new List<RewardOffer>();
            foreach (var id in _content.WeaponPool(State.Act))
                armory.Add(new RewardOffer { Path = InterludePath.Armory, Kind = OfferKind.Weapon, Id = id });
            foreach (var id in _content.TrinketPool(State.Act))
                armory.Add(new RewardOffer { Path = InterludePath.Armory, Kind = OfferKind.Trinket, Id = id });
            DrawDistinct(armory, preview.Armory, _cfg.RewardChoices, rng);

            var hourstone = new List<RewardOffer>();
            foreach (var id in _content.BannerPool(State.Act))
                if (!State.Banners.Contains(id))
                    hourstone.Add(new RewardOffer
                    {
                        Path = InterludePath.Hourstone,
                        Kind = OfferKind.Inscription,
                        Id = id,
                    });
            DrawDistinct(hourstone, preview.Hourstone, _cfg.RewardChoices, rng);
            return preview;
        }

        /// <summary>
        /// Take one visible Interlude path. For Armory/Hourstone, optionIndex selects the item
        /// from the corresponding preview group.
        /// </summary>
        public RewardOffer ResolveInterlude(InterludePath path, int optionIndex = 0)
        {
            var preview = PreviewInterlude();
            RewardOffer reward;
            switch (path)
            {
                case InterludePath.Treasury:
                    Require(optionIndex == 0, "Treasury has one fixed reward");
                    reward = new RewardOffer
                    {
                        Path = path,
                        Sand = preview.TreasurySand,
                    };
                    State.Gold += reward.Sand;
                    break;
                case InterludePath.Armory:
                    reward = At(preview.Armory, optionIndex, "Armory reward");
                    State.Inventory.Add(NewItem(
                        reward.Kind == OfferKind.Weapon ? ItemKind.Weapon : ItemKind.Trinket,
                        reward.Id, WeaponTier.Worn, 0));
                    break;
                case InterludePath.Hourstone:
                    reward = At(preview.Hourstone, optionIndex, "Hourstone reward");
                    State.Banners.Add(reward.Id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path));
            }

            State.UnlockedFieldSlots = Math.Min(_cfg.MaxFieldSlots,
                                                _cfg.StartingFieldSlots + State.Act);
            State.SlotOfferPending = State.FieldSlots < State.UnlockedFieldSlots;
            AdvanceAfterBeat();
            return reward;
        }

        /// <summary>
        /// The act boss — an AUTHORED encounter (ADR 0016). Win advances the act; lose and the
        /// run is over. The old ghost path (capture the player's board, mirror a ghost in,
        /// accumulate a best-of-5 record) is gone: PvP is Deferred and the snapshot seam stays
        /// unused rather than wired. Boss reward beyond base income is still an open question.
        /// </summary>
        public FightOutcome ResolveBoss(IReadOnlyList<Hex> placement)
        {
            Require(State.Phase == RunPhase.Planning && AtBoss, "not at the act boss");

            var enemies = _content.Boss(State.Act, RngFor(SaltEncounter, State.Act, _cfg.NodesPerAct));
            var outcome = RunBattle(placement, enemies);
            if (!outcome.Won) { State.BossLosses++; State.Phase = RunPhase.Defeated; return outcome; }
            State.BossWins++;

            if (State.Act == _cfg.Acts)
            {
                State.Phase = RunPhase.Complete;
                return outcome;
            }

            outcome.BaseIncome = _cfg.BossReward(State.Act);
            State.Gold += outcome.SandEarned;
            State.PendingBossSand = outcome.SandEarned;
            State.PendingBossRewards = PreviewBossRewardIds();
            if (State.PendingBossRewards.Count == 0)
                AdvanceToNextAct();
            else
                State.Phase = RunPhase.Reward;
            return outcome;
        }

        public IReadOnlyList<string> PreviewBossRewards()
        {
            Require(State.Phase == RunPhase.Reward, "no boss reward pending");
            return State.PendingBossRewards;
        }

        public void ChooseBossReward(int index)
        {
            Require(State.Phase == RunPhase.Reward, "no boss reward pending");
            string id = At(State.PendingBossRewards, index, "boss reward");
            State.Banners.Add(id);
            State.PendingBossRewards.Clear();
            State.PendingBossSand = 0;
            AdvanceToNextAct();
        }

        /// <summary>
        /// The enemies the current node WILL field, without resolving it. Every encounter is
        /// previewed before deployment (pve-encounters law), and the preview has to be the same
        /// comp the fight uses — which only this class can guarantee, because the rng derivation
        /// is stateless-by-salt and private. Callers outside would have to guess it and would
        /// silently show the player a different army than they are about to fight.
        /// </summary>
        public List<(UnitDef Def, Hex Pos)> PreviewEnemies(FightTier tier)
        {
            Require(State.Phase == RunPhase.Planning, "no encounter to preview outside Planning");
            Require(CurrentNodeKind != NodeKind.Event, "events field no enemies");
            return AtBoss
                ? _content.Boss(State.Act, RngFor(SaltEncounter, State.Act, _cfg.NodesPerAct))
                : _content.Encounter(State.Act, State.NodeIndex, tier,
                                     RngFor(SaltEncounter, State.Act, State.NodeIndex));
        }

        /// <summary>
        /// The rule the player is owed before locking deployment (pve-encounters.md: "no surprise
        /// mechanics after Play"). Derived from the same private salt as <see cref="PreviewEnemies"/>
        /// for the same reason — a brief describing a different encounter than the one that spawns
        /// would be worse than no brief at all.
        /// </summary>
        public EncounterBrief PreviewBrief(FightTier tier)
        {
            Require(State.Phase == RunPhase.Planning, "no encounter to preview outside Planning");
            Require(CurrentNodeKind != NodeKind.Event, "events field no enemies");
            return AtBoss
                ? _content.BossBrief(State.Act, RngFor(SaltEncounter, State.Act, _cfg.NodesPerAct))
                : _content.EncounterBrief(State.Act, State.NodeIndex, tier,
                                          RngFor(SaltEncounter, State.Act, State.NodeIndex));
        }

        // ---- Shop phase (ADR 0006 cadence, ADR 0009 stock) --------------------------

        /// <summary>Buy the offer at a slot. Hero card of an owned chassis = dupe: rank-up
        /// fires and the 1-of-2 spec choice must be resolved before any other shop action.</summary>
        public PurchaseResult BuyOffer(int index)
        {
            RequireShopActionable();
            var offer = OfferAt(index);
            Require(State.Gold >= offer.Price, "not enough Sand");
            var result = new PurchaseResult
            {
                OfferIndex = index,
                OfferKind = offer.Kind,
                ContentId = offer.Id,
                SandSpent = offer.Price,
            };
            switch (offer.Kind)
            {
                case OfferKind.Hero:
                    BuyHero(offer, result);
                    break;
                case OfferKind.Weapon:
                {
                    ItemRef item = NewItem(ItemKind.Weapon, offer.Id, offer.Tier, offer.Price);
                    State.Inventory.Add(item);
                    result.Outcome = PurchaseOutcome.Weapon;
                    result.ItemInstanceId = item.InstanceId;
                    break;
                }
                case OfferKind.Trinket:
                {
                    ItemRef item = NewItem(ItemKind.Trinket, offer.Id, WeaponTier.Worn, offer.Price);
                    State.Inventory.Add(item);
                    result.Outcome = PurchaseOutcome.Trinket;
                    result.ItemInstanceId = item.InstanceId;
                    break;
                }
                case OfferKind.Inscription:
                    State.Banners.Add(offer.Id);
                    result.Outcome = PurchaseOutcome.Inscription;
                    break;
            }
            State.Gold -= offer.Price;
            State.ShopOffers[index] = null;
            return result;
        }

        public void Reroll()
        {
            RequireShopActionable();
            Require(State.Gold >= _cfg.RerollCost, "not enough Sand to reroll");
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
            Require(State.Phase == RunPhase.Planning && State.PendingSpec != null,
                    "no spec choice pending");
            var p = State.PendingSpec!;
            var hero = Zone(p.Zone)[p.Index];
            string chosen = which == 0 ? p.OptionA : p.OptionB;
            hero.SpecNodeIds.Add(chosen);
            if (p.ForRank == _content.ForkRank(hero.ChassisId))
                hero.PathId = chosen;                        // the fork sets the path (B, or A for late-bloomers)
            State.PendingSpec = null;
        }

        public void SellHero(RosterZone zone, int index)
        {
            RequireShopActionable();
            var list = Zone(zone);
            var hero = list[index];
            UnequipWeapon(zone, index);
            while (hero.TrinketIds.Count > 0) UnequipTrinket(zone, index);
            State.Gold += (hero.GoldSpent + hero.StarterWeaponSandInvested) *
                          _cfg.SellPct / 100;
            list.RemoveAt(index);
        }

        public void SellItem(int invIndex)
        {
            RequireShopActionable();
            var item = State.Inventory[invIndex];
            State.Gold += item.SandInvested * _cfg.SellPct / 100;
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
                State.Inventory.Add(new ItemRef
                {
                    InstanceId = hero.WeaponInstanceId,
                    Kind = ItemKind.Weapon,
                    Id = hero.WeaponId,
                    Tier = hero.WeaponTier,
                    SandInvested = hero.WeaponSandInvested,
                });
            else
            {
                hero.StarterWeaponTier = hero.WeaponTier;
                hero.StarterWeaponSandInvested = hero.WeaponSandInvested;
            }
            hero.WeaponId = item.Id;
            hero.WeaponTier = item.Tier;
            hero.WeaponInstanceId = item.InstanceId;
            hero.WeaponSandInvested = item.SandInvested;
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
                State.Inventory.Add(new ItemRef
                {
                    InstanceId = hero.TrinketInstanceId,
                    Kind = ItemKind.Trinket,
                    Id = hero.TrinketIds[0],
                    SandInvested = hero.TrinketSandInvested,
                });
                hero.TrinketIds.Clear();
            }
            hero.TrinketIds.Add(item.Id);
            hero.TrinketInstanceId = item.InstanceId;
            hero.TrinketSandInvested = item.SandInvested;
        }

        public void UnequipWeapon(RosterZone zone, int index)
        {
            RequireShopActionable();
            var hero = Zone(zone)[index];
            if (hero.WeaponId == null) return;               // starter isn't an item
            State.Inventory.Add(new ItemRef
            {
                InstanceId = hero.WeaponInstanceId,
                Kind = ItemKind.Weapon,
                Id = hero.WeaponId,
                Tier = hero.WeaponTier,
                SandInvested = hero.WeaponSandInvested,
            });
            hero.WeaponId = null;
            hero.WeaponTier = hero.StarterWeaponTier;
            hero.WeaponInstanceId = 0;
            hero.WeaponSandInvested = hero.StarterWeaponSandInvested;
        }

        /// <summary>The Tower's forge (ADR 0015): pay gold, raise the held weapon —
        /// starter included — one temper tier, capped at the act's stock ceiling.</summary>
        public ReforgeResult Reforge(RosterZone zone, int index)
        {
            RequireShopActionable();
            var hero = Zone(zone)[index];
            Require(hero.WeaponTier < _cfg.TierCeiling(State.Act), "the forge follows the front");
            WeaponTier previous = hero.WeaponTier;
            int cost = _cfg.ReforgeCosts[(int)hero.WeaponTier];
            Require(State.Gold >= cost, "not enough Sand to reforge");
            State.Gold -= cost;
            hero.WeaponTier = hero.WeaponTier + 1;
            hero.WeaponSandInvested += cost;
            if (hero.WeaponId == null)
            {
                hero.StarterWeaponTier = hero.WeaponTier;
                hero.StarterWeaponSandInvested = hero.WeaponSandInvested;
            }
            return new ReforgeResult
            {
                Zone = zone,
                HeroIndex = index,
                WeaponId = hero.WeaponId ?? _content.Chassis(hero.ChassisId).StarterWeapon.Name,
                ItemInstanceId = hero.WeaponInstanceId,
                IsStarter = hero.WeaponId == null,
                PreviousTier = previous,
                NewTier = hero.WeaponTier,
                SandSpent = cost,
                TotalWeaponInvestment = hero.WeaponSandInvested,
            };
        }

        public void UnequipTrinket(RosterZone zone, int index)
        {
            RequireShopActionable();
            var hero = Zone(zone)[index];
            if (hero.TrinketIds.Count == 0) return;
            State.Inventory.Add(new ItemRef
            {
                InstanceId = hero.TrinketInstanceId,
                Kind = ItemKind.Trinket,
                Id = hero.TrinketIds[0],
                SandInvested = hero.TrinketSandInvested,
            });
            hero.TrinketIds.RemoveAt(0);
            hero.TrinketInstanceId = 0;
            hero.TrinketSandInvested = 0;
        }

        public bool SlotOfferOpen =>
            State.Phase == RunPhase.Planning &&
            State.SlotOfferPending &&
            State.FieldSlots < State.UnlockedFieldSlots;

        public int SlotOfferCost
        {
            get { Require(SlotOfferOpen, "no slot offer open"); return _cfg.SlotCost(State.SlotsBought); }
        }

        public void BuySlot()
        {
            RequireShopActionable();
            Require(SlotOfferOpen, "no slot offer open");
            int cost = _cfg.SlotCost(State.SlotsBought);
            Require(State.Gold >= cost, "not enough Sand for the slot");
            State.Gold -= cost;
            State.FieldSlots++;
            State.SlotsBought++;
            State.SlotOfferPending = State.FieldSlots < State.UnlockedFieldSlots;
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

        /// <summary>
        /// Compatibility no-op. The old shell used a blocking Shop screen and called LeaveShop to
        /// advance. The unified Planning workspace advances on reward settlement instead.
        /// </summary>
        public void LeaveShop()
        {
            Require(State.Phase == RunPhase.Planning, "not in Planning");
            Require(State.PendingSpec == null, "resolve the spec choice before leaving");
        }

        // ---- Internals -------------------------------------------------------------

        private void AdvanceAfterBeat()
        {
            State.NodeIndex++;
            State.Phase = RunPhase.Planning;
            GenerateShop();
        }

        private void AdvanceToNextAct()
        {
            State.Act++;
            State.NodeIndex = 0;
            State.Phase = RunPhase.Planning;
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
            // Draw one typed Workshop category using the explicit 45/35/20 weights. Empty
            // categories fall through in stable order without introducing a second random draw.
            var weapons = _content.WeaponPool(State.Act);
            var trinkets = _content.TrinketPool(State.Act);
            var inscriptions = new List<string>();
            foreach (var id in _content.BannerPool(State.Act))
                if (!State.Banners.Contains(id)) inscriptions.Add(id);

            int category = rng.Next(100);
            if (category < _cfg.WeaponChancePct && weapons.Count > 0)
                return RollWeapon(rng, weapons);
            if (category < _cfg.WeaponChancePct + _cfg.TrinketChancePct && trinkets.Count > 0)
                return new ShopOffer
                {
                    Kind = OfferKind.Trinket, Id = trinkets[rng.Next(trinkets.Count)],
                    Price = _cfg.TrinketPrice,
                };
            if (inscriptions.Count > 0)
                return new ShopOffer
                {
                    Kind = OfferKind.Inscription,
                    Id = inscriptions[rng.Next(inscriptions.Count)],
                    Price = _cfg.InscriptionPrice,
                };
            if (weapons.Count > 0)
                return RollWeapon(rng, weapons);
            if (trinkets.Count > 0)
                return new ShopOffer
                {
                    Kind = OfferKind.Trinket, Id = trinkets[rng.Next(trinkets.Count)],
                    Price = _cfg.TrinketPrice,
                };
            return null;
        }

        private ShopOffer RollWeapon(Rng rng, IReadOnlyList<string> weapons)
        {
            // Temper rolls uniformly up to the act ceiling (ADR 0015 act-gated stock).
            var tier = (WeaponTier)rng.Next((int)_cfg.TierCeiling(State.Act) + 1);
            int price = _cfg.WeaponPrice + (int)tier * _cfg.ReforgeCosts[0];
            return new ShopOffer
            {
                Kind = OfferKind.Weapon,
                Id = weapons[rng.Next(weapons.Count)],
                Price = price,
                Tier = tier,
            };
        }

        private void BuyHero(ShopOffer offer, PurchaseResult result)
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
                        result.Outcome = PurchaseOutcome.RankUp;
                        result.PreviousRank = hero.Rank;
                        hero.Rank++;
                        result.NewRank = hero.Rank;
                        hero.GoldSpent += offer.Price;
                        var (a, b) = _content.SpecOptions(hero.ChassisId, hero.Rank, hero.PathId);
                        State.PendingSpec = new PendingSpec
                        {
                            Zone = zone, Index = i, ForRank = hero.Rank, OptionA = a, OptionB = b,
                        };
                        result.PendingOptionA = a;
                        result.PendingOptionB = b;
                        return;
                    }
            }
            var recruit = new HeroInstance { ChassisId = offer.Id, GoldSpent = offer.Price };
            if (State.Field.Count < State.FieldSlots) State.Field.Add(recruit);
            else if (State.Bench.Count < _cfg.BenchSlots) State.Bench.Add(recruit);
            else throw new InvalidOperationException("no room — field and bench are full");
            result.Outcome = PurchaseOutcome.Recruit;
            result.NewRank = Rank.C;
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

        public int IndexOfItem(long instanceId) =>
            State.Inventory.FindIndex(item => item.InstanceId == instanceId);

        private ItemRef NewItem(ItemKind kind, string id, WeaponTier tier, int sandInvested) =>
            new ItemRef
            {
                InstanceId = State.NextItemInstanceId++,
                Kind = kind,
                Id = id,
                Tier = tier,
                SandInvested = sandInvested,
            };

        private List<string> PreviewBossRewardIds()
        {
            var source = new List<string>();
            foreach (var id in _content.BannerPool(State.Act))
                if (!State.Banners.Contains(id))
                    source.Add(id);
            var rewards = new List<string>();
            DrawDistinct(source, rewards, _cfg.RewardChoices,
                         RngFor(SaltBossReward, State.Act, _cfg.NodesPerAct));
            return rewards;
        }

        private void RequireShopActionable()
        {
            Require(State.Phase == RunPhase.Planning, "only available in Planning");
            Require(State.PendingSpec == null, "resolve the pending spec choice first");
        }

        private FightOutcome RunBattle(IReadOnlyList<Hex> placement,
                                       List<(UnitDef Def, Hex Pos)> enemies,
                                       List<string>? enemyBanners = null,
                                       int enemyBannerCopies = 1)
        {
            ValidatePlacement(placement);
            var teamTriggers = new List<(int Team, Trigger T)>();
            // Bearer of the Mark (ADR 0015 dive law): a fielded bearer doubles the
            // warband's banner effects. Player side only for now — ghost bearers noted
            // as a fairness follow-up.
            int bannerCopies = 1;
            foreach (var hero in State.Field)
                foreach (var nodeId in hero.SpecNodeIds)
                    if (_content.Node(nodeId).DoublesBanners) { bannerCopies = 2; break; }
            for (int c = 0; c < bannerCopies; c++)
                foreach (var id in State.Banners)
                    foreach (var t in _content.Banner(id).TeamTriggers)
                        teamTriggers.Add((0, t));
            if (enemyBanners != null)
                for (int c = 0; c < enemyBannerCopies; c++)      // ghost bearers double too (fairness)
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
                BaseIncome = 0,
                KillPayout = 0,
                WinBonus = 0,
                Battle = result,
            };
        }

        private ComposedLoadout Compose(HeroInstance hero)
        {
            var chassis = _content.Chassis(hero.ChassisId);
            var weapon = hero.WeaponId == null ? chassis.StarterWeapon : _content.Weapon(hero.WeaponId);
            return Loadout.Compose(
                chassis,
                weapon,
                hero.TrinketIds.Select(_content.Trinket),
                hero.SpecNodeIds.Select(_content.Node),
                tier: hero.WeaponTier,
                mastered: chassis.Specializations.Contains(weapon.Category),
                rankSteps: (int)hero.Rank);
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
                var nodes = new NodeKind[_cfg.NodesPerAct];
                nodes[_cfg.InterludeNodeIndex] = NodeKind.Event;
                maps[act - 1] = nodes;
            }
            return maps;
        }

        private void ValidateConfig()
        {
            if (_cfg.Acts < 1 || _cfg.FightRewardsByAct.Length != _cfg.Acts ||
                _cfg.BossSandByAct.Length != _cfg.Acts)
                throw new ArgumentException("economy tables must contain exactly one row per act");
            if (_cfg.NodesPerAct != 4 || _cfg.EventsPerAct != 1 ||
                _cfg.InterludeNodeIndex < 0 || _cfg.InterludeNodeIndex >= _cfg.NodesPerAct)
                throw new ArgumentException("an act must be Fight, Fight, Interlude, Fight, Boss");
            if (_cfg.StartingFieldSlots + _cfg.Acts != _cfg.MaxFieldSlots)
                throw new ArgumentException("one Interlude capacity unlock per act must reach the field cap");
            if (_cfg.SlotCosts.Length != _cfg.MaxFieldSlots - _cfg.StartingFieldSlots)
                throw new ArgumentException("slot cost table must cover every field expansion");
            if (_cfg.WeaponChancePct + _cfg.TrinketChancePct + _cfg.InscriptionChancePct != 100)
                throw new ArgumentException("Workshop category chances must total 100");
            foreach (var rewards in _cfg.FightRewardsByAct)
                if (rewards.Length != 3)
                    throw new ArgumentException("each act needs Stable, Fraying, and Collapsing rewards");
        }

        private static T At<T>(IReadOnlyList<T> list, int index, string label)
        {
            if (index < 0 || index >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"{label} index is out of range");
            return list[index];
        }

        private static void DrawDistinct<T>(List<T> source, List<T> destination, int count, Rng rng)
        {
            while (destination.Count < count && source.Count > 0)
            {
                int index = rng.Next(source.Count);
                destination.Add(source[index]);
                source.RemoveAt(index);
            }
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
