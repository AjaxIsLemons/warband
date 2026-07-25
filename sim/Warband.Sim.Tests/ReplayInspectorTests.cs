using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The render-config tool: a replay's event log folds into distinct PRESENTATION
    /// signatures (what the tell layer keys on). These pin the splitting rules — damage by
    /// cause + crit, status by kind, fields by wall/zone — plus the aggregation.
    /// </summary>
    public class ReplayInspectorTests
    {
        private static BattleEvent Ev(EventKind kind, int tick, int amount = 0,
            Cause cause = Cause.None, StatusKind status = default, bool crit = false, int aux = -1)
            => new BattleEvent { Kind = kind, Tick = tick, Amount = amount, Cause = cause, Crit = crit, Aux = aux == -1 ? (int)status : aux };

        private static BattleEvent Field(int tick, FieldFlavor flavor, bool wall = false)
            => new BattleEvent { Kind = EventKind.FieldCreated, Tick = tick, Amount = wall ? 1 : 0, Aux3 = (int)flavor };

        [Fact]
        public void DamageSplitsByCauseAndCrit()
        {
            var events = new List<BattleEvent>
            {
                Ev(EventKind.DamageDealt, 0, 10, Cause.Attack),
                Ev(EventKind.DamageDealt, 1, 30, Cause.Attack, crit: true),
                Ev(EventKind.DamageDealt, 2, 4, Cause.Burn),
                Ev(EventKind.DamageDealt, 3, 12, Cause.Attack),   // merges with row 1
            };
            var labels = ReplayInspector.Summarize(events).Select(s => s.Label).ToList();

            Assert.Contains("Damage/Attack", labels);
            Assert.Contains("Damage/Attack/crit", labels);
            Assert.Contains("Damage/Burn", labels);

            var atk = ReplayInspector.Summarize(events).First(s => s.Label == "Damage/Attack");
            Assert.Equal(2, atk.Count);
            Assert.Equal(10, atk.MinAmount);
            Assert.Equal(12, atk.MaxAmount);
        }

        [Fact]
        public void StatusSplitsByKindAndDirection()
        {
            var events = new List<BattleEvent>
            {
                Ev(EventKind.StatusApplied, 0, status: StatusKind.Burn),
                Ev(EventKind.StatusApplied, 1, status: StatusKind.Taunt),
                Ev(EventKind.StatusExpired, 5, status: StatusKind.Burn),
            };
            var labels = ReplayInspector.Summarize(events).Select(s => s.Label).ToList();

            Assert.Contains("Status+/Burn", labels);
            Assert.Contains("Status+/Taunt", labels);
            Assert.Contains("Status-/Burn", labels);
            Assert.DoesNotContain("Status-/Taunt", labels);
        }

        [Fact]
        public void FieldSplitsWallFromZone()
        {
            var events = new List<BattleEvent>
            {
                Ev(EventKind.FieldCreated, 0, amount: 0),  // flavorless zone
                Ev(EventKind.FieldCreated, 1, amount: 1),  // flavorless wall
            };
            var labels = ReplayInspector.Summarize(events).Select(s => s.Label).ToList();
            Assert.Contains("Field/zone", labels);
            Assert.Contains("Field/wall", labels);
        }

        [Fact]
        public void FieldSplitsByFlavorAndComposesWithWallness()
        {
            var events = new List<BattleEvent>
            {
                Field(0, FieldFlavor.Hazard),
                Field(1, FieldFlavor.Boon),
                Field(2, FieldFlavor.Debuff),
                Field(3, FieldFlavor.Hazard, wall: true),   // a burning wall is both
                Field(4, FieldFlavor.Hazard),               // merges with row 1
            };
            var stats = ReplayInspector.Summarize(events);
            var labels = stats.Select(s => s.Label).ToList();

            Assert.Contains("Field/Hazard", labels);
            Assert.Contains("Field/Boon", labels);
            Assert.Contains("Field/Debuff", labels);
            Assert.Contains("Field/wall+Hazard", labels);
            Assert.Equal(2, stats.First(s => s.Label == "Field/Hazard").Count);
        }

        [Fact]
        public void AggregatesCountsTicksInFirstAppearanceOrder()
        {
            var events = new List<BattleEvent>
            {
                Ev(EventKind.BattleStart, 0),
                Ev(EventKind.Attack, 3),
                Ev(EventKind.Attack, 7),
                Ev(EventKind.Death, 9, amount: 5),
            };
            var stats = ReplayInspector.Summarize(events);

            // order = first appearance
            Assert.Equal(new[] { "BattleStart", "Attack", "Death" }, stats.Select(s => s.Label).ToArray());

            var attack = stats.First(s => s.Label == "Attack");
            Assert.Equal(2, attack.Count);
            Assert.Equal(3, attack.FirstTick);
            Assert.Equal(7, attack.LastTick);
        }
    }
}
