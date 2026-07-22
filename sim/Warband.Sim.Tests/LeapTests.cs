using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class LeapTests
    {
        [Fact]
        public void AssassinLeapsAtBattleStartAndHuntsTheBackline()
        {
            var shade = BattleTests.Grunt(hp: 120, atk: 12);
            shade.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.Leap,
                    Select = new Selector { Kind = SelKind.FarthestEnemy },
                } },
            });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, shade, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 200), Hex.FromRowCol(3, 3)),   // our frontline
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 200), Hex.FromRowCol(4, 3)),   // their frontline
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 80, atk: 15), Hex.FromRowCol(7, 2)), // their backline carry
            };
            var result = new Battle(units).Run();

            // The leap: a tick-0 Move event landing adjacent to the backliner.
            var leap = result.Events.First(e => e.Kind == EventKind.Move && e.Source == 0);
            Assert.Equal(0, leap.Tick);
            Assert.Equal(1, Hex.Distance(new Hex(leap.Amount, leap.Aux), Hex.FromRowCol(7, 2)));

            // "Then attacks like normal": its first swing goes into the backliner, not the front.
            var firstSwing = result.Events.First(e => e.Kind == EventKind.Attack && e.Source == 0);
            Assert.Equal(3, firstSwing.Target);
        }

        [Fact]
        public void LeapBattlesReconstructAndStayDeterministic()
        {
            List<UnitState> Setup()
            {
                var shade = BattleTests.Grunt(hp: 120, atk: 12);
                shade.Triggers.Add(new Trigger
                {
                    On = EventKind.BattleStart,
                    Do = { new EffectDef { Kind = EffectKind.Leap, Select = new Selector { Kind = SelKind.FarthestEnemy } } },
                });
                return new List<UnitState>
                {
                    UnitState.Spawn(0, 0, shade, Hex.FromRowCol(0, 0)),
                    UnitState.Spawn(1, 1, BattleTests.Grunt(hp: 150), Hex.FromRowCol(4, 2)),
                    UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 90), Hex.FromRowCol(7, 5)),
                };
            }
            var r1 = new Battle(Setup()).Run();
            var r2 = new Battle(Setup()).Run();
            Assert.Equal(r1.FinalHash, r2.FinalHash);

            var fold = PlaybackState.From(r1.InitialUnits);
            for (int t = 0; t < r1.TickViewHashes.Count; t++)
            {
                fold.AdvanceToTick(r1.Events, t);
                Assert.True(r1.TickViewHashes[t] == fold.ViewHash(), $"fold diverged at tick {t}");
            }
        }
    }
}
