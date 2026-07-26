using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// ADR 0008 always specified `contentVersion` and nothing ever built it. These pin the two
    /// properties that make it worth having:
    ///
    ///   1. **It is STABLE** — same content, same fingerprint, in every process and on every
    ///      platform. If it drifts, every save spuriously refuses to load.
    ///   2. **It catches RETUNES, at any depth** — the failure mode no id check can see. A hash that
    ///      only covered the scalar stat block would be worse than none: it would promise a
    ///      guarantee it does not keep.
    /// </summary>
    public class ContentHashTests
    {
        [Fact]
        public void TheAlgorithmIsPinnedToAKnownValue()
        {
            // Computed independently (FNV-1a-64 over the documented byte order), NOT copied from a
            // run of this code. This is what fails the moment somebody "simplifies" the hasher to
            // string.GetHashCode() — which .NET randomizes PER PROCESS, so saves would refuse to
            // load after a restart and the bug would look like save corruption.
            string hex = new ContentHash()
                .Add("warband").Add(42).Add("").Add(-7)
                .Hex;
            Assert.Equal("8b280131e527153f", hex);
        }

        [Fact]
        public void NullAndEmptyStringsAreDistinguishable()
        {
            // WeaponId null ("chassis starter") is different content from WeaponId "".
            Assert.NotEqual(new ContentHash().Add((string?)null).Hex, new ContentHash().Add("").Hex);
        }

        [Fact]
        public void OrderIsPartOfTheFingerprint()
        {
            // Deliberate: a reordered registry can change tell-match ties and shop draws, so it is
            // a content change, not a no-op.
            Assert.NotEqual(new ContentHash().Add("a").Add("b").Hex,
                            new ContentHash().Add("b").Add("a").Hex);
        }

        [Fact]
        public void HashingTheSameUnitTwiceAgrees()
        {
            Assert.Equal(new ContentHash().AddUnit(Sample()).Hex,
                         new ContentHash().AddUnit(Sample()).Hex);
        }

        [Theory]
        // Surface stats — the easy cases.
        [InlineData("attack")]
        [InlineData("hp")]
        [InlineData("interval")]
        // The cases that matter: numbers buried in the effect graph, where a shallow hash would
        // report "same content" while the fight resolves completely differently.
        [InlineData("signature-amount")]
        [InlineData("trigger-effect-amount")]
        [InlineData("trigger-condition-amount")]
        [InlineData("selector-range")]
        [InlineData("field-radius")]
        [InlineData("field-pulse-amount")]
        [InlineData("statrule-amount")]
        [InlineData("status-ticks")]
        public void ARetuneAtAnyDepthMovesTheFingerprint(string what)
        {
            var before = new ContentHash().AddUnit(Sample()).Hex;
            var tweaked = Sample();
            switch (what)
            {
                case "attack": tweaked.Attack += 1; break;
                case "hp": tweaked.MaxHp += 1; break;
                case "interval": tweaked.AttackInterval += 1; break;
                case "signature-amount": tweaked.Signature[0].Amount += 1; break;
                case "trigger-effect-amount": tweaked.Triggers[0].Do[0].Amount += 1; break;
                case "trigger-condition-amount": tweaked.Triggers[0].When[0].Amount += 1; break;
                case "selector-range": tweaked.Signature[0].Select.Range += 1; break;
                case "field-radius": tweaked.Signature[1].Field!.Radius += 1; break;
                case "field-pulse-amount": tweaked.Signature[1].Field!.Pulse[0].Amount += 1; break;
                case "statrule-amount": tweaked.StatRules[0].Amount += 1; break;
                case "status-ticks": tweaked.Triggers[0].Do[0].StatusTicks += 1; break;
            }
            Assert.NotEqual(before, new ContentHash().AddUnit(tweaked).Hex);
        }

        /// <summary>A unit with something in every channel the hasher walks: a signature with a
        /// selector and a field, a conditional trigger, and a stat rule.</summary>
        private static UnitDef Sample() => new UnitDef
        {
            Name = "sample", ChassisId = "pyromancer", WeaponName = "Ashwood Staff",
            MaxHp = 100, Attack = 12, AttackInterval = 10, Range = 3, MoveInterval = 5,
            ManaMax = 20,
            Signature =
            {
                new EffectDef
                {
                    Kind = EffectKind.Damage,
                    Amount = 30,
                    Select = new Selector { Kind = SelKind.EnemiesWithin, Range = 2 },
                },
                new EffectDef
                {
                    Kind = EffectKind.CreateField,
                    Select = new Selector { Kind = SelKind.CurrentTarget },
                    Field = new FieldDef
                    {
                        Radius = 1, Ticks = 60, PulseAffects = Affects.Enemies,
                        Pulse =
                        {
                            new EffectDef
                            {
                                Kind = EffectKind.ApplyStatus,
                                Status = StatusKind.Burn, Amount = 2,
                                Select = new Selector { Kind = SelKind.Self },
                            },
                        },
                    },
                },
            },
            Triggers =
            {
                new Trigger
                {
                    On = EventKind.DamageDealt,
                    When = { new Cond { Kind = CondKind.TargetBelowHpPct, Amount = 50 } },
                    Do =
                    {
                        new EffectDef
                        {
                            Kind = EffectKind.ApplyStatus,
                            Status = StatusKind.Slow, Amount = 200, StatusTicks = 30,
                            Select = new Selector { Kind = SelKind.EventTarget },
                        },
                    },
                },
            },
            StatRules =
            {
                new StatRule { Stat = StatKind.AttackFlat, Amount = 5, ScaleBy = StatScale.None },
            },
        };
    }
}
