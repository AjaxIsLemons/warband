using System.Collections.Generic;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>One compact human line per event, for the debug Event viewer. Names resolve through
    /// a callback; the field semantics decoded match the comments on <see cref="EventKind"/>.</summary>
    public class EventTextTests
    {
        // A stable id→name map mirroring the client's callback (unknown → "#id", -1 → "storm").
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 0, "Shade" }, { 1, "Bulwark" }, { 2, "Sharpshot" }, { 3, "Cleric" },
            { 4, "Berserker" }, { 5, "Pyromancer" }, { 6, "Warden" }, { 7, "Juggernaut" },
            { 8, "Phalanx" },
        };
        private static string Name(int id) =>
            id < 0 ? "storm" : (Names.TryGetValue(id, out var n) ? n : $"#{id}");
        private static string Say(BattleEvent e) => EventText.Describe(e, Name);

        [Fact]
        public void DamageCarriesCritCauseAndAbsorbed()
        {
            var crit = new BattleEvent { Kind = EventKind.DamageDealt, Source = 2, Target = 1, Amount = 18, Cause = Cause.Attack, Crit = true };
            Assert.Equal("Damage 18 crit: Sharpshot → Bulwark", Say(crit));

            var burn = new BattleEvent { Kind = EventKind.DamageDealt, Source = 5, Target = 1, Amount = 6, Cause = Cause.Burn };
            Assert.Equal("Damage 6 (Burn): Pyromancer → Bulwark", Say(burn));

            // shield-absorbed (Aux>0) is appended at the end of the line
            var soaked = new BattleEvent { Kind = EventKind.DamageDealt, Source = 2, Target = 1, Amount = 30, Aux = 12, Cause = Cause.Attack };
            Assert.Equal("Damage 30: Sharpshot → Bulwark [12 absorbed]", Say(soaked));
        }

        [Fact]
        public void DeathNamesKillerAndOverkillAndStorm()
        {
            var slain = new BattleEvent { Kind = EventKind.Death, Target = 1, Source = 4, Amount = 17 };
            Assert.Equal("DEATH: Bulwark (by Berserker, overkill 17)", Say(slain));

            // no overkill → no tail
            var clean = new BattleEvent { Kind = EventKind.Death, Target = 1, Source = 4, Amount = 0 };
            Assert.Equal("DEATH: Bulwark (by Berserker)", Say(clean));

            // storm kill: Source < 0
            var stormed = new BattleEvent { Kind = EventKind.Death, Target = 0, Source = -1, Amount = 3 };
            Assert.Equal("DEATH: Shade (by storm, overkill 3)", Say(stormed));
        }

        [Fact]
        public void AttackAppendsCauseOnlyWhenNotPlainAttack()
        {
            var plain = new BattleEvent { Kind = EventKind.Attack, Source = 0, Target = 1, Cause = Cause.Attack };
            Assert.Equal("Attack: Shade → Bulwark", Say(plain));

            var counter = new BattleEvent { Kind = EventKind.Attack, Source = 8, Target = 0, Cause = Cause.Counter };
            Assert.Equal("Attack (Counter): Phalanx → Shade", Say(counter));
        }

        [Fact]
        public void StatusApplyAndExpireShowKindMagnitudeAndApplier()
        {
            var apply = new BattleEvent { Kind = EventKind.StatusApplied, Source = 6, Target = 7, Amount = 2, Aux = (int)StatusKind.Taunt };
            Assert.Equal("+Taunt ×2: Warden → Juggernaut", Say(apply));

            // magnitude 1 is the implicit default → no ×N
            var single = new BattleEvent { Kind = EventKind.StatusApplied, Source = 6, Target = 7, Amount = 1, Aux = (int)StatusKind.Root };
            Assert.Equal("+Root: Warden → Juggernaut", Say(single));

            // expiry carries no applier
            var expire = new BattleEvent { Kind = EventKind.StatusExpired, Target = 0, Amount = 0, Aux = (int)StatusKind.Phase };
            Assert.Equal("-Phase: Shade", Say(expire));
        }

        [Fact]
        public void ShieldAndManaAreSigned()
        {
            var shield = new BattleEvent { Kind = EventKind.ShieldChanged, Source = 3, Target = 8, Amount = 30 };
            Assert.Equal("Shield +30: Cleric → Phalanx", Say(shield));

            // self-shield (source==target) drops the arrow
            var self = new BattleEvent { Kind = EventKind.ShieldChanged, Source = 8, Target = 8, Amount = -10 };
            Assert.Equal("Shield -10: Phalanx", Say(self));

            var mana = new BattleEvent { Kind = EventKind.ManaChanged, Target = 1, Amount = 10 };
            Assert.Equal("Mana +10: Bulwark", Say(mana));
        }

        [Fact]
        public void HealAndCastReadCleanly()
        {
            var heal = new BattleEvent { Kind = EventKind.Heal, Source = 3, Target = 4, Amount = 9 };
            Assert.Equal("Heal 9: Cleric → Berserker", Say(heal));

            var cast = new BattleEvent { Kind = EventKind.Cast, Source = 5 };
            Assert.Equal("Cast: Pyromancer", Say(cast));
        }

        [Fact]
        public void FieldShowsFlavorRadiusWallAndAura()
        {
            // static hazard, radius 2 (Aux=-1 → not attached)
            var hazard = new BattleEvent { Kind = EventKind.FieldCreated, Source = 5, Target = 20, Amount = 0, Aux = -1, Aux2 = 2, Aux3 = (int)FieldFlavor.Hazard };
            Assert.Equal("Field Hazard r2: Pyromancer", Say(hazard));

            // a plain barrier (wall, Neutral flavor)
            var wall = new BattleEvent { Kind = EventKind.FieldCreated, Source = 1, Target = 21, Amount = 1, Aux = -1, Aux2 = 0, Aux3 = (int)FieldFlavor.Neutral };
            Assert.Equal("Field wall r0: Bulwark", Say(wall));

            // an aura (attached to a unit) boon
            var aura = new BattleEvent { Kind = EventKind.FieldCreated, Source = 3, Target = 22, Amount = 0, Aux = 3, Aux2 = 1, Aux3 = (int)FieldFlavor.Boon };
            Assert.Equal("Field Boon r1 aura: Cleric", Say(aura));

            Assert.Equal("Field hex (2,3)", Say(new BattleEvent { Kind = EventKind.FieldHex, Target = 20, Amount = 2, Aux = 3 }));
            Assert.Equal("Field expired", Say(new BattleEvent { Kind = EventKind.FieldExpired, Target = 20 }));
        }

        [Fact]
        public void BlockedNamesTheWallHex()
        {
            var e = new BattleEvent { Kind = EventKind.AttackBlocked, Source = 2, Target = 2, Amount = 2, Aux = 2 };
            Assert.Equal("BLOCKED: Sharpshot → Sharpshot (wall at 2,2)", Say(e));
        }

        [Fact]
        public void MoveLeapCheatDeathAndControlEvents()
        {
            Assert.Equal("Move: Shade → (2,3)", Say(new BattleEvent { Kind = EventKind.Move, Source = 0, Amount = 2, Aux = 3 }));
            // Both endpoints: a leap now says where it came FROM too, because the renderer arcs
            // between them and a reader debugging a leap wants the same pair.
            Assert.Equal("Leap: Shade (1,2) → (3,4)",
                Say(new BattleEvent { Kind = EventKind.Leap, Source = 0, Target = 1, Amount = 3, Aux = 4, Aux2 = 1, Aux3 = 2 }));
            Assert.Equal("CHEAT DEATH: Berserker", Say(new BattleEvent { Kind = EventKind.CheatDeath, Target = 4 }));
            Assert.Equal("Storm tick", Say(new BattleEvent { Kind = EventKind.StormTick }));
            Assert.Equal("Battle start", Say(new BattleEvent { Kind = EventKind.BattleStart }));
            Assert.Equal("Battle end", Say(new BattleEvent { Kind = EventKind.End }));
        }

        [Fact]
        public void NameCallbackFallbacksForNegativeAndUnknownIds()
        {
            // -1 killer resolves via the callback to "storm"; an unmapped id reads "#id"
            var dmg = new BattleEvent { Kind = EventKind.DamageDealt, Source = -1, Target = 99, Amount = 4, Cause = Cause.Storm };
            Assert.Equal("Damage 4 (Storm): storm → #99", Say(dmg));

            // a callback that returns null falls back to "?" inside Describe
            var e = new BattleEvent { Kind = EventKind.Attack, Source = 0, Target = 1, Cause = Cause.Attack };
            Assert.Equal("Attack: ? → ?", EventText.Describe(e, _ => null!));
        }

        [Fact]
        public void IsNoiseCoversTheSpamKinds()
        {
            Assert.True(EventText.IsNoise(new BattleEvent { Kind = EventKind.Move }));
            Assert.True(EventText.IsNoise(new BattleEvent { Kind = EventKind.ManaChanged }));
            Assert.True(EventText.IsNoise(new BattleEvent { Kind = EventKind.FieldHex }));
            Assert.True(EventText.IsNoise(new BattleEvent { Kind = EventKind.BattleStart }));

            Assert.False(EventText.IsNoise(new BattleEvent { Kind = EventKind.DamageDealt }));
            Assert.False(EventText.IsNoise(new BattleEvent { Kind = EventKind.Death }));
            Assert.False(EventText.IsNoise(new BattleEvent { Kind = EventKind.StatusApplied }));
            Assert.False(EventText.IsNoise(new BattleEvent { Kind = EventKind.FieldCreated }));
        }
    }
}
