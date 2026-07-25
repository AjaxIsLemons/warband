using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class ShopTests
    {
        private static RunController ShopRun(ulong seed = 7, StubContent? content = null,
                                             RunConfig? cfg = null, int gold = 50)
        {
            var run = new RunController(seed, content ?? new StubContent(), Kit.Warband(), cfg);
            switch (run.CurrentNodeKind)
            {
                case NodeKind.Fight: run.ResolveFight(FightTier.Even, Kit.AutoPlace(run)); break;
                case NodeKind.Event: run.ResolveEvent(); break;
            }
            run.State.Gold = gold;                // tests rig the wallet; income is tested elsewhere
            return run;
        }

        private static int SlotOf(RunController run, OfferKind kind) =>
            run.State.ShopOffers.ToList().FindIndex(o => o?.Kind == kind);

        [Fact]
        public void ShopRollsHeroAndItemSlotsByLayout()
        {
            var run = ShopRun();
            var cfg = new RunConfig();
            Assert.Equal(cfg.HeroSlots + cfg.ItemSlots, run.State.ShopOffers.Count);
            for (int i = 0; i < cfg.HeroSlots; i++)
                Assert.Equal(OfferKind.Hero, run.State.ShopOffers[i]!.Kind);
            for (int i = cfg.HeroSlots; i < cfg.HeroSlots + cfg.ItemSlots; i++)
                Assert.NotEqual(OfferKind.Hero, run.State.ShopOffers[i]!.Kind);
        }

        [Fact]
        public void ShopGenerationIsSeedDeterministic()
        {
            var a = ShopRun(seed: 12);
            var b = ShopRun(seed: 12);
            Assert.Equal(a.State.ShopOffers.Select(o => o?.Id), b.State.ShopOffers.Select(o => o?.Id));
        }

        [Fact]
        public void RerollChargesFlatCostAndKeepsFrozenOffers()
        {
            var run = ShopRun();
            var cfg = new RunConfig();
            run.ToggleFreeze(0);
            string frozenId = run.State.ShopOffers[0]!.Id;
            int gold = run.State.Gold;
            run.Reroll();
            Assert.Equal(gold - cfg.RerollCost, run.State.Gold);
            Assert.Equal(frozenId, run.State.ShopOffers[0]!.Id);
            Assert.True(run.State.ShopOffers[0]!.Frozen);
        }

        [Fact]
        public void FrozenOfferSurvivesIntoTheNextNodesShop()
        {
            var run = ShopRun();
            run.ToggleFreeze(1);
            string frozenId = run.State.ShopOffers[1]!.Id;
            switch (run.CurrentNodeKind)
            {
                case NodeKind.Fight: run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run)); break;
                case NodeKind.Event: run.ResolveEvent(); break;
            }
            Assert.Equal(frozenId, run.State.ShopOffers[1]!.Id);
            Assert.True(run.State.ShopOffers[1]!.Frozen);
        }

        [Fact]
        public void BuyingNewHeroesFillsFieldThenBenchThenRejects()
        {
            // Field starts full (3/3), so new chassis land on the bench until it jams.
            var run = ShopRun();
            int bought = 0;
            var owned = new HashSet<string> { "hero0", "hero1", "hero2" };
            while (bought < 2)                    // bench holds 2 — fill it
            {
                int slot = run.State.ShopOffers.ToList()
                    .FindIndex(o => o?.Kind == OfferKind.Hero && !owned.Contains(o.Id));
                if (slot < 0) { run.Reroll(); continue; }
                owned.Add(run.State.ShopOffers[slot]!.Id);
                run.BuyOffer(slot);
                bought++;
            }
            Assert.Equal(3, run.State.Field.Count);       // field was full at 3 slots
            Assert.Equal(2, run.State.Bench.Count);
            int next = -1;
            while ((next = run.State.ShopOffers.ToList()
                       .FindIndex(o => o?.Kind == OfferKind.Hero && !owned.Contains(o.Id))) < 0)
                run.Reroll();
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(next));
        }

        [Fact]
        public void BuyingOwnedChassisRanksUpThroughForkChoices()
        {
            var content = new StubContent { Heroes = new List<string> { "hero0" } };
            var run = ShopRun(content: content);
            var hero = run.State.Field.First(h => h.ChassisId == "hero0");

            run.BuyOffer(0);                      // every hero card is hero0 — a dupe
            Assert.Equal(Rank.B, hero.Rank);
            Assert.NotNull(run.State.PendingSpec);
            Assert.Throws<InvalidOperationException>(() => run.LeaveShop());
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(1));
            run.ChooseSpec(0);
            Assert.Equal("hero0-B-a", hero.PathId);       // the fork set the path
            Assert.Contains("hero0-B-a", hero.SpecNodeIds);

            run.BuyOffer(1);
            Assert.Equal(Rank.A, hero.Rank);
            run.ChooseSpec(1);
            Assert.Contains("hero0-B-a-A-b", hero.SpecNodeIds);  // A options scoped by path

            run.BuyOffer(2);
            Assert.Equal(Rank.S, hero.Rank);
            run.ChooseSpec(0);
            Assert.Equal(3 * new RunConfig().HeroPrice, hero.GoldSpent);

            // hero0 is S and it's the only chassis: hero slots must now roll empty.
            run.Reroll();
            for (int i = 0; i < new RunConfig().HeroSlots; i++)
                Assert.Null(run.State.ShopOffers[i]);
        }

        [Fact]
        public void ItemsBuyIntoInventoryAndEquipWithSlotRules()
        {
            var content = new StubContent { Banners = new List<string>() };  // no banner rolls
            var run = ShopRun(content: content);
            int slot;
            while ((slot = SlotOf(run, OfferKind.Weapon)) < 0) run.Reroll();
            string weaponId = run.State.ShopOffers[slot]!.Id;
            run.BuyOffer(slot);
            Assert.Single(run.State.Inventory);

            run.EquipWeapon(RosterZone.Field, 0, 0);
            Assert.Equal(weaponId, run.State.Field[0].WeaponId);
            Assert.Empty(run.State.Inventory);

            run.UnequipWeapon(RosterZone.Field, 0);
            Assert.Null(run.State.Field[0].WeaponId);     // back on the starter
            Assert.Single(run.State.Inventory);

            while ((slot = SlotOf(run, OfferKind.Trinket)) < 0) run.Reroll();
            run.BuyOffer(slot);
            int trinketInv = run.State.Inventory.FindIndex(x => x.Kind == ItemKind.Trinket);
            run.EquipTrinket(RosterZone.Field, 0, trinketInv);
            Assert.Single(run.State.Field[0].TrinketIds);
        }

        [Fact]
        public void SellingRefundsHalf()
        {
            var content = new StubContent { Heroes = new List<string> { "hero0" }, Banners = new List<string>() };
            var run = ShopRun(content: content);
            var cfg = new RunConfig();
            var hero = run.State.Field.First(h => h.ChassisId == "hero0");

            run.BuyOffer(0); run.ChooseSpec(0);           // dupe: GoldSpent = HeroPrice
            int gold = run.State.Gold;
            int heroIdx = run.State.Field.IndexOf(hero);
            run.SellHero(RosterZone.Field, heroIdx);
            Assert.Equal(gold + cfg.HeroPrice * cfg.SellPct / 100, run.State.Gold);
            Assert.DoesNotContain(hero, run.State.Field);

            int slot;
            while ((slot = SlotOf(run, OfferKind.Weapon)) < 0) run.Reroll();
            run.BuyOffer(slot);
            gold = run.State.Gold;
            run.SellItem(0);
            Assert.Equal(gold + cfg.WeaponPrice * cfg.SellPct / 100, run.State.Gold);
            Assert.Empty(run.State.Inventory);
        }

        [Fact]
        public void SoldHeroReturnsItsGearToInventory()
        {
            var content = new StubContent { Banners = new List<string>() };
            var run = ShopRun(content: content);
            int slot;
            while ((slot = SlotOf(run, OfferKind.Weapon)) < 0) run.Reroll();
            run.BuyOffer(slot);
            run.EquipWeapon(RosterZone.Field, 0, 0);
            run.SellHero(RosterZone.Field, 0);
            Assert.Single(run.State.Inventory);           // the weapon came back
            Assert.Equal(2, run.State.Field.Count);
        }

        [Fact]
        public void InscriptionBuysApplyToBattle()
        {
            var cfg = new RunConfig { BannerChancePct = 100 };
            var run = ShopRun(cfg: cfg);
            int slot;
            while ((slot = SlotOf(run, OfferKind.Inscription)) < 0) run.Reroll();
            run.BuyOffer(slot);
            Assert.Contains("warbanner", run.State.Banners);

            FightOutcome? fight = null;
            while (run.State.Phase != RunPhase.Complete && fight == null)
            {
                if (run.CurrentNodeKind == NodeKind.Fight)
                    fight = run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
                else if (run.CurrentNodeKind == NodeKind.Event) run.ResolveEvent();
                else run.ResolveBoss(Kit.AutoPlace(run));
            }
            // BattleStart banner: every player unit shielded on tick 0.
            Assert.Contains(fight!.Battle.Events,
                e => e.Kind == EventKind.ShieldChanged && e.Target == 0 && e.Tick == 0);

        }


        [Fact]
        public void MarketActionsRequireALivePlanningRun()
        {
            var run = new RunController(7, new StubContent(), Kit.Warband());
            run.State.Phase = RunPhase.Defeated;
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(0));
            Assert.Throws<InvalidOperationException>(() => run.Reroll());
            Assert.Throws<InvalidOperationException>(() => run.SellItem(0));
        }
    }
}
