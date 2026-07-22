using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class AuraStatRuleTests
    {
        private static UnitDef Banneret()
        {
            var def = BattleTests.Pacifist(60);
            def.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.CreateField,
                    Select = new Selector { Kind = SelKind.Self },
                    Field = new FieldDef
                    {
                        AttachToOwner = true, Radius = 1, Ticks = -1,
                        Presence = { (StatusKind.Haste, 500) }, PresenceAffects = Affects.Allies,
                    },
                } },
            });
            return def;
        }

        [Fact]
        public void AuraGrantsOnEntryAndStripsOnLeaving()
        {
            var banneret = Banneret();
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 300), Hex.FromRowCol(1, 0)), // starts inside
                UnitState.Spawn(1, 0, banneret, Hex.FromRowCol(0, 0)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 300), Hex.FromRowCol(7, 5)), // far — carrier must walk
            };
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = 1 });
            var result = new Battle(units).Run();

            var applied = result.Events.First(e =>
                e.Kind == EventKind.StatusApplied && e.Target == 0 && e.Aux == (int)StatusKind.Haste);
            var expired = result.Events.First(e =>
                e.Kind == EventKind.StatusExpired && e.Target == 0 && e.Aux == (int)StatusKind.Haste);
            Assert.True(applied.Tick < expired.Tick); // gained standing beside the banneret, lost it marching away
        }

        [Fact]
        public void AuraDiesWithItsAnchor()
        {
            var banneret = Banneret();
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 400), Hex.FromRowCol(3, 3)), // in aura, rooted
                UnitState.Spawn(1, 0, banneret, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 400, atk: 15), Hex.FromRowCol(4, 2)), // kills banneret first
            };
            units[0].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = 0 });
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = 1 });
            var result = new Battle(units).Run();

            var bannerDeath = result.Events.First(e => e.Kind == EventKind.Death && e.Target == 1);
            var fieldGone = result.Events.First(e => e.Kind == EventKind.FieldExpired);
            var hasteGone = result.Events.First(e =>
                e.Kind == EventKind.StatusExpired && e.Target == 0 && e.Aux == (int)StatusKind.Haste);
            Assert.True(bannerDeath.Tick <= fieldGone.Tick);
            Assert.True(fieldGone.Tick <= hasteGone.Tick + 1);
            AssertReconstructs(result);
        }

        [Fact]
        public void ConditionalStatRule_EnrageBelowThreshold()
        {
            var rager = BattleTests.Grunt(hp: 100, atk: 10);
            rager.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackFlat, Amount = 10,
                When = { new Cond { Kind = CondKind.OwnerBelowHpPct, Amount = 60 } },
            });
            var result = new Battle(BattleTests.Duel(rager, BattleTests.Grunt(hp: 100, atk: 10))).Run();

            var swings = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack)
                .Select(e => e.Amount).ToList();
            Assert.Contains(10, swings);
            Assert.Contains(20, swings); // enraged
            Assert.True(swings.IndexOf(20) > swings.LastIndexOf(10)); // every 20 comes after every 10
            Assert.Equal(Winner.Team0, result.Winner); // the enrage breaks the mirror
        }

        [Fact]
        public void ConditionalStatRule_AttackSpeedBelowThreshold()
        {
            var zerk = BattleTests.Grunt(hp: 100, atk: 10);
            zerk.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackSpeed, Amount = 1000,
                When = { new Cond { Kind = CondKind.OwnerBelowHpPct, Amount = 60 } },
            });
            var result = new Battle(BattleTests.Duel(zerk, BattleTests.Grunt(hp: 100, atk: 10))).Run();
            Assert.Equal(Winner.Team0, result.Winner); // doubles attack speed once wounded
        }

        private static void AssertReconstructs(BattleResult result)
        {
            var fold = PlaybackState.From(result.InitialUnits);
            for (int t = 0; t < result.TickViewHashes.Count; t++)
            {
                fold.AdvanceToTick(result.Events, t);
                Assert.True(result.TickViewHashes[t] == fold.ViewHash(), $"fold diverged at tick {t}");
            }
        }
    }
}
