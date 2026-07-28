using System;
using System.Linq;
using Warband.Run;

namespace Warband.Run.Tests
{
    public sealed class RosterMoveTests
    {
        [Fact]
        public void ReserveHeroMovesToOpenFieldSlotByStableIdentity()
        {
            RunController run = NewRun();
            HeroInstance reserve = AddReserve(run, "alpha");

            run.MoveRosterHero(reserve.InstanceId, RosterZone.Field, 3);

            Assert.Empty(run.State.Bench);
            Assert.Equal(reserve.InstanceId, run.State.Field[3].InstanceId);
            Assert.True(run.TryFindHero(reserve.InstanceId, out RosterZone zone, out int index));
            Assert.Equal(RosterZone.Field, zone);
            Assert.Equal(3, index);
        }

        [Fact]
        public void CrossZoneOccupiedDropSwapsEvenWhenBothZonesAreFull()
        {
            RunController run = NewRun();
            run.State.FieldSlots = 3;
            HeroInstance firstReserve = AddReserve(run, "alpha");
            AddReserve(run, "beta");
            long fieldHero = run.State.Field[1].InstanceId;

            run.MoveRosterHero(firstReserve.InstanceId, RosterZone.Field, 1);

            Assert.Equal(firstReserve.InstanceId, run.State.Field[1].InstanceId);
            Assert.Equal(fieldHero, run.State.Bench[0].InstanceId);
            Assert.Equal(5, run.State.Field.Concat(run.State.Bench)
                .Select(hero => hero.InstanceId).Distinct().Count());
        }

        [Fact]
        public void OccupiedDropWithinZoneReordersHeroes()
        {
            RunController run = NewRun();
            long first = run.State.Field[0].InstanceId;
            long third = run.State.Field[2].InstanceId;

            run.MoveRosterHero(first, RosterZone.Field, 2);

            Assert.Equal(third, run.State.Field[0].InstanceId);
            Assert.Equal(first, run.State.Field[2].InstanceId);
        }

        [Fact]
        public void MoveRejectsLockedSlotAndPendingSpec()
        {
            RunController run = NewRun();
            HeroInstance reserve = AddReserve(run, "alpha");

            Assert.Throws<InvalidOperationException>(() =>
                run.MoveRosterHero(reserve.InstanceId, RosterZone.Field, 4));

            run.State.PendingSpec = new PendingSpec();
            Assert.Throws<InvalidOperationException>(() =>
                run.MoveRosterHero(reserve.InstanceId, RosterZone.Field, 0));
        }

        private static RunController NewRun()
        {
            var run = new RunController(81, new StubContent(), Kit.Warband());
            run.State.FieldSlots = 4;
            return run;
        }

        private static HeroInstance AddReserve(RunController run, string chassisId)
        {
            var hero = new HeroInstance
            {
                InstanceId = run.State.NextHeroInstanceId++,
                ChassisId = chassisId,
            };
            run.State.Bench.Add(hero);
            return hero;
        }
    }
}
