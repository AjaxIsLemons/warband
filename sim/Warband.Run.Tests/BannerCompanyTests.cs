using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The Banneret's muster law, on the Rally rather than only on the Haste (2026-07-27).
    ///
    /// Rally used to select allies inside a LIVE radius 2 at the instant of the cast, which meant
    /// it paid almost nobody: pathfinding pulls a warband apart within a dozen ticks, so the ally
    /// deliberately placed beside the banner is three or four hexes away by the first cast. The
    /// placement decision the card advertised was erased before it could pay out. The Company now
    /// works the way MUSTER always did (ADR 0014) — the roster is sworn in at BattleStart and the
    /// cast reaches it at any range.
    /// </summary>
    public class BannerCompanyTests
    {
        private static UnitState Spawn(int id, int team, string chassis, Hex pos, params string[] nodes) =>
            Loadout.Spawn(id, team, Loadout.Compose(Kits.Chassis[chassis],
                nodes: nodes.Select(n => Kits.Nodes[n])), pos);

        /// <summary>Ally 1 starts touching the banner, ally 2 starts out of reach. Both march.</summary>
        private static List<UnitState> Field(params string[] bannerNodes) => new List<UnitState>
        {
            Spawn(0, 0, "banneret",   Hex.FromRowCol(1, 2), bannerNodes),
            Spawn(1, 0, "pyromancer", Hex.FromRowCol(1, 3)),   // touching at placement → sworn in
            Spawn(2, 0, "pyromancer", Hex.FromRowCol(0, 0)),   // far at placement → never sworn in
            Spawn(3, 1, "bulwark",    Hex.FromRowCol(6, 2)),
            Spawn(4, 1, "phalanx",    Hex.FromRowCol(7, 3)),
        };

        /// <summary>Rally grants are the mana the BANNER sourced — never the amount, which
        /// GainMana clips against the ally's remaining pool (and drops entirely at full mana),
        /// and never a swing's own gain, which carries no source at all.</summary>
        private static List<BattleEvent> RalliesTo(BattleResult r, int allyId) => r.Events.Where(e =>
            e.Kind == EventKind.ManaChanged && e.Source == 0 && e.Target == allyId).ToList();

        [Fact]
        public void CompanyIsSwornInAtPlacement()
        {
            var r = new Battle(Field()).Run();

            Assert.Contains(r.Events, e => e.Kind == EventKind.StatusApplied
                && e.Target == 1 && e.Aux == (int)StatusKind.Mustered);
            Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.StatusApplied
                && e.Target == 2 && e.Aux == (int)StatusKind.Mustered);
        }

        [Fact]
        public void RallyPaysTheCompanyAfterTheLineHasBrokenApart()
        {
            var units = Field();
            var pos = units.ToDictionary(u => u.Id, u => u.Pos);
            var r = new Battle(units).Run();

            // Replay the walk so every rally can be checked against the gap it crossed. The old
            // radius-2 Rally would have scored zero here, which is the whole point of the fixture.
            int rallies = 0, ralliesFromOutsideTheOldReach = 0;
            foreach (var e in r.Events)
            {
                if (e.Kind == EventKind.Move) pos[e.Source] = new Hex(e.Amount, e.Aux);
                else if (e.Kind == EventKind.ManaChanged && e.Source == 0 && e.Target == 1)
                {
                    rallies++;
                    if (Hex.Distance(pos[0], pos[1]) > 2) ralliesFromOutsideTheOldReach++;
                }
            }

            Assert.Contains(r.Events, e => e.Kind == EventKind.Cast && e.Source == 0);
            Assert.True(rallies > 0, "the Company was never paid");
            Assert.Equal(rallies, ralliesFromOutsideTheOldReach);
        }

        [Fact]
        public void RallyStillIgnoresAlliesWhoWereNeverMustered()
        {
            var r = new Battle(Field()).Run();
            Assert.Empty(RalliesTo(r, 2));
        }

        [Fact]
        public void LastMarchSwearsInTheWholeWarband()
        {
            var r = new Battle(Field("banneret.warcaller", "banneret.warcaller.lastmarch")).Run();

            Assert.Contains(r.Events, e => e.Kind == EventKind.Cast && e.Source == 0);
            Assert.NotEmpty(RalliesTo(r, 2));   // the ally placed in the far corner is Company now
        }

        /// <summary>Wide Banner's radius-2 muster overlaps the innate radius-1 one. The Haste is
        /// authored to stack there ("r1 companions get both"); membership must not, or the roster
        /// carries a duplicate that no rule wants and the icon row would have to explain.</summary>
        [Fact]
        public void OverlappingMustersSwearAUnitInOnlyOnce()
        {
            var r = new Battle(Field("banneret.herald", "banneret.herald.widebanner")).Run();

            Assert.Equal(1, r.Events.Count(e => e.Kind == EventKind.StatusApplied
                && e.Target == 1 && e.Aux == (int)StatusKind.Mustered));
            Assert.Equal(2, r.Events.Count(e => e.Kind == EventKind.StatusApplied
                && e.Target == 1 && e.Aux == (int)StatusKind.Haste));
        }

        /// <summary>ADR 0024's disclosure contract: the card names the Company, not the tag and the
        /// 99-hex radius that implement it.</summary>
        [Fact]
        public void TheCardSaysCompanyRatherThanTheMachinery()
        {
            var def = Loadout.Compose(Kits.Chassis["banneret"]).Def;
            string copy = MechanicalRulePresenter.Signature(def.Signature);

            Assert.Contains("the Company", copy);
            Assert.DoesNotContain("99", copy);
            Assert.DoesNotContain("Mustered", copy);
        }
    }
}
