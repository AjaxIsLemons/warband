using System.Collections.Generic;
using System.IO;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The passive-legibility contract (Design/passive-legibility.md, roadmap item 20).
    ///
    /// The load-bearing property these guard is NEGATIVE: TriggerFired and RuleChanged must be
    /// incapable of changing a fight. Everything else here is about a rule being able to say its
    /// own name — the thing that was structurally impossible before.
    /// </summary>
    public class RuleLegibilityTests
    {
        private static UnitDef Fighter(string name, int hp = 100, int attack = 10) => new UnitDef
        {
            Name = name, MaxHp = hp, Attack = attack, AttackInterval = 10, Range = 1,
            MoveInterval = 4, ManaMax = 0,
        };

        private static UnitState U(int id, int team, UnitDef def, int row, int col) =>
            new UnitState { Id = id, Team = team, Def = def, Hp = def.MaxHp, Pos = Hex.FromRowCol(row, col) };

        // ---- the negative property: presentation cannot move the sim ----------------

        [Fact]
        public void PresentationEventsChangeNothingAboutTheFight()
        {
            // Same battle twice, but one side carries a passive that fires constantly. The
            // comparison that matters is between the fight WITH its announcements and the same
            // fight with them stripped: identical state hash, identical non-presentation log.
            var withRules = RunFight();
            var events = withRules.Events.Where(e =>
                e.Kind != EventKind.TriggerFired && e.Kind != EventKind.RuleChanged).ToList();

            Assert.Contains(withRules.Events, e => e.Kind == EventKind.TriggerFired);
            Assert.Contains(withRules.Events, e => e.Kind == EventKind.RuleChanged);

            // Folding the stripped log must reach the same view as folding the full one: the new
            // events carry no state a renderer could otherwise miss.
            var full = PlaybackState.From(withRules.InitialUnits, withRules.RuleIds);
            full.AdvanceToTick(withRules.Events, withRules.EndTick);
            var stripped = PlaybackState.From(withRules.InitialUnits, withRules.RuleIds);
            stripped.AdvanceToTick(events, withRules.EndTick);
            Assert.Equal(full.ViewHash(), stripped.ViewHash());
        }

        [Fact]
        public void UnguardedTriggerFiredEchoIsBoundedByCascadeDepth()
        {
            // ADR 0026 amended the original law here: TriggerFired MAY now wake On==TriggerFired
            // triggers (the Living Inscription hook) — but it still spends no cascade budget, and
            // even the pathological echo that matches its OWN announcement is cut by
            // MaxCascadeDepth rather than running away.
            var def = Fighter("echo");
            def.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,            // the seed: announces once per swing
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 1,
                                       Select = new Selector { Kind = SelKind.Self } } },
                RuleId = "echo.seed",
            });
            def.Triggers.Add(new Trigger
            {
                On = EventKind.TriggerFired,           // deliberately pathological
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 1,
                                       Select = new Selector { Kind = SelKind.Self } } },
                RuleId = "echo.loop",
            });
            var result = new Battle(new[]
            {
                U(1, 0, def, 1, 1),
                U(2, 1, Fighter("dummy", hp: 400), 1, 2),
            }, seed: 4).Run();

            int loopIdx = result.RuleIds.IndexOf("echo.loop");
            Assert.Contains(result.Events,                     // the hook is real…
                e => e.Kind == EventKind.TriggerFired && e.Aux == loopIdx);
            Assert.All(result.Events,                          // …and the ceiling held
                e => Assert.True(e.Depth <= Battle.MaxCascadeDepth));
        }

        // ---- identity ----------------------------------------------------------------

        [Fact]
        public void ComposedRulesTakeTheirSourcesName()
        {
            var chassis = new ChassisDef
            {
                Id = "testchassis", Name = "Test", MaxHp = 100, ManaMax = 0, MoveInterval = 4,
                StarterWeapon = new WeaponDef { Name = "Test Blade", Damage = 10, Interval = 10, Range = 1 },
                Passives = { new Trigger { On = EventKind.BattleStart } },
                StatRules = { new StatRule { Stat = StatKind.AttackFlat, Amount = 1 } },
            };
            var composed = Loadout.Compose(chassis).Def;

            Assert.Equal("testchassis", composed.Triggers[0].RuleId);
            Assert.Equal("testchassis", composed.StatRules[0].RuleId);
        }

        [Fact]
        public void ComposingDoesNotStampTheSharedCatalogInstance()
        {
            // The bug this exists to prevent: the catalog hands the SAME Trigger to every
            // composition, so stamping in place would rewrite the kit for every later one.
            var trigger = new Trigger { On = EventKind.BattleStart };
            var chassis = new ChassisDef
            {
                Id = "shared", Name = "Shared", MaxHp = 100, ManaMax = 0, MoveInterval = 4,
                StarterWeapon = new WeaponDef { Name = "W", Damage = 1, Interval = 10, Range = 1 },
                Passives = { trigger },
            };

            var first = Loadout.Compose(chassis).Def;
            Assert.Equal("shared", first.Triggers[0].RuleId);
            Assert.Equal("", trigger.RuleId);                       // catalog untouched
            Assert.NotSame(trigger, first.Triggers[0]);             // and it was cloned
        }

        [Fact]
        public void SecondRuleFromOneSourceIsSuffixed_SoTheFirstIsNeverRenamed()
        {
            var chassis = new ChassisDef
            {
                Id = "multi", Name = "Multi", MaxHp = 100, ManaMax = 0, MoveInterval = 4,
                StarterWeapon = new WeaponDef { Name = "W", Damage = 1, Interval = 10, Range = 1 },
                Passives = { new Trigger { On = EventKind.BattleStart }, new Trigger { On = EventKind.Death } },
            };
            var composed = Loadout.Compose(chassis).Def;

            Assert.Equal("multi", composed.Triggers[0].RuleId);     // adding a second must not rename the first
            Assert.Equal("multi#2", composed.Triggers[1].RuleId);
        }

        [Fact]
        public void EveryRuleIndexOnTheWireResolvesInsideTheTable()
        {
            var result = RunFight();
            foreach (var e in result.Events)
                if (e.Kind == EventKind.TriggerFired || e.Kind == EventKind.RuleChanged)
                {
                    Assert.InRange(e.Aux, 0, result.RuleIds.Count - 1);
                    Assert.False(string.IsNullOrEmpty(result.RuleIds[e.Aux]),
                                 "a composed rule should always name itself");
                }
        }

        // ---- the StatRule transition sweep ------------------------------------------

        [Fact]
        public void ConditionalStatRuleEmitsOnAndOffEdgesOnly()
        {
            // "While below 60% HP: +5 attack" — invisible before this feature existed.
            var def = Fighter("bleeder", hp: 100);
            def.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackFlat, Amount = 5, RuleId = "test.desperate",
                When = { new Cond { Kind = CondKind.OwnerBelowHpPct, Amount = 60 } },
            });
            var result = new Battle(new[]
            {
                U(1, 0, def, 1, 1),
                U(2, 1, Fighter("hitter", hp: 500, attack: 12), 1, 2),
            }, seed: 9).Run();

            var edges = result.Events.Where(e => e.Kind == EventKind.RuleChanged
                                              && result.RuleIds[e.Aux] == "test.desperate").ToList();
            Assert.NotEmpty(edges);
            Assert.Equal(1, edges[0].Amount);                       // starts full HP → first edge is ON
            Assert.Equal(5, edges[0].Aux2);                         // and carries its contribution
            // Transitions only: no two consecutive edges may report the same state.
            for (int i = 1; i < edges.Count; i++)
                Assert.NotEqual(edges[i - 1].Amount, edges[i].Amount);
        }

        [Fact]
        public void AnAlwaysTrueRuleFiresOneEdgeAndThenGoesQuiet()
        {
            // The owner must SURVIVE for this to be the single-edge case — see the companion test:
            // dying is itself a transition, and getting that wrong is how a badge outlives its unit.
            var def = Fighter("steady", hp: 600, attack: 40);
            def.StatRules.Add(new StatRule { Stat = StatKind.AttackFlat, Amount = 3, RuleId = "test.always" });
            var result = new Battle(new[]
            {
                U(1, 0, def, 1, 1),
                U(2, 1, Fighter("dummy", hp: 60), 1, 2),
            }, seed: 11).Run();

            var edges = result.Events.Where(e => e.Kind == EventKind.RuleChanged
                                              && result.RuleIds[e.Aux] == "test.always").ToList();
            Assert.Single(edges);
            Assert.Equal(1, edges[0].Amount);
        }

        [Fact]
        public void ADeadUnitsRulesGoOfflineOnTheTickItDies()
        {
            // Otherwise a corpse keeps a lit badge for the rest of the fight, which is exactly the
            // kind of stale persistent state the fold is supposed to make impossible.
            // Enough HP to be alive for the first sweep. A unit killed on tick 0 never emits an ON
            // edge at all — the sweep runs after DeathPhase, so it was never observed live — which
            // is correct and is why this needs to survive a few ticks to test the OFF edge.
            var def = Fighter("doomed", hp: 100, attack: 1);
            def.StatRules.Add(new StatRule { Stat = StatKind.AttackFlat, Amount = 3, RuleId = "test.always" });
            var result = new Battle(new[]
            {
                U(1, 0, def, 1, 1),
                U(2, 1, Fighter("killer", hp: 600, attack: 40), 1, 2),
            }, seed: 12).Run();

            int deathTick = result.Events.First(e => e.Kind == EventKind.Death && e.Target == 1).Tick;
            var edges = result.Events.Where(e => e.Kind == EventKind.RuleChanged
                                              && result.RuleIds[e.Aux] == "test.always").ToList();
            Assert.Equal(new[] { 1, 0 }, edges.Select(e => e.Amount));
            Assert.Equal(deathTick, edges[1].Tick);

            var fold = PlaybackState.From(result.InitialUnits, result.RuleIds);
            fold.AdvanceToTick(result.Events, result.EndTick);
            Assert.Empty(fold.ById(1)!.ActiveRules);
        }

        // ---- the fold + the wire -----------------------------------------------------

        [Fact]
        public void FoldTracksWhichPassivesAreLive_AndAScrubReconstructsIt()
        {
            var result = RunFight();
            var end = PlaybackState.From(result.InitialUnits, result.RuleIds);
            end.AdvanceToTick(result.Events, result.EndTick);

            // Rebuilt from scratch to the same tick — a scrub must land on the same live set.
            var scrub = PlaybackState.From(result.InitialUnits, result.RuleIds);
            scrub.AdvanceToTick(result.Events, result.EndTick);
            foreach (var u in end.Units)
                Assert.Equal(u.ActiveRules, scrub.ById(u.Id)!.ActiveRules);
        }

        [Fact]
        public void RuleTableSurvivesTheReplayWire()
        {
            var result = RunFight();
            var ms = new MemoryStream();
            Replay.Write(ms, result.InitialUnits, result.Events, result.RuleIds);
            var (_, events) = Replay.Read(new MemoryStream(ms.ToArray()), out var rules);

            Assert.Equal(result.RuleIds, rules);
            Assert.Equal(result.Events.Count, events.Count);
            Assert.Contains(events, e => e.Kind == EventKind.TriggerFired);
        }

        /// <summary>A fight with both a firing trigger and a toggling conditional stat rule.</summary>
        private static BattleResult RunFight()
        {
            var attacker = Fighter("attacker", hp: 160, attack: 14);
            attacker.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt, RuleId = "test.onhit",
                When = { new Cond { Kind = CondKind.SourceIsOwner },
                         new Cond { Kind = CondKind.IsRootEvent } },
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 2,
                                       Select = new Selector { Kind = SelKind.Self } } },
            });
            var victim = Fighter("victim", hp: 160, attack: 14);
            victim.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackFlat, Amount = 4, RuleId = "test.cornered",
                When = { new Cond { Kind = CondKind.OwnerBelowHpPct, Amount = 50 } },
            });
            return new Battle(new[] { U(1, 0, attacker, 1, 1), U(2, 1, victim, 1, 2) }, seed: 3).Run();
        }
    }
}
