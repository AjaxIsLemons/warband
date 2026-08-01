using System;
using System.Collections.Generic;
using System.Text.Json;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Item 19's contract: every line is one physical line of valid JSON, survives hostile
    /// content ids, and a real controller-driven fight produces a line that carries enough to
    /// re-simulate (seed via run id, tier, act/node). System.Text.Json is the INDEPENDENT
    /// verifier of the hand-rolled writer — the writer must never validate itself.
    /// </summary>
    public class RunTelemetryTests
    {
        private static readonly DateTime Utc = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

        private static RunController NewRun() =>
            new RunController(73, new StubContent(), Kit.Warband());

        private static JsonElement Parse(string line)
        {
            Assert.DoesNotContain('\n', line);
            Assert.DoesNotContain('\r', line);
            using JsonDocument doc = JsonDocument.Parse(line);
            return doc.RootElement.Clone();
        }

        [Fact]
        public void StartLineCarriesIdentityAndParty()
        {
            var run = NewRun();
            run.State.ContentVersion = "3dba11673c26e858";
            var log = new RunTelemetry(run.State, app: "0.1.test");

            JsonElement e = Parse(log.StartLine(run.State, Utc));
            Assert.Equal(1, e.GetProperty("v").GetInt32());
            Assert.Equal("start", e.GetProperty("t").GetString());
            Assert.Equal("0000000000000049-3dba1167", e.GetProperty("run").GetString());
            Assert.Equal("0000000000000049", e.GetProperty("seed").GetString());
            Assert.Equal("3dba11673c26e858", e.GetProperty("content").GetString());
            Assert.Equal(RevisionCatalog.BorrowedFutureId,
                e.GetProperty("revision").GetString());
            Assert.Equal("2026-07-28T12:00:00Z", e.GetProperty("utc").GetString());
            Assert.Equal(run.State.Field.Count, e.GetProperty("party").GetArrayLength());
            Assert.False(string.IsNullOrEmpty(
                e.GetProperty("party")[0].GetProperty("id").GetString()));
        }

        [Fact]
        public void FightLineFromARealResolvedFightIsResimulable()
        {
            var run = NewRun();
            run.State.ContentVersion = "3dba11673c26e858";
            var log = new RunTelemetry(run.State);
            int nodeBefore = run.State.NodeIndex;

            FightOutcome outcome = run.ResolveFight(FightTier.Collapsing, Kit.AutoPlace(run));
            FightSummary summary = FightSummary.Build(outcome.Battle);

            // State already advanced past the fight; the line records where it happened, so the
            // caller passes the summary of the battle it just watched, not a re-read of state.
            JsonElement e = Parse(log.FightLine(
                run.State, Utc, NodeKind.Fight, FightTier.Collapsing, "The Gnawing Hour",
                outcome, summary));

            Assert.Equal("fight", e.GetProperty("t").GetString());
            Assert.Equal("Collapsing", e.GetProperty("tier").GetString());
            Assert.Equal("The Gnawing Hour", e.GetProperty("encounter").GetString());
            Assert.Equal(outcome.Won, e.GetProperty("won").GetBoolean());
            Assert.Equal(outcome.SandEarned, e.GetProperty("sandEarned").GetInt32());
            Assert.Equal(summary.EndTick, e.GetProperty("ticks").GetInt32());
            Assert.True(e.GetProperty("units").GetArrayLength() > 0);
            foreach (JsonElement u in e.GetProperty("units").EnumerateArray())
            {
                Assert.True(u.TryGetProperty("id", out _));      // stub content ids may be empty
                Assert.True(u.TryGetProperty("dmg", out _));
                Assert.True(u.TryGetProperty("died", out _));
            }
            _ = nodeBefore;
        }

        [Fact]
        public void PurchaseRerollAndEndLinesParse()
        {
            var run = NewRun();
            var log = new RunTelemetry(run.State);

            run.State.Sand = 999;   // the line format is under test, not the economy
            PurchaseResult p = run.BuyOffer(0);
            JsonElement buy = Parse(log.PurchaseLine(run.State, Utc, p));
            Assert.Equal("buy", buy.GetProperty("t").GetString());
            Assert.Equal(p.OfferKind.ToString(), buy.GetProperty("offer").GetString());
            Assert.Equal(p.SandSpent, buy.GetProperty("cost").GetInt32());

            JsonElement reroll = Parse(log.RerollLine(run.State, Utc, 1));
            Assert.Equal("reroll", reroll.GetProperty("t").GetString());

            JsonElement end = Parse(log.EndLine(run.State, Utc));
            Assert.Equal("defeat", end.GetProperty("t").GetString());   // run not complete
            Assert.Equal(run.State.Sand, end.GetProperty("sand").GetInt32());
        }

        [Fact]
        public void PhaseLineCarriesCoarseBoundaryWithoutClickDetail()
        {
            var run = NewRun();
            var log = new RunTelemetry(run.State);

            JsonElement phase = Parse(log.PhaseLine(run.State, Utc, "planning"));

            Assert.Equal("phase", phase.GetProperty("t").GetString());
            Assert.Equal("planning", phase.GetProperty("phase").GetString());
            Assert.Equal(run.State.Act, phase.GetProperty("act").GetInt32());
            Assert.Equal(run.State.NodeIndex, phase.GetProperty("node").GetInt32());
            Assert.Equal(run.State.Sand, phase.GetProperty("sand").GetInt32());
            Assert.False(phase.TryGetProperty("screen", out _));
            Assert.False(phase.TryGetProperty("action", out _));
        }

        [Fact]
        public void HostileStringsSurviveEscaping()
        {
            var run = NewRun();
            var log = new RunTelemetry(run.State);

            string hostile = "a\"b\\c\nd\tef — ✓";
            JsonElement e = Parse(log.SellLine(run.State, Utc, "hero", hostile));
            Assert.Equal(hostile, e.GetProperty("id").GetString());

            JsonElement i = Parse(log.InterludeLine(
                run.State, Utc, InterludePath.Hourstone, 2, hostile));
            Assert.Equal(hostile, i.GetProperty("reward").GetString());
        }

        [Fact]
        public void RunIdIsStableAcrossSaveResume()
        {
            var run = NewRun();
            run.State.ContentVersion = "b8640a3ea7cd360b";
            var before = new RunTelemetry(run.State).RunId;

            string saved = RunSave.Write(run.State);
            RunState resumed = RunSave.Read(saved);
            var after = new RunTelemetry(resumed).RunId;

            Assert.Equal(before, after);
        }

        [Fact]
        public void EndlessChoiceCycleAndDefeatCarryTheBankedScore()
        {
            var run = NewRun();
            var log = new RunTelemetry(run.State);
            run.State.VictoryBanked = true;
            run.State.InEndless = true;
            run.State.Act = 5;
            run.State.EndlessCycles = 1;
            run.State.EndlessBeat = 2;

            JsonElement choice = Parse(log.EndlessChoiceLine(run.State, Utc, true));
            Assert.Equal("endlessChoice", choice.GetProperty("t").GetString());
            Assert.Equal("continue", choice.GetProperty("choice").GetString());
            Assert.True(choice.GetProperty("victoryBanked").GetBoolean());
            Assert.True(choice.GetProperty("endless").GetBoolean());
            Assert.Equal(1, choice.GetProperty("endlessCycles").GetInt32());
            Assert.Equal(2, choice.GetProperty("endlessBeat").GetInt32());

            JsonElement cycle = Parse(log.EndlessCycleLine(run.State, Utc));
            Assert.Equal("endlessCycle", cycle.GetProperty("t").GetString());
            Assert.Equal(1, cycle.GetProperty("cycles").GetInt32());

            run.State.Phase = RunPhase.Defeated;
            JsonElement end = Parse(log.EndLine(run.State, Utc));
            Assert.Equal("endlessDefeat", end.GetProperty("t").GetString());
        }

        [Fact]
        public void RevisionLinesCarryEvolutionAnchorTargetsAndOutcomeFlip()
        {
            var run = NewRun();
            var log = new RunTelemetry(run.State);
            RevisionUpgradeDef upgrade = RevisionCatalog.NextOptions(run.State.Revision)[1];
            JsonElement evolved = Parse(log.RevisionUpgradeLine(run.State, Utc, upgrade));
            Assert.Equal("revisionUpgrade", evolved.GetProperty("t").GetString());
            Assert.Equal(upgrade.Id, evolved.GetProperty("id").GetString());
            Assert.Equal(1, evolved.GetProperty("tier").GetInt32());

            var choice = new RevisionChoice
            {
                PresentTick = 47,
                BranchTick = 27,
                TargetIds = { 0, 2 },
            };
            JsonElement split = Parse(log.RevisionLine(
                run.State, Utc, true, choice,
                new FightOutcome { Won = false },
                new FightOutcome { Won = true, Revised = true }));
            Assert.Equal("revision", split.GetProperty("t").GetString());
            Assert.True(split.GetProperty("finalChance").GetBoolean());
            Assert.Equal(47, split.GetProperty("presentTick").GetInt32());
            Assert.Equal(27, split.GetProperty("branchTick").GetInt32());
            Assert.True(split.GetProperty("flipped").GetBoolean());
            Assert.Equal(2, split.GetProperty("targets").GetArrayLength());
        }
    }
}
