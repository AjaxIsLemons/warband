using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The deployment board draws muster rings off <see cref="MechanicalRulePresenter.Musters"/>,
    /// so this is the contract that keeps the ring honest: if a kit's reach changes and the query
    /// does not follow, the board would keep promising hexes the rule no longer touches.
    /// </summary>
    public class MusterQueryTests
    {
        private static IReadOnlyList<MechanicalRulePresenter.Muster> Musters(
            string chassis, params string[] nodes) =>
            MechanicalRulePresenter.Musters(
                Loadout.Compose(Kits.Chassis[chassis], nodes: nodes.Select(n => Kits.Nodes[n])).Def);

        [Fact]
        public void EveryAuthoredMusterIsFound()
        {
            // The three chassis that make deployment matter today. If a fourth is authored, this
            // test does not fail — the board simply starts drawing it, which is the point.
            Assert.Equal(new[] { 2 }, Musters("cleric").Select(m => m.Radius));          // Mercy Aura
            Assert.Equal(new[] { 1 }, Musters("banneret").Select(m => m.Radius));        // the Company
            Assert.Equal(new[] { 1 },
                Musters("phalanx", "phalanx.pikewall", "phalanx.pikewall.unbrokenline")
                    .Select(m => m.Radius));                                             // Unbroken Line
        }

        [Fact]
        public void AKitWithNoPlacementPromiseReportsNoMuster()
        {
            Assert.Empty(Musters("berserker"));
            Assert.Empty(Musters("shade"));
        }

        [Fact]
        public void WideBannerReportsBothRungsInnermostFirst()
        {
            var musters = Musters("banneret", "banneret.herald", "banneret.herald.widebanner");
            Assert.Equal(new[] { 1, 2 }, musters.Select(m => m.Radius));
        }

        /// <summary>"Everyone" is a different promise from "a very large ring" — the board must not
        /// draw Last March as a 99-hex circle that happens to cover the map.</summary>
        [Fact]
        public void LastMarchIsUnboundedAndSortsLast()
        {
            var musters = Musters("banneret", "banneret.warcaller", "banneret.warcaller.lastmarch");
            Assert.Equal(MechanicalRulePresenter.Muster.Unbounded, musters[musters.Count - 1].Radius);
            Assert.True(musters[musters.Count - 1].IsUnbounded);
            Assert.Contains(musters, m => m.Radius == 1);
        }

        /// <summary>A cast-time effect is not a muster: Rally reaches the Company at any range, but
        /// standing anywhere at deploy time buys you nothing unless the BattleStart rule caught you.</summary>
        [Fact]
        public void CastTimeAllyEffectsAreNotMusters()
        {
            var def = Loadout.Compose(Kits.Chassis["banneret"],
                nodes: new[] { Kits.Nodes["banneret.warcaller"], Kits.Nodes["banneret.warcaller.drumbeat"] }).Def;

            Assert.Single(MechanicalRulePresenter.Musters(def));   // the r1 muster, not Drumbeat
            Assert.Equal(1, MechanicalRulePresenter.Musters(def)[0].Radius);
        }

        /// <summary>The ring's tooltip is the same grammar as the card — no enum names leaking.</summary>

        // ---- seats: what the deployment board actually paints ----

        private static IReadOnlyList<Hex> Seats(string chassis, Hex at, params string[] nodes) =>
            MechanicalRulePresenter.MusterSeats(
                Loadout.Compose(Kits.Chassis[chassis], nodes: nodes.Select(n => Kits.Nodes[n])).Def, at);

        [Fact]
        public void SeatsAreTheWholeRingWhenItFitsInsideThePlayerHalf()
        {
            // Row 1, mid-board: a radius-1 muster has all six neighbours plus its own hex.
            Assert.Equal(7, Seats("banneret", Hex.FromRowCol(1, 2)).Count);
        }

        [Fact]
        public void SeatsNeverIncludeAHexAHeroCannotBeDeployedOn()
        {
            foreach (var at in new[] { Hex.FromRowCol(0, 0), Hex.FromRowCol(3, 5), Hex.FromRowCol(3, 0) })
                foreach (var h in Seats("cleric", at))
                    Assert.True(RunController.IsDeployable(h), $"{h} is not a legal deployment hex");
        }

        /// <summary>The front rank is the interesting case: half a Banneret's ring is in the enemy
        /// half, and a board that lit those hexes would be inviting a placement lock-in refuses.</summary>
        [Fact]
        public void AFrontRankMusterOffersOnlyTheSeatsBehindIt()
        {
            var seats = Seats("banneret", Hex.FromRowCol(3, 2));
            Assert.NotEmpty(seats);
            Assert.All(seats, h => Assert.True(h.Row < Battle.BoardRows / 2));
            Assert.True(seats.Count < 7, "the ring should have lost its forward hexes to the clip");
        }

        [Fact]
        public void AKitWithNoMusterOffersNoSeats()
        {
            Assert.Empty(Seats("berserker", Hex.FromRowCol(1, 2)));
        }

        /// <summary>Last March sweeps in the whole warband, so there is nowhere to stand that is
        /// outside it — a ring would be a lie in both directions.</summary>
        [Fact]
        public void AnUnboundedMusterOffersNoRing()
        {
            Assert.Empty(Seats("banneret", Hex.FromRowCol(1, 2),
                               "banneret.warcaller", "banneret.warcaller.lastmarch"));
        }

        /// <summary>Wide Banner paints the OUTER rung only — one ring per hero, not a diagram.</summary>
        [Fact]
        public void WideBannerPaintsTheOuterRungOnly()
        {
            var wide = Seats("banneret", Hex.FromRowCol(1, 2),
                             "banneret.herald", "banneret.herald.widebanner");
            Assert.Equal(Hex.Range(Hex.FromRowCol(1, 2), 2).Count(RunController.IsDeployable), wide.Count);
        }

        [Fact]
        public void MusterTextReadsAsCopy()
        {
            string text = Musters("banneret")[0].Text;
            Assert.Contains("Haste", text);
            Assert.Contains("Company", text);
            Assert.DoesNotContain("Mustered", text);

            Assert.Contains("Regen", Musters("cleric")[0].Text);
        }
    }
}
