using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The sim half of the display vocabulary. The point of these tests is that a NEW enum value
    /// fails here, in CI, instead of shipping a bare "MultiShotRamp" into a tooltip.
    /// </summary>
    public class LexiconTests
    {
        [Fact]
        public void EveryStatusKindHasCopy()
        {
            var missing = Enum.GetValues(typeof(StatusKind)).Cast<StatusKind>()
                .Where(k => !Lexicon.Statuses.ContainsKey(k))
                .ToList();
            Assert.True(missing.Count == 0, $"StatusKind without copy: {string.Join(", ", missing)}");
        }

        [Fact]
        public void EveryCauseHasCopy()
        {
            var missing = Enum.GetValues(typeof(Cause)).Cast<Cause>()
                .Where(c => !Lexicon.Causes.ContainsKey(c))
                .ToList();
            Assert.True(missing.Count == 0, $"Cause without copy: {string.Join(", ", missing)}");
        }

        [Fact]
        public void CopyIsPresentableNotRaw()
        {
            foreach (var pair in Lexicon.Statuses)
            {
                Assert.False(string.IsNullOrWhiteSpace(pair.Value.Name));
                Assert.False(string.IsNullOrWhiteSpace(pair.Value.Text));
                // A description that just restates the enum name teaches nothing.
                Assert.NotEqual(pair.Value.Name, pair.Value.Text);
                Assert.EndsWith(".", pair.Value.Text);
            }
        }

        [Fact]
        public void KindIsADomainNotAValence()
        {
            // Opposites share a domain on purpose — the client colors by the tell language, not
            // by LexKind, and "good/bad" is exactly the table FieldFlavor refused to build.
            Assert.Equal(Lexicon.Of(StatusKind.AttackUp).Kind, Lexicon.Of(StatusKind.AttackDown).Kind);
            Assert.Equal(Lexicon.Of(StatusKind.Haste).Kind, Lexicon.Of(StatusKind.Slow).Kind);
            Assert.Equal(Lexicon.Of(StatusKind.DamageTakenDown).Kind, Lexicon.Of(StatusKind.DamageTakenUp).Kind);
        }

        [Fact]
        public void RiposteDefinesItsExactSpend()
        {
            Assert.Equal(
                "Spend 1 to Counter an incoming basic attack.",
                Lexicon.Of(StatusKind.CounterCharge).Text);
        }

        [Fact]
        public void UnknownValueFallsBackInsteadOfThrowing()
        {
            // A live fight must never crash on an un-authored id; the coverage tests above are
            // what keep this path unreachable in practice.
            var entry = Lexicon.Of((StatusKind)9999);
            Assert.False(string.IsNullOrEmpty(entry.Name));
            Assert.Equal("", entry.Text);
        }

        [Fact]
        public void EventTextHydratesStatusesAndCauses()
        {
            string Name(int id) => $"U{id}";

            // Raw enum names would read "+MultiShotRamp" and "(Dot)".
            var applied = new BattleEvent
            {
                Kind = EventKind.StatusApplied, Source = 1, Target = 2,
                Amount = 1, Aux = (int)StatusKind.MultiShotRamp,
            };
            Assert.Contains("Volley", EventText.Describe(applied, Name));
            Assert.DoesNotContain("MultiShotRamp", EventText.Describe(applied, Name));

            var expired = new BattleEvent
            {
                Kind = EventKind.StatusExpired, Target = 2, Aux = (int)StatusKind.DamageTakenUp,
            };
            Assert.Contains("Exposed", EventText.Describe(expired, Name));
            Assert.DoesNotContain("DamageTakenUp", EventText.Describe(expired, Name));

            var dot = new BattleEvent
            {
                Kind = EventKind.DamageDealt, Source = 1, Target = 2, Amount = 5, Cause = Cause.Dot,
            };
            Assert.Contains("Decay", EventText.Describe(dot, Name));
        }

        [Fact]
        public void NoStatusDescriptionLeaksAnEnumIdentifier()
        {
            // Multi-word enum names are the ones that would look like debug output on screen.
            string[] rawish = { "MultiShot", "DamageTaken", "CritMult", "OverhealTo", "SwingAmp", "CounterCharge", "AttackUp", "AttackDown" };
            foreach (var pair in Lexicon.Statuses)
                foreach (var raw in rawish)
                    Assert.False(pair.Value.Name.Contains(raw) || pair.Value.Text.Contains(raw),
                        $"{pair.Key} copy leaks the raw identifier '{raw}'");
        }
    }
}
