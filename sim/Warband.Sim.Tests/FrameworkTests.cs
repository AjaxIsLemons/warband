using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>Trigger/status/cascade semantics — the ADR 0004 contract.</summary>
    public class FrameworkTests
    {
        private static Trigger OnBattleStart(params EffectDef[] effects) => new Trigger
        {
            On = EventKind.BattleStart,
            Do = new List<EffectDef>(effects),
        };

        private static EffectDef StatusEffect(StatusKind kind, int mag, int ticks, SelKind sel, int range = 0) =>
            new EffectDef
            {
                Kind = EffectKind.ApplyStatus, Status = kind, Amount = mag, StatusTicks = ticks,
                Select = new Selector { Kind = sel, Range = range },
            };

        [Fact]
        public void SelfSustainingCascadeIsDepthBounded()
        {
            // "Haste applies Haste" — must terminate at MaxCascadeDepth, not hang.
            var def = BattleTests.Grunt();
            def.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Haste, 1, -1, SelKind.Self)));
            def.Triggers.Add(new Trigger
            {
                On = EventKind.StatusApplied,
                When = { new Cond { Kind = CondKind.TargetIsOwner } },
                Do = { StatusEffect(StatusKind.Haste, 1, -1, SelKind.Self) },
            });
            var result = new Battle(BattleTests.Duel(def, BattleTests.Grunt())).Run();
            int applied = result.Events.Count(e => e.Kind == EventKind.StatusApplied && e.Target == 0);
            Assert.InRange(applied, 1, Battle.MaxCascadeDepth + 1);
        }

        [Fact]
        public void StunnedUnitNeverActs()
        {
            var stunner = BattleTests.Grunt();
            stunner.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Stun, 0, -1, SelKind.NearestEnemy)));
            // Equal grunts otherwise; the permanently stunned one never swings.
            var result = new Battle(BattleTests.Duel(stunner, BattleTests.Grunt())).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Attack && e.Source == 1);
        }

        [Fact]
        public void ShieldAbsorbsBeforeHp()
        {
            var shielded = BattleTests.Grunt(hp: 50);
            shielded.Triggers.Add(OnBattleStart(new EffectDef
            {
                Kind = EffectKind.GrantShield, Amount = 1000,
                Select = new Selector { Kind = SelKind.Self },
            }));
            var result = new Battle(BattleTests.Duel(shielded, BattleTests.Grunt(hp: 100))).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Target == 0 && e.Aux > 0);
        }

        [Fact]
        public void HasteFlipsAMirror()
        {
            var fast = BattleTests.Grunt();
            fast.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Haste, Battle.FP, -1, SelKind.Self)));
            var result = new Battle(BattleTests.Duel(fast, BattleTests.Grunt())).Run();
            Assert.Equal(Winner.Team0, result.Winner); // double attack speed wins the mirror
        }

        [Fact]
        public void SilenceBlocksCastingAndManaGain()
        {
            var caster = BattleTests.Grunt(hp: 200);
            caster.ManaMax = 20;
            caster.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 100 });
            var silencer = BattleTests.Grunt(hp: 200);
            silencer.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Silence, 0, -1, SelKind.NearestEnemy)));
            var result = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, silencer, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, caster, Hex.FromRowCol(4, 2)),
            }).Run();
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Cast && e.Source == 1);
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.ManaChanged && e.Target == 1);
        }

        [Fact]
        public void DisarmBlocksAutosButCastingContinues()
        {
            var disarmed = BattleTests.Grunt(hp: 500);
            disarmed.ManaMax = 15;
            disarmed.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 30 });
            var disarmer = BattleTests.Grunt(hp: 500, atk: 5);
            disarmer.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Disarm, 0, -1, SelKind.NearestEnemy)));
            var result = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, disarmer, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, disarmed, Hex.FromRowCol(4, 2)),
            }).Run();
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Attack && e.Source == 1);
            Assert.Contains(result.Events, e => e.Kind == EventKind.Cast && e.Source == 1);
        }

        [Fact]
        public void DotTicksAndKills()
        {
            var afflictor = BattleTests.Pacifist(200);
            afflictor.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Dot, 10, -1, SelKind.NearestEnemy)));
            var result = new Battle(BattleTests.Duel(afflictor, BattleTests.Pacifist(50))).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Dot);
            Assert.True(result.EndTick < Battle.OvertimeStartTick); // DoT killed, not the storm
        }

        [Fact]
        public void RegenOuthealsSmallDamage()
        {
            var sustained = BattleTests.Grunt(hp: 100, atk: 10);
            sustained.Triggers.Add(OnBattleStart(StatusEffect(StatusKind.Regen, 5, -1, SelKind.Self)));
            var result = new Battle(BattleTests.Duel(sustained, BattleTests.Grunt(hp: 100, atk: 4))).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Contains(result.Events, e => e.Kind == EventKind.Heal && e.Target == 0);
        }

        [Fact]
        public void CounterReactionPunishesAttacker()
        {
            // The guardian pattern (ADR 0003): when I am hit by an attack → strike back.
            var guard = BattleTests.Grunt(hp: 200, atk: 5);
            guard.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Damage, Amount = 7,
                    Select = new Selector { Kind = SelKind.EventSource },
                } },
            });
            var result = new Battle(BattleTests.Duel(BattleTests.Grunt(hp: 200), guard)).Run();
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Trigger && e.Source == 1 && e.Target == 0);
            Assert.Equal(Winner.Team1, result.Winner); // counters flip an otherwise-losing matchup
        }

        [Fact]
        public void TeamTriggerFiresOncePerTeam()
        {
            var banner = new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.GrantShield, Amount = 40,
                    Select = new Selector { Kind = SelKind.AlliesWithin, Range = 99 },
                } },
            };
            var result = new Battle(
                BattleTests.Duel(BattleTests.Grunt(hp: 90), BattleTests.Grunt(hp: 100)),
                new[] { (0, banner) }).Run();
            // 40 shield on the weaker side flips the mirror-ish matchup.
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Contains(result.Events, e => e.Kind == EventKind.ShieldChanged && e.Target == 0);
        }
    }
}
