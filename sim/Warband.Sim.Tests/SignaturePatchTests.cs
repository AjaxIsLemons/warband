using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// Signature patching (2026-07-25 systems review §4). Overrides are last-wins, so a crown
    /// silently erased an amplifier's texture and "the same thing, bigger" could only be authored
    /// by copy-pasting a whole effect list with one number changed. A patch modifies the signature
    /// it inherits instead, so B-fork → A-amplifier → S-crown composes.
    /// </summary>
    public class SignaturePatchTests
    {
        private static ChassisDef Caster() => new ChassisDef
        {
            Name = "Caster", MaxHp = 100, ManaMax = 20,
            StarterWeapon = new WeaponDef { Name = "Staff", Damage = 8, Interval = 10, Range = 3 },
            Signature =
            {
                new EffectDef
                {
                    Kind = EffectKind.Damage, Amount = 10,
                    Select = new Selector { Kind = SelKind.EnemiesWithin, Range = 1 },
                },
            },
        };

        private static SpecNode Node(string name, SignaturePatch patch) =>
            new SpecNode { Name = name, SignaturePatch = patch };

        [Fact]
        public void RadiusDeltaGrowsRadiusSelectorsOnly()
        {
            var chassis = Caster();
            chassis.Signature.Add(new EffectDef
            {
                Kind = EffectKind.GrantShield, Amount = 20,
                Select = new Selector { Kind = SelKind.Self },   // not a radius — must not move
            });

            var def = Loadout.Compose(chassis,
                nodes: new[] { Node("bigger", new SignaturePatch { RadiusDelta = 1 }) }).Def;

            Assert.Equal(2, def.Signature[0].Select.Range);
            Assert.Equal(0, def.Signature[1].Select.Range);
        }

        /// <summary>Radius and line length are both Selector.Range but mean different things, so
        /// they get different knobs — a board-length line (Range 0) bumped by a radius delta would
        /// silently collapse to a single hex.</summary>
        [Fact]
        public void LineRangeIsSetOutrightAndRadiusDeltaLeavesLinesAlone()
        {
            var chassis = Caster();
            chassis.Signature[0].Select = new Selector { Kind = SelKind.EnemiesOnLineThroughTarget, Range = 3 };

            var widened = Loadout.Compose(chassis,
                nodes: new[] { Node("wider", new SignaturePatch { RadiusDelta = 1 }) }).Def;
            Assert.Equal(3, widened.Signature[0].Select.Range);   // untouched

            var lengthened = Loadout.Compose(chassis,
                nodes: new[] { Node("longer", new SignaturePatch { LineRange = 0 }) }).Def;
            Assert.Equal(0, lengthened.Signature[0].Select.Range); // board-length
        }

        [Fact]
        public void AmountPctScalesAndRepeatDuplicatesTheWholeList()
        {
            var def = Loadout.Compose(Caster(),
                nodes: new[] { Node("twice", new SignaturePatch { AmountPct = 175, Repeat = 2 }) }).Def;

            Assert.Equal(2, def.Signature.Count);
            Assert.All(def.Signature, e => Assert.Equal(17, e.Amount));   // 10 × 175%, integer math
        }

        [Fact]
        public void FieldKnobsReachIntoTheGlyphAndInheritAcrossNodes()
        {
            var chassis = Caster();
            chassis.Signature[0] = new EffectDef
            {
                Kind = EffectKind.CreateField,
                Select = new Selector { Kind = SelKind.CurrentTarget },
                Field = new FieldDef { Radius = 1, Ticks = 80 },
            };

            // The Inferno → Everburn shape: the A node owns the radius, the S node owns the
            // duration, and neither has to restate the other's number.
            var def = Loadout.Compose(chassis, nodes: new[]
            {
                Node("inferno", new SignaturePatch { FieldRadius = 2 }),
                Node("everburn", new SignaturePatch { FieldTicks = -1 }),
            }).Def;

            Assert.Equal(2, def.Signature[0].Field!.Radius);
            Assert.Equal(-1, def.Signature[0].Field!.Ticks);
        }

        /// <summary>The wart this model was built to kill: Sarissa's crown used to REPLACE Deep
        /// Thrust's signature, so "Breach the Line" kept the board-length lunge and silently lost
        /// the escalation the A node was picked for.</summary>
        [Fact]
        public void ACrownNoLongerErasesAnAmplifier()
        {
            var chassis = Caster();
            chassis.Signature[0].Select = new Selector { Kind = SelKind.EnemiesOnLineThroughTarget, Range = 3 };

            var def = Loadout.Compose(chassis, nodes: new[]
            {
                Node("lancer", new SignaturePatch { LineRange = 4 }),
                Node("deepthrust", new SignaturePatch { Escalate = 30 }),
                Node("sarissa", new SignaturePatch { LineRange = 0 }),
            }).Def;

            Assert.Equal(0, def.Signature[0].Select.Range);          // board-length, from the crown
            Assert.Equal(30, def.Signature[0].EscalatePctPerIndex);  // AND still escalating
        }

        [Fact]
        public void AnOverrideResetsTheBaseAndLaterPatchesRideOnTop()
        {
            var def = Loadout.Compose(Caster(), nodes: new[]
            {
                new SpecNode
                {
                    Name = "fork",
                    SignatureOverride = new List<EffectDef>
                    {
                        new EffectDef
                        {
                            Kind = EffectKind.Heal, Amount = 20,
                            Select = new Selector { Kind = SelKind.AlliesWithin, Range = 1 },
                        },
                    },
                },
                Node("amplifier", new SignaturePatch { RadiusDelta = 1 }),
            }).Def;

            Assert.Single(def.Signature);
            Assert.Equal(EffectKind.Heal, def.Signature[0].Kind);
            Assert.Equal(2, def.Signature[0].Select.Range);
        }

        /// <summary>The composer patches effects in place, and every signature starts life pointing
        /// at the STATIC content catalog — so a missing clone would rewrite the kit for every later
        /// composition in the process. This is the test that would have caught that.</summary>
        [Fact]
        public void PatchingDoesNotMutateTheSharedChassisCatalog()
        {
            var chassis = Caster();
            var patch = new[] { Node("bigger", new SignaturePatch { RadiusDelta = 3, AmountPct = 200 }) };

            var first = Loadout.Compose(chassis, nodes: patch).Def;
            var plain = Loadout.Compose(chassis).Def;
            var second = Loadout.Compose(chassis, nodes: patch).Def;

            Assert.Equal(4, first.Signature[0].Select.Range);
            Assert.Equal(1, plain.Signature[0].Select.Range);    // the catalog is untouched
            Assert.Equal(10, plain.Signature[0].Amount);
            Assert.Equal(4, second.Signature[0].Select.Range);   // and patching is idempotent
            Assert.Equal(1, chassis.Signature[0].Select.Range);
        }

        // ---- the behavior hooks a node may now carry ----

        [Fact]
        public void ANodeMayReAimAndRepositionTheUnit()
        {
            var def = Loadout.Compose(Caster(), nodes: new[]
            {
                new SpecNode { Name = "backline", TargetPref = TargetPref.Farthest, Standoff = 3 },
            }).Def;

            Assert.Equal(TargetPref.Farthest, def.TargetPref);
            Assert.Equal(3, def.Standoff);
        }

        [Fact]
        public void ChassisBehaviorSurvivesWhenNoNodeOverridesIt()
        {
            var chassis = Caster();
            chassis.TargetPref = TargetPref.LowestHp;
            chassis.Standoff = 2;
            chassis.MoveInterval = 3;

            var def = Loadout.Compose(chassis, nodes: new[] { Node("noop", new SignaturePatch()) }).Def;

            Assert.Equal(TargetPref.LowestHp, def.TargetPref);
            Assert.Equal(2, def.Standoff);
            Assert.Equal(3, def.MoveInterval);
        }
    }
}
