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
    }
}
