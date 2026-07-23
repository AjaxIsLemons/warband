using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>The dive-campaign status laws: Burn decay, Taunt, Phase, CheatDeath,
    /// killer attribution, Execute, overheal→Shield, lifesteal, Detonate.</summary>
    public class DiveStatusTests
    {
        internal static Trigger AtStart(params EffectDef[] effects)
        {
            var t = new Trigger { On = EventKind.BattleStart };
            t.Do.AddRange(effects);
            return t;
        }

        internal static EffectDef Apply(StatusKind kind, int mag, SelKind sel, int ticks = -1, int swings = 0, int range = 0) =>
            new EffectDef
            {
                Kind = EffectKind.ApplyStatus, Status = kind, Amount = mag,
                StatusTicks = ticks, StatusSwings = swings,
                Select = new Selector { Kind = sel, Range = range },
            };

        private static UnitDef Rooted(UnitDef def)
        {
            def.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            return def;
        }

        // ---- The Burn law (Pyro dive): tick = stacks, then −1; one merged pool ----

        [Fact]
        public void BurnDecaysBazaarStyle()
        {
            var igniter = BattleTests.Pacifist(500);
            igniter.Triggers.Add(AtStart(Apply(StatusKind.Burn, 3, SelKind.NearestEnemy)));
            var result = new Battle(BattleTests.Duel(igniter, BattleTests.Pacifist(500))).Run();

            var ticks = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Burn && e.Target == 1)
                .Select(e => e.Amount).ToList();
            Assert.Equal(new List<int> { 3, 2, 1 }, ticks);
        }

        [Fact]
        public void BurnMergesIntoOnePool()
        {
            // Two appliers, 2 stacks each → ONE pool of 4: first tick deals 4, not 2+2.
            var igniter = BattleTests.Pacifist(500);
            igniter.Triggers.Add(AtStart(
                Apply(StatusKind.Burn, 2, SelKind.NearestEnemy),
                Apply(StatusKind.Burn, 2, SelKind.NearestEnemy)));
            var result = new Battle(BattleTests.Duel(igniter, BattleTests.Pacifist(500))).Run();

            var first = result.Events.First(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Burn);
            Assert.Equal(4, first.Amount);
        }

        [Fact]
        public void BurnAmpDoublesTicks()
        {
            var igniter = BattleTests.Pacifist(500);
            igniter.Triggers.Add(AtStart(
                Apply(StatusKind.Burn, 3, SelKind.NearestEnemy),
                Apply(StatusKind.BurnAmp, 100, SelKind.NearestEnemy)));
            var result = new Battle(BattleTests.Duel(igniter, BattleTests.Pacifist(500))).Run();

            var ticks = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Burn && e.Target == 1)
                .Select(e => e.Amount).ToList();
            Assert.Equal(new List<int> { 6, 4, 2 }, ticks);
        }

        // ---- Taunt (Bulwark dive): forced targeting + Silence behavior ----

        [Fact]
        public void TauntForcesTargetAndSilences()
        {
            // Taunter far away; a juicy decoy adjacent to the enemy. Taunted enemy must
            // walk past the decoy to the taunter, never cast, never gain mana.
            var taunter = BattleTests.Pacifist(2000);
            taunter.Triggers.Add(AtStart(Apply(StatusKind.Taunt, 0, SelKind.NearestEnemy)));

            var enemy = BattleTests.Grunt(hp: 300, atk: 5);
            enemy.ManaMax = 10;
            enemy.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 99 });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, taunter, Hex.FromRowCol(7, 5)),
                UnitState.Spawn(1, 0, BattleTests.Pacifist(300), Hex.FromRowCol(3, 2)), // decoy
                UnitState.Spawn(2, 1, enemy, Hex.FromRowCol(4, 2)),                     // spawns beside decoy
            };
            var result = new Battle(units).Run();

            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Attack && e.Source == 2 && e.Target == 1);
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Cast && e.Source == 2);
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.ManaChanged && e.Target == 2);
            Assert.Contains(result.Events, e => e.Kind == EventKind.Attack && e.Source == 2 && e.Target == 0);
        }

        // ---- Phase (Shade dive): untargetable + immune, attackers re-acquire ----

        [Fact]
        public void PhasedUnitIsImmuneAndDropped()
        {
            var phased = BattleTests.Pacifist(100);
            phased.Triggers.Add(AtStart(Apply(StatusKind.Phase, 0, SelKind.Self, ticks: 60)));

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, Rooted(phased), Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 4)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 300, atk: 8), Hex.FromRowCol(4, 2)), // adjacent to the phased one
            };
            var result = new Battle(units).Run();

            // While Phased: no damage lands on unit 0; the attacker goes for unit 1 instead.
            var firstHitOnPhased = result.Events.FirstOrDefault(e => e.Kind == EventKind.DamageDealt && e.Target == 0);
            var hitOnDecoy = result.Events.FirstOrDefault(e => e.Kind == EventKind.DamageDealt && e.Target == 1);
            Assert.NotNull(hitOnDecoy);
            Assert.True(firstHitOnPhased == null || firstHitOnPhased.Tick > 60,
                "phased unit was damaged inside its Phase window");
        }

        // ---- CheatDeath (Berserker dive) + killer attribution + overkill ----

        [Fact]
        public void CheatDeathSurvivesOnceAtOneHp()
        {
            var deathless = BattleTests.Pacifist(10);
            deathless.Triggers.Add(AtStart(Apply(StatusKind.CheatDeath, 0, SelKind.Self)));
            var result = new Battle(BattleTests.Duel(deathless, BattleTests.Grunt(hp: 300, atk: 25))).Run();

            var cheat = result.Events.Single(e => e.Kind == EventKind.CheatDeath);
            Assert.Equal(0, cheat.Target);
            Assert.Equal(1, cheat.PostHp);
            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 0);
            Assert.True(death.Tick >= cheat.Tick); // survived once, died to the next hit
        }

        [Fact]
        public void DeathCarriesKillerAndOverkill()
        {
            // 25 damage into 10 HP: killer = unit 1, overkill = 15.
            var result = new Battle(BattleTests.Duel(BattleTests.Pacifist(10), BattleTests.Grunt(hp: 300, atk: 25))).Run();
            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 0);
            Assert.Equal(1, death.Source);
            Assert.Equal(15, death.Amount);
        }

        [Fact]
        public void ExecuteKillsThroughShield()
        {
            var reaper = BattleTests.Grunt(hp: 300, atk: 10);
            reaper.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                    new Cond { Kind = CondKind.TargetBelowHpPct, Amount = 50 },
                },
                Do = { new EffectDef { Kind = EffectKind.Execute, Select = new Selector { Kind = SelKind.EventTarget } } },
            });
            var shielded = BattleTests.Pacifist(30);
            shielded.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.GrantShield, Amount = 500, Select = new Selector { Kind = SelKind.Self },
            }));
            var result = new Battle(BattleTests.Duel(reaper, shielded)).Run();

            // Shield absorbs the pokes, so HP never drops below 50% naturally — only the
            // Execute (HP+Shield in one blow) can kill. It must not have fired.
            Assert.Equal(Winner.Team0, result.Winner);
            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 1);
            Assert.True(death.Tick > 0);
        }

        [Fact]
        public void ExecuteFiresBelowThreshold()
        {
            var reaper = BattleTests.Grunt(hp: 300, atk: 10);
            reaper.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                    new Cond { Kind = CondKind.TargetBelowHpPct, Amount = 50 },
                },
                Do = { new EffectDef { Kind = EffectKind.Execute, Select = new Selector { Kind = SelKind.EventTarget } } },
            });
            // 30 HP victim: swing → 20 (67%), swing → 10 (33%, below half) → executed.
            var result = new Battle(BattleTests.Duel(reaper, BattleTests.Pacifist(30))).Run();
            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 1);
            var execute = result.Events.Single(e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Trigger && e.Target == 1);
            Assert.Equal(10, execute.Amount); // exactly the remaining HP
            Assert.Equal(execute.Tick, death.Tick);
        }

        // ---- Overheal → Shield (Crimson Tide, censer mastery) ----

        [Fact]
        public void OverhealConvertsToShield()
        {
            var tank = BattleTests.Pacifist(100);
            tank.Triggers.Add(AtStart(
                Apply(StatusKind.OverhealToShield, 0, SelKind.Self),
                Apply(StatusKind.Regen, 7, SelKind.Self)));
            var result = new Battle(BattleTests.Duel(tank, BattleTests.Pacifist(100))).Run();

            // Full HP the whole fight: every Regen pulse overflows into Shield.
            var shields = result.Events
                .Where(e => e.Kind == EventKind.ShieldChanged && e.Target == 0).ToList();
            Assert.NotEmpty(shields);
            Assert.All(shields, e => Assert.Equal(7, e.Amount));
        }

        // ---- Lifesteal (Bloodreaver) via PctOfEventAmount ----

        [Fact]
        public void LifestealHealsPctOfDamageDealt()
        {
            var reaver = BattleTests.Grunt(hp: 60, atk: 10);
            reaver.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Heal, PctOfEventAmount = 50,
                    Select = new Selector { Kind = SelKind.Self },
                } },
            });
            var result = new Battle(BattleTests.Duel(reaver, BattleTests.Grunt(hp: 200, atk: 8))).Run();

            var heals = result.Events
                .Where(e => e.Kind == EventKind.Heal && e.Target == 0 && e.Source == 0).ToList();
            Assert.NotEmpty(heals);
            Assert.All(heals, e => Assert.Equal(5, e.Amount)); // 50% of the 10-damage swing
        }

        // ---- Detonate (Starfall): consume the pool, damage per stack ----

        [Fact]
        public void DetonateScalesByBurnAndConsumesIt()
        {
            var caster = BattleTests.Pacifist(400);
            caster.ManaMax = 10;
            caster.Triggers.Add(AtStart(Apply(StatusKind.Burn, 50, SelKind.NearestEnemy)));
            caster.Signature.Add(new EffectDef
            {
                Kind = EffectKind.Damage, Amount = 2,
                ScaleByTargetStatus = true, ScaleStatus = StatusKind.Burn,
                Select = new Selector { Kind = SelKind.CurrentTarget },
            });
            caster.Signature.Add(new EffectDef
            {
                Kind = EffectKind.RemoveStatus, Status = StatusKind.Burn,
                Select = new Selector { Kind = SelKind.CurrentTarget },
            });

            var result = new Battle(BattleTests.Duel(caster, BattleTests.Grunt(hp: 3000, atk: 5))).Run();

            var cast = result.Events.First(e => e.Kind == EventKind.Cast && e.Source == 0);
            var nuke = result.Events.First(e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Ability && e.Source == 0);
            int poolAtCast = 50 - cast.Tick / Battle.PulseInterval; // one decay per pulse
            Assert.Equal(2 * poolAtCast, nuke.Amount);
            // Consumed: no Burn tick after the cast.
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Burn && e.Tick > cast.Tick);
        }
    }
}
