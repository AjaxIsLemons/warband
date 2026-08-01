using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class RevisionTests
    {
        private static UnitDef Def(string name, int mana = 0) => new UnitDef
        {
            Name = name,
            MaxHp = 600,
            Attack = 2,
            AttackInterval = 20,
            MoveInterval = 2,
            Range = 1,
            ManaMax = mana,
        };

        [Fact]
        public void BorrowedFutureCarriesManaAndTurnsOverflowIntoShield()
        {
            var intervention = new TimelineIntervention
            {
                BranchTick = 10,
                Kind = RevisionEffectKind.BorrowedFuture,
                Targets = { new RevisionTarget { UnitId = 0, PresentMana = 40 } },
            };
            var result = new Battle(new[]
            {
                UnitState.Spawn(0, 0, Def("seer", 30), Hex.FromRowCol(1, 1)),
                UnitState.Spawn(100, 1, Def("anchor"), Hex.FromRowCol(6, 1)),
            }, seed: 17, intervention: intervention).Run();

            var announce = Assert.Single(result.Events,
                e => e.Kind == EventKind.RevisionApplied && e.Target == 0);
            Assert.Equal(10, announce.Tick);
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.ManaChanged
                && e.Target == 0 && e.PostMana == 30);
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.ShieldChanged
                && e.Target == 0 && e.PostShield == 10);
        }

        [Fact]
        public void RecallTeleportsToOriginAndDisarmsWithoutSilencing()
        {
            var enemy = UnitState.Spawn(100, 1, Def("marcher", 30), Hex.FromRowCol(6, 2));
            var origin = enemy.Pos;
            var intervention = new TimelineIntervention
            {
                BranchTick = 10,
                Kind = RevisionEffectKind.RecallToFormation,
                Modifiers = RevisionModifier.EmptyHands,
                Targets = { new RevisionTarget { UnitId = 100 } },
            };
            var result = new Battle(new[]
            {
                UnitState.Spawn(0, 0, Def("guard"), Hex.FromRowCol(1, 2)),
                enemy,
            }, seed: 21, intervention: intervention).Run();

            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.Move && e.Source == 100
                && e.Amount == origin.Q && e.Aux == origin.R);
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.StatusApplied && e.Target == 100
                && e.Aux == (int)StatusKind.Disarm && e.Aux2 == 15);
            Assert.DoesNotContain(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.StatusApplied
                && e.Aux == (int)StatusKind.Silence);
        }

        [Fact]
        public void MissingHourDoesNotCreateAFalseVictory()
        {
            var intervention = new TimelineIntervention
            {
                BranchTick = 0,
                Kind = RevisionEffectKind.RecallToFormation,
                Modifiers = RevisionModifier.MissingHour,
                Targets = { new RevisionTarget { UnitId = 100 } },
            };
            var result = new Battle(new[]
            {
                UnitState.Spawn(0, 0, Def("guard"), Hex.FromRowCol(1, 1)),
                UnitState.Spawn(100, 1, Def("lost"), Hex.FromRowCol(6, 1)),
            }, seed: 3, intervention: intervention).Run();

            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.UnitOmitted && e.Target == 100 && e.Amount == 20);
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.UnitReturned && e.Target == 100 && e.Tick == 20);
            Assert.True(result.EndTick > 20);
        }

        [Fact]
        public void BorrowedEvolutionComposesWithoutHidingOrdinaryConsequences()
        {
            UnitState primary = UnitState.Spawn(
                0, 0, Def("primary", 30), Hex.FromRowCol(1, 1));
            primary.Def.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 1 });
            primary.Statuses.Add(new Status { Kind = StatusKind.Silence, TicksLeft = -1 });
            primary.Statuses.Add(new Status { Kind = StatusKind.Disarm, TicksLeft = -1 });
            UnitState second = UnitState.Spawn(
                1, 0, Def("second", 20), Hex.FromRowCol(1, 3));
            UnitState shared = UnitState.Spawn(
                2, 0, Def("shared", 50), Hex.FromRowCol(1, 2));

            var result = new Battle(new[]
            {
                primary,
                second,
                shared,
                UnitState.Spawn(100, 1, Def("anchor"), Hex.FromRowCol(6, 2)),
            }, seed: 31, intervention: new TimelineIntervention
            {
                BranchTick = 10,
                Kind = RevisionEffectKind.BorrowedFuture,
                Modifiers = RevisionModifier.SharedPremonition |
                            RevisionModifier.DeepReserve |
                            RevisionModifier.ClearIntention |
                            RevisionModifier.Convergence |
                            RevisionModifier.Afterthought,
                Targets =
                {
                    new RevisionTarget { UnitId = 0, PresentMana = 40 },
                    new RevisionTarget { UnitId = 1, PresentMana = 5 },
                },
            }).Run();

            Assert.Equal(2, result.Events.Count(e =>
                e.Tick == 10 && e.Kind == EventKind.RevisionApplied));
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.StatusExpired &&
                e.Target == 0 && e.Aux == (int)StatusKind.Silence);
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.StatusExpired &&
                e.Target == 0 && e.Aux == (int)StatusKind.Disarm);
            Assert.Contains(result.Events, e =>
                e.Tick == 10 && e.Kind == EventKind.ManaChanged &&
                e.Target == 2 && e.Amount == 20);
            Assert.Contains(result.Events, e =>
                e.Tick >= 10 && e.Kind == EventKind.ManaChanged &&
                e.Target == 0 && e.Amount == 15);
        }

        [Fact]
        public void RecallEvolutionControlsPrimaryAndSecondaryWithoutSilence()
        {
            UnitState primary = UnitState.Spawn(
                100, 1, Def("primary", 30), Hex.FromRowCol(6, 1));
            primary.Mana = 20;
            UnitState second = UnitState.Spawn(
                101, 1, Def("second"), Hex.FromRowCol(6, 2));
            var result = new Battle(new[]
            {
                UnitState.Spawn(0, 0, Def("guard"), Hex.FromRowCol(1, 2)),
                primary,
                second,
                UnitState.Spawn(102, 1, Def("far"), Hex.FromRowCol(7, 7)),
            }, seed: 37, intervention: new TimelineIntervention
            {
                BranchTick = 0,
                Kind = RevisionEffectKind.RecallToFormation,
                Modifiers = RevisionModifier.FixedPoint |
                            RevisionModifier.LongPeace |
                            RevisionModifier.RollCall |
                            RevisionModifier.EmptyHands,
                Targets = { new RevisionTarget { UnitId = 100 } },
            }).Run();

            Assert.Contains(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.ManaChanged &&
                e.Target == 100 && e.PostMana == 0);
            Assert.Contains(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.StatusApplied &&
                e.Target == 100 && e.Aux == (int)StatusKind.Disarm && e.Aux2 == 25);
            Assert.Contains(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.StatusApplied &&
                e.Target == 100 && e.Aux == (int)StatusKind.Root && e.Aux2 == 15);
            Assert.Contains(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.RevisionApplied && e.Target == 101);
            Assert.Contains(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.StatusApplied &&
                e.Target == 101 && e.Aux == (int)StatusKind.Disarm && e.Aux2 == 10);
            Assert.DoesNotContain(result.Events, e =>
                e.Tick == 0 && e.Kind == EventKind.StatusApplied &&
                e.Aux == (int)StatusKind.Silence);
        }

        [Fact]
        public void SameRevisionProducesTheSameHashAndWire()
        {
            BattleResult Run() => new Battle(new[]
            {
                UnitState.Spawn(0, 0, Def("seer", 40), Hex.FromRowCol(1, 1)),
                UnitState.Spawn(100, 1, Def("anchor"), Hex.FromRowCol(6, 1)),
            }, seed: 44, intervention: new TimelineIntervention
            {
                BranchTick = 10,
                Kind = RevisionEffectKind.BorrowedFuture,
                Targets = { new RevisionTarget { UnitId = 0, PresentMana = 31 } },
            }).Run();

            var a = Run();
            var b = Run();
            Assert.Equal(a.FinalHash, b.FinalHash);
            Assert.Equal(
                a.Events.Select(e => (e.Tick, e.Kind, e.Source, e.Target, e.Amount,
                    e.Aux, e.Aux2, e.PostMana, e.PostShield)),
                b.Events.Select(e => (e.Tick, e.Kind, e.Source, e.Target, e.Amount,
                    e.Aux, e.Aux2, e.PostMana, e.PostShield)));
        }
    }
}
