using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>Jake's three passive patterns (2026-07-22), each as pure data.</summary>
    public class PassiveTests
    {
        [Fact]
        public void RampPassive_GainAttackEachTimeIAttack()
        {
            var ramper = BattleTests.Grunt(hp: 200, atk: 5);
            ramper.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.AttackUp,
                    Amount = 3, StatusTicks = -1,
                    Select = new Selector { Kind = SelKind.Self },
                } },
            });
            var result = new Battle(BattleTests.Duel(ramper, BattleTests.Grunt(hp: 200, atk: 8))).Run();

            // Starts weaker (5 vs 8) but out-scales: 5, 8, 11, 14... damage per swing.
            Assert.Equal(Winner.Team0, result.Winner);
            var swings = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack)
                .Select(e => e.Amount).ToList();
            Assert.True(swings.Count >= 3);
            Assert.Equal(swings.OrderBy(x => x).ToList(), swings); // monotonically ramping
            Assert.True(swings.Last() > swings.First());
        }

        [Fact]
        public void ZonePunisher_DamageEnemiesNearMeWhoAttackMyAllies()
        {
            // "Deal 20 to any enemy within 2 hexes that attacks an ally" — the Sentinel.
            var sentinel = BattleTests.Pacifist(300);
            sentinel.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                    new Cond { Kind = CondKind.TargetIsAllyOfOwner },
                    new Cond { Kind = CondKind.SourceIsEnemyOfOwner },
                    new Cond { Kind = CondKind.SourceWithinHexesOfOwner, Amount = 2 },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Damage, Amount = 20,
                    Select = new Selector { Kind = SelKind.EventSource },
                } },
            });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 100), Hex.FromRowCol(3, 2)), // the protected ally
                UnitState.Spawn(1, 0, sentinel, Hex.FromRowCol(3, 3)),                    // guard, adjacent
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 100), Hex.FromRowCol(4, 2)), // attacker in zone
            };
            var result = new Battle(units).Run();

            Assert.Equal(Winner.Team0, result.Winner); // punisher turns 1v1-plus-bystander into a win
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Trigger && e.Source == 1 && e.Target == 2);
        }

        [Fact]
        public void PlacementPassive_AdjacentAlliesAtBattleStartGainHaste()
        {
            Trigger warBanner() => new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste,
                    Amount = Battle.FP, StatusTicks = -1,
                    Select = new Selector { Kind = SelKind.AlliesWithin, Range = 1, ExcludeSelf = true },
                } },
            };

            List<UnitState> Setup(Hex carrierPos)
            {
                var banneret = BattleTests.Pacifist(80);
                banneret.Triggers.Add(warBanner());
                return new List<UnitState>
                {
                    UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 100), carrierPos),
                    UnitState.Spawn(1, 0, banneret, Hex.FromRowCol(2, 2)),
                    UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 130), Hex.FromRowCol(4, 2)),
                };
            }

            // Same army, two placements: the buff exists only when the carrier stood
            // adjacent at battle start. Placement IS the input (ADR 0003).
            var adjacent = new Battle(Setup(Hex.FromRowCol(3, 2))).Run();
            var apart = new Battle(Setup(Hex.FromRowCol(0, 5))).Run();

            Assert.Contains(adjacent.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 0 && e.Aux == (int)StatusKind.Haste);
            Assert.DoesNotContain(apart.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 0 && e.Aux == (int)StatusKind.Haste);
        }
    }
}
