using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class EndlessRunTests
    {
        private static RunController ReachVictoryChoice(StubContent? content = null)
        {
            content ??= new StubContent();
            var run = new RunController(3030, content, Kit.Warband());
            while (run.State.Phase != RunPhase.VictoryChoice)
            {
                if (run.State.Phase == RunPhase.Reward)
                {
                    run.ChooseBossReward(0);
                    continue;
                }

                switch (run.CurrentNodeKind)
                {
                    case NodeKind.Fight:
                        Assert.True(run.ResolveFight(
                            FightTier.Fraying, Kit.AutoPlace(run)).Won);
                        break;
                    case NodeKind.Event:
                        run.ResolveEvent();
                        break;
                    case NodeKind.Boss:
                        Assert.True(run.ResolveBoss(Kit.AutoPlace(run)).Won);
                        break;
                }
            }
            return run;
        }

        private static List<(UnitDef, Hex)> WeakEncounter() =>
            new List<(UnitDef, Hex)>
            {
                (new UnitDef
                {
                    Name = "endless witness",
                    MaxHp = 10,
                    Attack = 1,
                    AttackInterval = 20,
                    Range = 1,
                    MoveInterval = 10,
                }, Hex.FromRowCol(6, 0)),
            };

        private static List<(UnitDef, Hex)> LethalEncounter() =>
            new List<(UnitDef, Hex)>
            {
                (new UnitDef
                {
                    Name = "the deeper hour",
                    MaxHp = 100000,
                    Attack = 100000,
                    AttackInterval = 1,
                    Range = 99,
                    MoveInterval = 1,
                }, Hex.FromRowCol(6, 0)),
            };

        [Fact]
        public void FinalBossBanksVictoryBeforeTheContinuationChoice()
        {
            var run = ReachVictoryChoice();

            Assert.Equal(RunPhase.VictoryChoice, run.State.Phase);
            Assert.True(run.State.VictoryBanked);
            Assert.True(run.State.Victory);
            Assert.False(run.State.Over);
            Assert.False(run.State.InEndless);
            Assert.Equal(3, run.State.BossWins);
            Assert.Throws<System.InvalidOperationException>(() => _ = run.CurrentNodeKind);

            run.RetireWithVictory();

            Assert.Equal(RunPhase.Complete, run.State.Phase);
            Assert.True(run.State.Victory);
            Assert.True(run.State.Over);
        }

        [Fact]
        public void EndlessCycleIsThreeActThreePoolFightsThenTheCrown()
        {
            var actsAsked = new List<int>();
            var content = new StubContent();
            var run = ReachVictoryChoice(content);
            content.EncounterOverride = (act, node, tier, rng) =>
            {
                actsAsked.Add(act);
                return WeakEncounter();
            };

            int sandBefore = run.State.Sand;
            run.ContinueBeyondTheHour();

            Assert.True(run.State.InEndless);
            Assert.True(run.State.Victory);
            Assert.Equal(4, run.State.Act);
            Assert.Equal(0, run.State.NodeIndex);
            Assert.Equal(NodeKind.Fight, run.CurrentNodeKind);

            var cfg = new RunConfig();
            for (int beat = 1; beat <= 3; beat++)
            {
                FightOutcome outcome =
                    run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
                Assert.True(outcome.Won);
                Assert.Equal(cfg.FightReward(3, FightTier.Fraying), outcome.SandEarned);
                Assert.Equal(beat, run.State.EndlessBeat);
            }

            Assert.Equal(new[] { 4, 4, 4 }, actsAsked);
            Assert.True(run.AtBoss);
            Assert.Equal(3, run.State.NodeIndex);
            Assert.Equal(NodeKind.Boss, run.CurrentNodeKind);
            Assert.Equal(sandBefore + 3 * cfg.FightReward(3, FightTier.Fraying),
                         run.State.Sand);

            Assert.True(run.ResolveBoss(Kit.AutoPlace(run)).Won);

            Assert.Equal(1, run.State.EndlessCycles);
            Assert.Equal(0, run.State.EndlessBeat);
            Assert.Equal(5, run.State.Act);
            Assert.Equal(0, run.State.NodeIndex);
            Assert.Equal(4, run.State.BossWins);
            Assert.Equal(NodeKind.Fight, run.CurrentNodeKind);
        }

        [Fact]
        public void EndlessDefeatPreservesTheAuthoredVictoryAndScore()
        {
            var content = new StubContent();
            var run = ReachVictoryChoice(content);
            content.EncounterOverride = (act, node, tier, rng) => WeakEncounter();
            run.ContinueBeyondTheHour();
            for (int beat = 0; beat < 3; beat++)
                Assert.True(run.ResolveFight(
                    FightTier.Stable, Kit.AutoPlace(run)).Won);
            Assert.True(run.ResolveBoss(Kit.AutoPlace(run)).Won);

            content.EncounterOverride = (act, node, tier, rng) => LethalEncounter();
            FightOutcome loss =
                run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run));

            Assert.False(loss.Won);
            Assert.Equal(RunPhase.Defeated, run.State.Phase);
            Assert.True(run.State.Over);
            Assert.True(run.State.Victory);
            Assert.True(run.State.EndlessDefeat);
            Assert.Equal(1, run.State.EndlessCycles);
            Assert.Equal(0, run.State.EndlessBeat);
        }

        [Fact]
        public void EndlessContinuationAndScoreSurviveSaveResume()
        {
            var content = new StubContent();
            var run = ReachVictoryChoice(content);
            content.EncounterOverride = (act, node, tier, rng) => WeakEncounter();
            run.ContinueBeyondTheHour();
            run.ResolveFight(FightTier.Collapsing, Kit.AutoPlace(run));

            RunState saved = RunSave.Read(RunSave.Write(run.State));
            RunController resumed = RunController.Resume(saved, content);

            Assert.True(resumed.State.VictoryBanked);
            Assert.True(resumed.State.InEndless);
            Assert.Equal(4, resumed.State.Act);
            Assert.Equal(1, resumed.State.NodeIndex);
            Assert.Equal(0, resumed.State.EndlessCycles);
            Assert.Equal(1, resumed.State.EndlessBeat);
            Assert.Equal(NodeKind.Fight, resumed.CurrentNodeKind);
            Assert.Equal(3, resumed.State.ActMaps.Length);
        }

        [Fact]
        public void SaveRejectsEndlessWithoutABankedVictory()
        {
            var state = ReachVictoryChoice().State;
            state.Phase = RunPhase.Planning;
            state.InEndless = true;
            state.VictoryBanked = false;
            state.Act = 4;

            RunSaveException error =
                Assert.Throws<RunSaveException>(() => RunSave.Read(RunSave.Write(state)));
            Assert.Contains("banked standard victory", error.Message);
        }
    }
}
