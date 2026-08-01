using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Item 15 step A — inscriptions that ride for a fixed number of COMBATS.
    ///
    /// The load-bearing claim these guard is the duration unit. `Design/events-and-inscriptions.md`
    /// §3 law 4: the counter burns on combats and on nothing else. Hades ships the opposite bug —
    /// its curses count encounters while the HUD counts chambers — and the result is four community
    /// guides teaching players to do the arithmetic by hand. So the Interlude test below is a real
    /// negative control, not a formality: it is the exact assertion that would have caught that.
    /// </summary>
    public class TimedInscriptionTests
    {
        private static RunController NewRun(ulong seed = 7, int heroes = 3) =>
            new RunController(seed, new StubContent(), Kit.Warband(heroes));

        /// <summary>PrepareFight parks a fight awaiting commitment, so it can only be called once
        /// per controller. Measuring on a FRESH run per reading keeps each count independent — same
        /// seed, same stub content, so the only difference is the inscription under test.</summary>
        private static int TriggerCount(RunController run) =>
            run.PrepareFight(FightTier.Even, Kit.AutoPlace(run)).TeamTriggerCount;

        private static int TriggersWithTimed(string? id, int fights = 1, ulong seed = 7)
        {
            var run = NewRun(seed);
            if (id != null) run.AddTimedInscription(id, fights);
            return TriggerCount(run);
        }

        [Fact]
        public void TimedInscriptionAppliesToTheFightItWasTakenFor()
        {
            // `Fights = 1` must mean "the next combat", not "no combats". Decrementing before the
            // battle instead of after would make a one-fight cost silently free.
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 1);
            Assert.Contains("paradox.bloodless", run.State.ActiveInscriptionIds);

            Assert.True(TriggersWithTimed("paradox.bloodless") > TriggersWithTimed(null),
                        "a live timed inscription must reach battle prep");
        }

        [Fact]
        public void OneCombatBurnsAOneFightInscription()
        {
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 1);

            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));

            Assert.Empty(run.State.Timed);
            Assert.DoesNotContain("paradox.bloodless", run.State.ActiveInscriptionIds);
            // And the rules are genuinely gone from the NEXT battle, not merely absent from state.
            var control = NewRun();
            control.ResolveFight(FightTier.Even, Kit.AutoPlace(control));
            Assert.Equal(TriggerCount(control), TriggerCount(run));
        }

        [Fact]
        public void TwoFightInscriptionSurvivesTheFirstCombatAndDiesOnTheSecond()
        {
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 2);

            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
            Assert.Equal(1, run.State.Timed.Single().FightsRemaining);
            Assert.Contains("paradox.bloodless", run.State.ActiveInscriptionIds);

            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
            Assert.Empty(run.State.Timed);
        }

        [Fact]
        public void AnInterludeDoesNotBurnACombat()
        {
            // NEGATIVE CONTROL. This is the assertion the whole duration design rests on: route the
            // run through a non-combat beat and the counter must not move. If this ever passes
            // vacuously — because the run never reached an Interlude — the Assert.Equal on the node
            // kind below fails first and says so.
            var run = NewRun();

            // Walk to the Interlude with nothing running, so no combat can muddy the reading.
            int guard = 0;
            while (run.CurrentNodeKind != NodeKind.Event)
            {
                Assert.True(guard++ < 10, "never reached an Interlude — this test would pass vacuously");
                run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
            }
            Assert.Equal(NodeKind.Event, run.CurrentNodeKind);

            // NOW take it, so the only beat it experiences is the Interlude itself.
            run.AddTimedInscription("paradox.bloodless", fights: 1);
            run.ResolveEvent();

            Assert.Equal(1, run.State.Timed.Single().FightsRemaining);
            Assert.Contains("paradox.bloodless", run.State.ActiveInscriptionIds);
        }

        [Fact]
        public void PermanentInscriptionsAreNeverBurned()
        {
            var run = NewRun();
            run.State.Inscriptions.Add("insc.permanent");

            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));

            Assert.Contains("insc.permanent", run.State.Inscriptions);
            Assert.Contains("insc.permanent", run.State.ActiveInscriptionIds);
        }

        [Fact]
        public void RetakingRefreshesInsteadOfStacking()
        {
            // Two rows with the same id would double that inscription's triggers, which no authored
            // Paradox expects. Refresh to the longer remainder instead.
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 1);
            run.AddTimedInscription("paradox.bloodless", fights: 3);

            Assert.Single(run.State.Timed);
            Assert.Equal(3, run.State.Timed.Single().FightsRemaining);
            // Re-taking must not double the triggers: one copy held twice is still one copy.
            Assert.Equal(TriggersWithTimed("paradox.bloodless"), TriggerCount(run));
        }

        [Fact]
        public void RetakingNeverShortensARunningInscription()
        {
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 3);
            run.AddTimedInscription("paradox.bloodless", fights: 1);
            Assert.Equal(3, run.State.Timed.Single().FightsRemaining);
        }

        [Fact]
        public void ATimedInscriptionSurvivesSaveAndResume()
        {
            var run = NewRun();
            run.AddTimedInscription("paradox.bloodless", fights: 2);

            var resumed = RunController.Resume(
                RunSave.Read(RunSave.Write(run.State)), new StubContent());

            var t = Assert.Single(resumed.State.Timed);
            Assert.Equal("paradox.bloodless", t.Id);
            Assert.Equal(2, t.FightsRemaining);
            Assert.Contains("paradox.bloodless", resumed.State.ActiveInscriptionIds);
        }

        [Fact]
        public void ASaveWithNoTimedInscriptionsStillRoundTrips()
        {
            // Backward compatibility: the three timed.* keys are new, and a run that never took one
            // must write and read them as empty rather than as one blank entry.
            var run = NewRun();
            var resumed = RunController.Resume(
                RunSave.Read(RunSave.Write(run.State)), new StubContent());
            Assert.Empty(resumed.State.Timed);
        }

        [Fact]
        public void AddingATimedInscriptionRejectsNonsense()
        {
            var run = NewRun();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => run.AddTimedInscription("paradox.bloodless", fights: 0));
            Assert.Throws<System.ArgumentException>(
                () => run.AddTimedInscription("", fights: 1));
        }

        [Fact]
        public void HasInscriptionSeesBothPermanentAndTimed()
        {
            // The offer surfaces dedupe through this, so a timed copy of X must block X being
            // offered again while it is still live.
            var run = NewRun();
            run.State.Inscriptions.Add("insc.permanent");
            run.AddTimedInscription("paradox.bloodless", fights: 1);

            Assert.True(run.State.HasInscription("insc.permanent"));
            Assert.True(run.State.HasInscription("paradox.bloodless"));
            Assert.False(run.State.HasInscription("insc.never.taken"));
        }
    }
}
