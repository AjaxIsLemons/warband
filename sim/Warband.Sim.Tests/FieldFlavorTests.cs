using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// FieldFlavor is what the renderer colors a glyph by. These pin the derivation against the
    /// shapes real content actually authors (Pyromancer Blaze, Cleric healing ground, Banneret
    /// Dread Presence, Phalanx walls) plus the ambiguous cases, so a content edit that changes a
    /// glyph's meaning shows up here rather than as a silently miscolored zone.
    /// </summary>
    public class FieldFlavorTests
    {
        [Fact]
        public void PulseAimedAtEnemies_IsHazard()   // Pyromancer Blaze: Burn to occupants
        {
            var blaze = new FieldDef
            {
                Radius = 1, Ticks = 80, PulseAffects = Affects.Enemies,
                Pulse = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Burn, Amount = 2 } },
            };
            Assert.Equal(FieldFlavor.Hazard, blaze.Flavor());
        }

        [Fact]
        public void PulseAimedAtAllies_IsBoon()      // Cleric Grace: healing ground
        {
            var grace = new FieldDef
            {
                Radius = 1, Ticks = 40, PulseAffects = Affects.Allies,
                Pulse = { new EffectDef { Kind = EffectKind.Heal, Amount = 4 } },
            };
            Assert.Equal(FieldFlavor.Boon, grace.Flavor());
        }

        [Fact]
        public void PresenceOnEnemies_IsDebuff()     // Banneret Dread Presence: Slow aura
        {
            var dread = new FieldDef
            {
                AttachToOwner = true, Radius = 1, Ticks = -1,
                PresenceAffects = Affects.Enemies, Presence = { (StatusKind.Slow, 200) },
            };
            Assert.Equal(FieldFlavor.Debuff, dread.Flavor());
        }

        [Fact]
        public void PresenceOnAllies_IsBuff()
        {
            var rally = new FieldDef
            {
                AttachToOwner = true, Radius = 1, Ticks = -1,
                PresenceAffects = Affects.Allies, Presence = { (StatusKind.Haste, 200) },
            };
            Assert.Equal(FieldFlavor.Buff, rally.Flavor());
        }

        [Fact]
        public void BareWall_IsNeutral_AndFlavorIsOrthogonalToWallness()
        {
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            Assert.Equal(FieldFlavor.Neutral, wall.Flavor());

            // A burning wall is BOTH — the renderer draws a barrier and tints it hazardous.
            var burningWall = new FieldDef
            {
                Radius = 0, Ticks = -1, IsWall = true, PulseAffects = Affects.Enemies,
                Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 3 } },
            };
            Assert.True(burningWall.IsWall);
            Assert.Equal(FieldFlavor.Hazard, burningWall.Flavor());
        }

        [Fact]
        public void ProjectileOnlyField_IsNeutral()  // the "blessing" shape: no occupant rule at all
        {
            var blessing = new FieldDef
            {
                Radius = 0, Ticks = -1, ProjectileAffects = Affects.Allies, ProjectileBonus = 10,
            };
            Assert.Equal(FieldFlavor.Neutral, blessing.Flavor());
        }

        [Fact]
        public void PulseHittingEveryone_ReadsFromTheEffects()
        {
            var fire = new FieldDef   // environmental hazard: damages all comers
            {
                Radius = 1, Ticks = -1,
                Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 6 } },
            };
            Assert.Equal(FieldFlavor.Hazard, fire.Flavor());

            var font = new FieldDef   // a font anyone may drink from
            {
                Radius = 1, Ticks = -1,
                Pulse = { new EffectDef { Kind = EffectKind.GrantMana, Amount = 5 } },
            };
            Assert.Equal(FieldFlavor.Boon, font.Flavor());
        }

        [Fact]
        public void AllAffectingStatusPulse_StaysNeutralRatherThanGuessing()
        {
            var odd = new FieldDef
            {
                Radius = 1, Ticks = -1,
                Pulse = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Burn, Amount = 1 } },
            };
            Assert.Equal(FieldFlavor.Neutral, odd.Flavor());
        }

        [Fact]
        public void PulseOutranksPresence()
        {
            var both = new FieldDef
            {
                Radius = 1, Ticks = 50, PulseAffects = Affects.Enemies,
                Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 4 } },
                PresenceAffects = Affects.Allies, Presence = { (StatusKind.Haste, 100) },
            };
            Assert.Equal(FieldFlavor.Hazard, both.Flavor());
        }
    }
}
