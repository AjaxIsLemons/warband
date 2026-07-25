using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The Bond readout feeds the what-if probe, and the probe's whole job is telling Jake
    /// whether the encounter poses a decision. A wrong number here would send him down a false
    /// path, so the readout is pinned against fights with known shapes.
    /// </summary>
    public class BondReadoutTests
    {
        private static UnitDef Executioner(int attack = 1000, int range = 8) => new UnitDef
        {
            Name = "proof executioner", MaxHp = 4000, Attack = attack,
            AttackInterval = 10, Range = range, MoveInterval = 5,
        };

        private static (BattleResult Result, List<int> EnemyIds) RunOath(UnitDef player, Hex at, int seed = 11)
        {
            var encounter = Encounters.BondedPair();
            var units = new List<UnitState> { UnitState.Spawn(0, 0, player, at) };
            var ids = new List<int>();
            for (int i = 0; i < encounter.Enemies.Count; i++)
            {
                units.Add(UnitState.Spawn(100 + i, 1, encounter.Enemies[i].Def, encounter.Enemies[i].Pos));
                ids.Add(100 + i);
            }
            return (new Battle(units, seed: (ulong)seed).Run(), ids);
        }

        [Fact]
        public void ReadsTheEnrageAndItsSurvivor()
        {
            var (result, ids) = RunOath(Executioner(), Hex.FromRowCol(2, 2));
            var bond = Encounters.ReadBond(result, ids);

            Assert.True(bond.EnrageFired);
            Assert.Contains(bond.SurvivorId, ids);

            // Cross-check against the raw log rather than trusting the readout's own arithmetic.
            var enrage = result.Events.Single(e =>
                e.Kind == EventKind.StatusApplied && e.Aux == (int)StatusKind.Haste &&
                e.Amount == Encounters.BondHaste && ids.Contains(e.Target));
            Assert.Equal(enrage.Tick, bond.EnrageTick);
            Assert.Equal(enrage.Target, bond.SurvivorId);
            Assert.Equal(result.EndTick - enrage.Tick, bond.TicksAfterEnrage);

            // The survivor is the one that did NOT die first.
            var firstDeath = result.Events.First(e => e.Kind == EventKind.Death && ids.Contains(e.Target));
            Assert.NotEqual(firstDeath.Target, bond.SurvivorId);
        }

        [Fact]
        public void CountsOnlyTheSurvivorsSwingsAfterTheEnrage()
        {
            var (result, ids) = RunOath(Executioner(), Hex.FromRowCol(2, 2));
            var bond = Encounters.ReadBond(result, ids);

            int expected = result.Events.Count(e =>
                e.Kind == EventKind.Attack && e.Source == bond.SurvivorId && e.Tick >= bond.EnrageTick);
            Assert.Equal(expected, bond.SurvivorSwingsAfterEnrage);

            // Swings thrown by the partner, or before the Enrage, must not be counted.
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.Attack && e.Source == bond.SurvivorId &&
                e.Tick < bond.EnrageTick && bond.SurvivorSwingsAfterEnrage == 0);
        }

        [Fact]
        public void NoEnrageWhenNoOathboundDies()
        {
            // A pacifist can't kill anyone, so the Bond never triggers — the readout must say so
            // rather than reporting a phantom Enrage at tick 0.
            var pacifist = new UnitDef
            {
                Name = "bystander", MaxHp = 60, Attack = 0,
                AttackInterval = 10, Range = 1, MoveInterval = 5,
            };
            var (result, ids) = RunOath(pacifist, Hex.FromRowCol(0, 0));
            var bond = Encounters.ReadBond(result, ids);

            Assert.False(bond.EnrageFired);
            Assert.Equal(-1, bond.EnrageTick);
            Assert.Equal(-1, bond.SurvivorId);
            Assert.Equal(0, bond.SurvivorSwingsAfterEnrage);
            Assert.Equal(0, bond.TicksAfterEnrage);
            Assert.Equal(0, bond.EnemyDeaths);
        }

        [Fact]
        public void IncidentalHasteIsNotMistakenForTheBond()
        {
            // The readout keys on the published BondHaste magnitude. A player kit that hastes an
            // ENEMY would otherwise be indistinguishable from an Enrage.
            // The executioner is slowed deliberately: a one-shot kill puts the death at tick 0,
            // where the decoy also lands, and the test could not tell the two apart.
            var confuser = Executioner(attack: 50);
            confuser.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do =
                {
                    new EffectDef
                    {
                        Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste,
                        Amount = Encounters.BondHaste - 1, StatusTicks = 200,
                        Select = new Selector { Kind = SelKind.EnemiesWithin, Range = 8 },
                    },
                },
            });

            var (result, ids) = RunOath(confuser, Hex.FromRowCol(2, 2));
            var bond = Encounters.ReadBond(result, ids);

            // The decoy really is in the log, at tick 0, on the same units.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Aux == (int)StatusKind.Haste &&
                e.Amount == Encounters.BondHaste - 1 && e.Tick == 0 && ids.Contains(e.Target));

            // …and the readout ignored it: Enrage is reported at the death tick, well after tick 0.
            Assert.True(bond.EnrageFired);
            Assert.True(bond.EnrageTick > 0, $"enrage read at tick {bond.EnrageTick} — decoy tick");
            var firstDeath = result.Events.First(e => e.Kind == EventKind.Death && ids.Contains(e.Target));
            Assert.Equal(firstDeath.Tick, bond.EnrageTick);
            Assert.NotEqual(firstDeath.Target, bond.SurvivorId);
        }

        [Fact]
        public void CountsBothEnemyDeathsOnAClearedBoard()
        {
            var (result, ids) = RunOath(Executioner(), Hex.FromRowCol(2, 2));
            var bond = Encounters.ReadBond(result, ids);

            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Equal(2, bond.EnemyDeaths);
        }
    }
}
