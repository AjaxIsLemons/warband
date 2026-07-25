using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Pins the player-facing run contract that the unified Planning workspace renders.
    /// These tests deliberately describe visible choices rather than internal phase plumbing.
    /// </summary>
    public class RunLoopUxContractTests
    {
        private static RunController NewRun(StubContent? content = null) =>
            new RunController(73, content ?? RichContent(), Kit.Warband());

        private static StubContent RichContent() => new StubContent
        {
            Weapons = new List<string> { "blade", "bow", "staff", "spear" },
            Trinkets = new List<string> { "charm", "lens", "ward" },
            Banners = new List<string> { "warbanner", "tempo", "field", "mana" },
        };

        private static void ReachInterlude(RunController run)
        {
            while (run.CurrentNodeKind != NodeKind.Event)
                run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run));
        }

        private static void ReachBoss(RunController run)
        {
            while (!run.AtBoss)
            {
                if (run.CurrentNodeKind == NodeKind.Event)
                    run.ResolveInterlude(InterludePath.Treasury);
                else
                    run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run));
            }
        }

        [Fact]
        public void NewRunOpensInPlanningWithSandAndFiveVisibleOffers()
        {
            var run = NewRun();
            var cfg = new RunConfig();

            Assert.Equal(RunPhase.Planning, run.State.Phase);
            Assert.Equal(cfg.StartingSand, run.State.Sand);
            Assert.Equal(cfg.HeroSlots + cfg.ItemSlots, run.State.ShopOffers.Count);
            Assert.Equal(new[] { NodeKind.Fight, NodeKind.Fight, NodeKind.Event, NodeKind.Fight },
                         run.State.ActMaps[0]);
        }

        [Fact]
        public void InterludePreviewIsDeterministicGroupedAndDistinct()
        {
            var run = NewRun();
            ReachInterlude(run);

            var a = run.PreviewInterlude();
            var b = run.PreviewInterlude();

            Assert.Equal(5, a.TreasurySand);
            Assert.Equal(3, a.Armory.Count);
            Assert.Equal(3, a.Hourstone.Count);
            Assert.Equal(3, a.Armory.Select(x => (x.Kind, x.Id)).Distinct().Count());
            Assert.Equal(3, a.Hourstone.Select(x => x.Id).Distinct().Count());
            Assert.Equal(a.Armory.Select(x => (x.Kind, x.Id)),
                         b.Armory.Select(x => (x.Kind, x.Id)));
            Assert.Equal(a.Hourstone.Select(x => x.Id), b.Hourstone.Select(x => x.Id));
        }

        [Fact]
        public void ArmoryAndHourstonePathsGrantThePreviewedChoice()
        {
            var armoryRun = NewRun();
            ReachInterlude(armoryRun);
            var armoryPreview = armoryRun.PreviewInterlude();
            var item = armoryRun.ResolveInterlude(InterludePath.Armory, 1);
            Assert.Equal(armoryPreview.Armory[1].Id, item.Id);
            Assert.Contains(armoryRun.State.Inventory, x => x.Id == item.Id);

            var hourstoneRun = NewRun();
            ReachInterlude(hourstoneRun);
            var hourstonePreview = hourstoneRun.PreviewInterlude();
            var inscription = hourstoneRun.ResolveInterlude(InterludePath.Hourstone, 2);
            Assert.Equal(hourstonePreview.Hourstone[2].Id, inscription.Id);
            Assert.Contains(inscription.Id, hourstoneRun.State.Inscriptions);
        }

        [Fact]
        public void NonFinalBossPresentsThreeBlockingInscriptionRewards()
        {
            var run = NewRun();
            ReachBoss(run);

            var outcome = run.ResolveBoss(Kit.AutoPlace(run));

            Assert.True(outcome.Won);
            Assert.Equal(new RunConfig().BossReward(1), outcome.SandEarned);
            Assert.Equal(RunPhase.Reward, run.State.Phase);
            Assert.Equal(3, run.PreviewBossRewards().Count);
            Assert.Throws<System.InvalidOperationException>(() => run.Reroll());

            string chosen = run.PreviewBossRewards()[1];
            run.ChooseBossReward(1);
            Assert.Contains(chosen, run.State.Inscriptions);
            Assert.Equal(2, run.State.Act);
            Assert.Equal(0, run.State.NodeIndex);
            Assert.Equal(RunPhase.Planning, run.State.Phase);
        }

        [Fact]
        public void WorkshopAlwaysUsesThreeRecruitAndTwoTypedMixedSlots()
        {
            var run = NewRun();
            var cfg = new RunConfig();

            Assert.All(run.State.ShopOffers.Take(3),
                       offer => Assert.Equal(OfferKind.Hero, offer!.Kind));
            Assert.All(run.State.ShopOffers.Skip(3),
                       offer => Assert.Contains(offer!.Kind,
                           new[] { OfferKind.Weapon, OfferKind.Trinket, OfferKind.Inscription }));
            Assert.Equal(100, cfg.WeaponChancePct + cfg.TrinketChancePct +
                              cfg.InscriptionChancePct);
        }
    }
}
