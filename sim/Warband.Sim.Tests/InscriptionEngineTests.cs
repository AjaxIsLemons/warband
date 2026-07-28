using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// ADR 0026's engine machinery: the once-per-root guard, EveryN counters with wire-visible
    /// progress, the adjacent-to-ally opener filter, the HealToShield Paradox rewrite, and the
    /// On==TriggerFired hook Living Inscription rides. Each mechanism gets the behavioral test
    /// that would catch its specific regression.
    /// </summary>
    public class InscriptionEngineTests
    {
        private static UnitDef Fighter(string name, int hp = 100, int attack = 10) => new UnitDef
        {
            Name = name, MaxHp = hp, Attack = attack, AttackInterval = 10, Range = 1,
            MoveInterval = 4, ManaMax = 0,
        };

        private static UnitState U(int id, int team, UnitDef def, int row, int col) =>
            new UnitState { Id = id, Team = team, Def = def, Hp = def.MaxHp, Pos = Hex.FromRowCol(row, col) };

        /// <summary>A team rule that deals damage on DamageDealt — the canonical self-feeding
        /// cascade the guard exists for.</summary>
        private static Trigger EchoRule(bool guarded) => new Trigger
        {
            On = EventKind.DamageDealt, OncePerRoot = guarded, RuleId = "insc.echo",
            Do = { new EffectDef { Kind = EffectKind.Damage, Amount = 1,
                                   Select = new Selector { Kind = SelKind.EventTarget } } },
        };

        [Fact]
        public void OncePerRootStopsACascadeFromRefeedingItself()
        {
            // Guarded, the rule engages exactly once per root event: every announcement it makes
            // sits at depth 1, directly under a root, never riding its own output deeper.
            var result = new Battle(new[]
            {
                U(1, 0, Fighter("hitter"), 1, 1),
                U(2, 1, Fighter("wall", hp: 4000), 1, 2),
            }, new[] { (0, EchoRule(guarded: true)) }, seed: 7).Run();

            int idx = result.RuleIds.IndexOf("insc.echo");
            var fires = result.Events.Where(e => e.Kind == EventKind.TriggerFired && e.Aux == idx).ToList();
            Assert.NotEmpty(fires);
            Assert.All(fires, e => Assert.Equal(1, e.Depth));
        }

        [Fact]
        public void WithoutTheGuardTheSameRuleRidesItsOwnOutput()
        {
            // The control: proves the previous test detects the echo it claims to prevent.
            var result = new Battle(new[]
            {
                U(1, 0, Fighter("hitter"), 1, 1),
                U(2, 1, Fighter("wall", hp: 4000), 1, 2),
            }, new[] { (0, EchoRule(guarded: false)) }, seed: 7).Run();

            int idx = result.RuleIds.IndexOf("insc.echo");
            Assert.Contains(result.Events,
                e => e.Kind == EventKind.TriggerFired && e.Aux == idx && e.Depth > 1);
        }

        [Fact]
        public void EveryNCountsOnTheWireAndFiresOnTheNth()
        {
            var counter = new Trigger
            {
                On = EventKind.DamageDealt, EveryN = 3, RuleId = "insc.thirdchime",
                When = { new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack } },
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 5,
                                       Select = new Selector { Kind = SelKind.Self } } },
            };
            var result = new Battle(new[]
            {
                U(1, 0, Fighter("hitter"), 1, 1),
                U(2, 1, Fighter("wall", hp: 4000, attack: 0), 1, 2),
            }, new[] { (0, counter) }, seed: 7).Run();

            int idx = result.RuleIds.IndexOf("insc.thirdchime");
            var pips = result.Events.Where(e => e.Kind == EventKind.RuleProgress && e.Aux == idx).ToList();
            var fires = result.Events.Where(e => e.Kind == EventKind.TriggerFired && e.Aux == idx).ToList();

            Assert.NotEmpty(fires);
            // Pips run 1,2,3, 1,2,3, … (Amount is SET, never accumulated), N rides Aux2,
            // and the rule fires exactly on each full count.
            for (int i = 0; i < pips.Count; i++) Assert.Equal(i % 3 + 1, pips[i].Amount);
            Assert.All(pips, e => Assert.Equal(3, e.Aux2));
            Assert.Equal(pips.Count / 3, fires.Count);
        }

        [Fact]
        public void CounterProgressFoldsIntoPlaybackForTheBadgeRail()
        {
            var counter = new Trigger
            {
                On = EventKind.DamageDealt, EveryN = 5, RuleId = "insc.pips",
                When = { new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack } },
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 1,
                                       Select = new Selector { Kind = SelKind.Self } } },
            };
            var result = new Battle(new[]
            {
                U(1, 0, Fighter("hitter"), 1, 1),
                U(2, 1, Fighter("wall", hp: 4000, attack: 0), 1, 2),
            }, new[] { (0, counter) }, seed: 7).Run();

            int idx = result.RuleIds.IndexOf("insc.pips");
            var second = result.Events.Where(e => e.Kind == EventKind.RuleProgress && e.Aux == idx)
                                      .Skip(1).First();   // the "2 of 5" moment

            var fold = PlaybackState.From(result.InitialUnits, result.RuleIds);
            fold.AdvanceToTick(result.Events, second.Tick);
            Assert.True(fold.RuleCounters.TryGetValue(idx, out var c));
            Assert.Equal(5, c.N);
            Assert.InRange(c.Progress, 1, 5);   // scrubbing reconstructs pips, not just the flash
        }

        [Fact]
        public void AdjacentToAllyOpenerPicksOnlyMusteredPairs()
        {
            var opener = new Trigger
            {
                On = EventKind.BattleStart, RuleId = "insc.shoulder",
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 10,
                                       Select = new Selector { Kind = SelKind.AlliesWithin, Range = 99,
                                                               AdjacentToAlly = true } } },
            };
            var result = new Battle(new[]
            {
                U(1, 0, Fighter("a"), 1, 1),
                U(2, 0, Fighter("b"), 1, 2),      // beside a
                U(3, 0, Fighter("loner"), 4, 5),  // mustered alone
                U(4, 1, Fighter("enemy", hp: 500), 7, 1),
            }, new[] { (0, opener) }, seed: 7).Run();

            var opening = result.Events
                .Where(e => e.Kind == EventKind.ShieldChanged && e.Amount == 10)
                .Select(e => e.Target).ToList();
            Assert.Contains(1, opening);
            Assert.Contains(2, opening);
            Assert.DoesNotContain(3, opening);
        }

        [Fact]
        public void HealToShieldConvertsTheWholeHealAndEmitsNoHealEvent()
        {
            // The Bloodless Hour's shape: the status is on the unit fight-long; every heal —
            // not just overflow — arrives as Shield, and no Heal event exists for on-Heal
            // engines to ride. That silence is the Paradox's drawback and must be real.
            var def = Fighter("bloodless");
            def.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart, RuleId = "insc.bloodless",
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.HealToShield,
                                       Amount = 1, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } },
                       new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Regen,
                                       Amount = 5, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } } },
            });
            var result = new Battle(new[]
            {
                U(1, 0, def, 1, 1),
                U(2, 1, Fighter("enemy", hp: 400), 6, 1),
            }, seed: 7).Run();

            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Heal);
            Assert.Contains(result.Events, e => e.Kind == EventKind.ShieldChanged && e.Amount == 5);
        }

        [Fact]
        public void AnOnTriggerFiredHookRidesTeamRulesOncePerRoot()
        {
            // Living Inscription's exact shape (ADR 0026): wake on an Inscription's announcement,
            // gain Mana, at most once per root event. EventRuleIsTeamRule keeps it deaf to its own
            // announcement (a unit-rule index), so no equality here can hide an echo.
            var vespera = Fighter("vespera");
            vespera.ManaMax = 200;
            vespera.Triggers.Add(new Trigger
            {
                On = EventKind.TriggerFired, OncePerRoot = true, RuleId = "node.living",
                When = { new Cond { Kind = CondKind.EventRuleIsTeamRule } },
                Do = { new EffectDef { Kind = EffectKind.GrantMana, Amount = 10,
                                       Select = new Selector { Kind = SelKind.Self } } },
            });
            var teamRule = new Trigger
            {
                On = EventKind.DamageDealt, OncePerRoot = true, RuleId = "insc.any",
                When = { new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack } },
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 1,
                                       Select = new Selector { Kind = SelKind.Self } } },
            };
            var result = new Battle(new[]
            {
                U(1, 0, vespera, 1, 1),
                U(2, 1, Fighter("wall", hp: 4000, attack: 0), 1, 2),
            }, new[] { (0, teamRule) }, seed: 7).Run();

            int teamIdx = result.RuleIds.IndexOf("insc.any");
            int hookIdx = result.RuleIds.IndexOf("node.living");
            int teamFires = result.Events.Count(e => e.Kind == EventKind.TriggerFired && e.Aux == teamIdx);
            int hookFires = result.Events.Count(e => e.Kind == EventKind.TriggerFired && e.Aux == hookIdx);

            Assert.True(teamFires > 0);
            Assert.Equal(teamFires, hookFires);
            // And the Mana genuinely arrived, attributed to the hook's owner as source.
            Assert.Contains(result.Events,
                e => e.Kind == EventKind.ManaChanged && e.Source == 1 && e.Amount == 10);
        }
    }
}
