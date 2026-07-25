using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Starting a run — the seam the shell's New Run / Recruit screens bind to.
    /// </summary>
    public class RunSetupTests
    {
        [Fact]
        public void RecruitOfferIsDeterministicForASeed()
        {
            var content = new StubContent();
            var a = RunSetup.RecruitOffer(content, 1234);
            var b = RunSetup.RecruitOffer(content, 1234);
            Assert.Equal(a, b);   // same seed, same run — reproducible end to end
        }

        [Fact]
        public void DefaultOpeningDraftShowsFiveChoices()
        {
            var content = new StubContent();

            Assert.Equal(5, RunSetup.RecruitOffer(content, 1234).Count);
        }

        [Fact]
        public void DifferentSeedsGiveDifferentOffers()
        {
            var content = new StubContent { Heroes = Enumerable.Range(0, 20).Select(i => $"h{i}").ToList() };
            var a = RunSetup.RecruitOffer(content, 1);
            var b = RunSetup.RecruitOffer(content, 2);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void OfferHasNoDuplicatesAndRespectsASmallPool()
        {
            var content = new StubContent { Heroes = new List<string> { "a", "b" } };
            var offer = RunSetup.RecruitOffer(content, 5, count: 6);

            Assert.Equal(offer.Distinct().Count(), offer.Count);
            Assert.Equal(2, offer.Count);   // never invents heroes to pad the offer
        }

        [Fact]
        public void OfferOnlyDrawsFromTheActOnePool()
        {
            var content = new StubContent();
            var pool = content.HeroPool(1);
            Assert.All(RunSetup.RecruitOffer(content, 77), id => Assert.Contains(id, pool));
        }

        [Fact]
        public void BeginBuildsARunnableControllerFromThePicks()
        {
            var content = new StubContent();
            var picks = RunSetup.RecruitOffer(content, 42).Take(3).ToList();
            var run = RunSetup.Begin(42, content, picks);

            Assert.Equal(RunPhase.Planning, run.State.Phase);
            Assert.Equal(picks, run.State.Field.Select(h => h.ChassisId));
            Assert.Equal(1, run.State.Act);
            Assert.False(run.State.Over);
        }

        [Fact]
        public void BeginRejectsIllegalWarbandsWithAShowableMessage()
        {
            var content = new StubContent();
            var cfg = new RunConfig();

            var tooMany = Enumerable.Range(0, cfg.StartingFieldSlots + 1).Select(i => $"hero{i}").ToList();
            Assert.Contains("pick 1..", Assert.Throws<ArgumentException>(
                () => RunSetup.Begin(1, content, tooMany)).Message);

            Assert.Contains("same hero twice", Assert.Throws<ArgumentException>(
                () => RunSetup.Begin(1, content, new[] { "hero0", "hero0" })).Message);

            Assert.Throws<ArgumentException>(() => RunSetup.Begin(1, content, new string[0]));
        }

        [Fact]
        public void PicksRemainingDrivesTheRecruitScreensReadyState()
        {
            var cfg = new RunConfig();
            Assert.Equal(cfg.StartingFieldSlots, RunSetup.PicksRemaining(0, cfg));
            Assert.Equal(0, RunSetup.PicksRemaining(cfg.StartingFieldSlots, cfg));
            Assert.Equal(0, RunSetup.PicksRemaining(cfg.StartingFieldSlots + 5, cfg));   // never negative
        }

        [Fact]
        public void ARunStartedFromPicksPlaysThroughToATerminalPhase()
        {
            var content = new StubContent();
            var picks = RunSetup.RecruitOffer(content, 8).Take(3).ToList();
            var state = Kit.PlayOut(RunSetup.Begin(8, content, picks));

            Assert.True(state.Over);
            Assert.True(state.Phase == RunPhase.Complete || state.Phase == RunPhase.Defeated);
        }
    }
}
