using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// ADR 0024. Two things are pinned here, and they are pinned together on purpose because
    /// shipping one without the other is what caused the bug being fixed:
    ///
    /// ① **Every act closes on its own strength exam.** `Catalog.Boss` used to return the
    ///    act-scaled Last Oath for all three acts.
    /// ② **The brief is the fight.** The client's boss disclosure was hardcoded to the bonded pair,
    ///    which was invisible only because there was one boss. The moment ① lands, any brief that
    ///    is built separately from the spawn starts lying — so the contract that they are the same
    ///    comp is now a test, not a convention.
    /// </summary>
    public class ActBossAndDisclosureTests
    {
        private static readonly FightTier[] Tiers =
            { FightTier.Stable, FightTier.Fraying, FightTier.Collapsing };

        // ---- ① per-act bosses -------------------------------------------------------

        [Fact]
        public void EachActClosesOnADifferentAuthoredBoss()
        {
            var ids = Enumerable.Range(1, 3).Select(a => Encounters.BossFor(a).Id).ToList();
            Assert.Equal(new[] { "bonded-pair", "ashfall-battery", "waning-crown" }, ids);
            Assert.Equal(3, ids.Distinct().Count());
        }

        [Fact]
        public void EveryBossDisclosesAnIdentityAPressureAndARule()
        {
            // pve-encounters.md: the boss is revealed at act start as a build target. A boss with
            // no rule text is a stat block the player cannot prepare for.
            foreach (var factory in Encounters.BossPool)
            {
                var def = factory();
                Assert.False(string.IsNullOrWhiteSpace(def.Id));
                Assert.False(string.IsNullOrWhiteSpace(def.Name));
                Assert.False(string.IsNullOrWhiteSpace(def.Pressure));
                Assert.False(string.IsNullOrWhiteSpace(def.RuleName));
                Assert.True(def.RuleText.Length > 40, $"{def.Name}'s rule text is too thin to prepare against");
                Assert.NotEmpty(def.Enemies);
            }
        }

        [Fact]
        public void EveryBossBodyIsPlacedLegallyInTheEnemyHalf()
        {
            foreach (var factory in Encounters.BossPool)
                Assert.All(factory().Enemies, e =>
                {
                    Assert.True(Battle.InBounds(e.Pos), $"{e.Def.Name} is off the board");
                    Assert.True(e.Pos.Row >= 4, $"{e.Def.Name} is standing in the player's half");
                });
        }

        [Fact]
        public void BossBodiesAreFreshPerCallSoScalingNeverLeaksIntoTheCatalog()
        {
            var cat = new Catalog();
            int before = Encounters.WaningCrown().Enemies[0].Def.MaxHp;
            cat.Boss(6, new Rng(1));            // an endless-depth act, which DOES scale
            cat.Boss(6, new Rng(1));
            Assert.Equal(before, Encounters.WaningCrown().Enemies[0].Def.MaxHp);
        }

        [Fact]
        public void NoBossUnitCarriesABlanketControlImmunity()
        {
            // pve-encounters.md: "being part of a boss encounter grants no universal immunity".
            // The shared verbs must all still land, so no boss body may strip or refuse them.
            var control = new[]
            {
                StatusKind.Stun, StatusKind.Silence, StatusKind.Taunt,
                StatusKind.Disarm, StatusKind.Root, StatusKind.Slow,
            };
            foreach (var factory in Encounters.BossPool)
            foreach (var e in factory().Enemies)
            foreach (var trigger in e.Def.Triggers)
            foreach (var effect in trigger.Do)
            {
                if (effect.Kind != EffectKind.RemoveStatus) continue;
                Assert.False(control.Contains(effect.Status),
                    $"{e.Def.Name} strips {effect.Status} — that is a hidden control immunity");
            }
        }

        [Fact]
        public void TheWaningCrownsBellIsAdvancedByEveryDeathInItsCourt()
        {
            // The act-3 boss's whole design: clearing the escorts — the habit three acts of node
            // fights train — is what rings the bell. If a death does not feed it, the encounter is
            // just a slower Hour-Scribe.
            var crown = Enemies.Crown();
            var fed = crown.Triggers.Where(t =>
                t.On == EventKind.Death && t.Do.Any(e => e.Kind == EffectKind.GrantMana)).ToList();
            Assert.Single(fed);
            Assert.True(fed[0].Do.First(e => e.Kind == EffectKind.GrantMana).Amount > 0);

            // And it must be pure-time otherwise: a hit-fed clock fires the instant it is focused,
            // which inverts the problem (the ADR 0023 finding that produced ManaPerHitTaken).
            Assert.Equal(0, crown.ManaPerHitTaken);
        }

        [Fact]
        public void BothNewBossClocksActuallyRingInARealFight()
        {
            // A boss rule that never fires before the fight ends is decoration. Measured against a
            // deliberately passive wall so the clock, not the probe party, is what is under test.
            foreach (string id in new[] { "ashfall-battery", "waning-crown" })
            {
                var def = Encounters.BossPool.Select(f => f()).First(d => d.Id == id);
                var punchbag = new UnitDef
                {
                    Name = "proof wall", MaxHp = 4000, Attack = 1, AttackInterval = 40,
                    Range = 1, MoveInterval = 20,
                };
                var units = new List<UnitState>
                {
                    UnitState.Spawn(0, 0, punchbag, Hex.FromRowCol(1, 2)),
                    UnitState.Spawn(1, 0, punchbag, Hex.FromRowCol(1, 3)),
                };
                int enemyId = 100;
                foreach (var e in def.Enemies)
                    units.Add(UnitState.Spawn(enemyId++, 1, e.Def, e.Pos));

                var result = new Battle(units, seed: 5).Run();
                Assert.True(result.Events.Any(ev => ev.Kind == EventKind.Cast && ev.Source >= 100),
                    $"{def.Name}'s clock never rang");
            }
        }

        [Fact]
        public void SilenceStopsTheBellCompletely()
        {
            // Both new bosses promise this in their rule text, and GainMana is gated on Silence.
            // If that ever changes, the disclosed answer stops working and the text becomes a lie.
            Assert.True(CrownCasts(silenced: false), "control failed: the bell never rang unsilenced, " +
                                                     "so the silenced case proves nothing");
            Assert.False(CrownCasts(silenced: true), "Silence did not stop the bell — the rule text lies");
        }

        private static bool CrownCasts(bool silenced)
        {
            var witness = new UnitDef
            {
                Name = "proof witness", MaxHp = 4000, Attack = 0, AttackInterval = 10,
                Range = 8, MoveInterval = 20,
            };
            if (silenced) witness.Triggers.Add(D2.SilenceAtStart());
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, witness, Hex.FromRowCol(1, 2)),
                UnitState.Spawn(100, 1, Enemies.Crown(), Hex.FromRowCol(7, 2)),
            };
            var result = new Battle(units, seed: 3).Run();
            return result.Events.Any(e => e.Kind == EventKind.Cast && e.Source == 100);
        }

        // ---- ② the brief IS the fight ----------------------------------------------

        [Fact]
        public void BossBriefDescribesExactlyTheBossThatSpawns()
        {
            var cat = new Catalog();
            for (int act = 1; act <= 4; act++)     // 4 exercises the endless-depth scale path too
            {
                var brief = ((IRunContent)cat).BossBrief(act, new Rng(1));
                var spawn = cat.Boss(act, new Rng(1));

                Assert.Equal(spawn.Count, brief.Units.Count);
                Assert.Equal(
                    spawn.Select(u => (u.Def.Name, u.Def.MaxHp, u.Def.Attack, u.Def.Range, u.Pos.Row)),
                    brief.Units.Select(u => (u.Name, u.MaxHp, u.Attack, u.Range, u.Row)));
            }
        }

        [Fact]
        public void NodeBriefDescribesExactlyTheEncounterThatSpawns()
        {
            // The scaled numbers must match too: a brief showing pre-scaling health would be a
            // preview of a different fight at every tier above Stable.
            var cat = new Catalog();
            for (int act = 1; act <= 3; act++)
            foreach (var tier in Tiers)
            for (int node = 0; node < 4; node++)
            {
                var brief = ((IRunContent)cat).EncounterBrief(act, node, tier, new Rng((ulong)(node + 1)));
                var spawn = cat.Encounter(act, node, tier, new Rng((ulong)(node + 1)));

                Assert.Equal(spawn.Count, brief.Units.Count);
                Assert.Equal(
                    spawn.Select(u => (u.Def.Name, u.Def.MaxHp, u.Def.Attack, u.Pos.Row)),
                    brief.Units.Select(u => (u.Name, u.MaxHp, u.Attack, u.Row)));
            }
        }

        [Fact]
        public void EveryPreviewedBodyCarriesARoleAndABehaviorSentence()
        {
            // "Know the rules, not the result" includes targeting rules. A Sanddrift Gunner whose
            // entire design is "acquires FARTHEST, holds standoff 5" must say so before deployment.
            var cat = new Catalog();
            var briefs = new List<EncounterBrief>();
            for (int act = 1; act <= 3; act++)
            {
                briefs.Add(((IRunContent)cat).BossBrief(act, new Rng(1)));
                for (int node = 0; node < 4; node++)
                    briefs.Add(((IRunContent)cat).EncounterBrief(act, node, FightTier.Stable,
                                                                 new Rng((ulong)(node + 1))));
            }

            Assert.All(briefs, b => Assert.All(b.Units, u =>
            {
                Assert.False(string.IsNullOrWhiteSpace(u.Name));
                Assert.False(string.IsNullOrWhiteSpace(u.Role), $"{u.Name} has no previewed role");
                Assert.False(string.IsNullOrWhiteSpace(u.RoleId), $"{u.Name} has no role id");
                Assert.False(string.IsNullOrWhiteSpace(u.Accent), $"{u.Name} has no accent");
                Assert.True(u.Behavior.Length > 20,
                    $"{u.Name} is previewed without a behavior sentence — the player cannot prepare for it");
            }));
        }

        [Fact]
        public void ChassisIdIsCarriedAsARenderKeyAndNeverAsTheUnitsName()
        {
            // The bug this guards: the shell titled enemy cards from ContentLexicon.Chassis(id), so
            // an Hourling previewed as "Shade" with the Shade's ability text. The brief must carry
            // the authored name, and it must differ from the borrowed silhouette's hero name.
            var cat = new Catalog();
            var brief = ((IRunContent)cat).EncounterBrief(1, 0, FightTier.Stable, new Rng(1));
            Assert.NotEmpty(brief.Units);
            Assert.All(brief.Units, u =>
            {
                if (string.IsNullOrEmpty(u.ChassisId)) return;
                Assert.NotEqual(ContentLexicon.Chassis(u.ChassisId).Name, u.Name);
            });
        }

        [Fact]
        public void EveryAuthoredBodyReachesTheBoardCarryingItsRole()
        {
            // The board renders a monster AS its role (item 29), so the role has to survive the trip
            // from the encounter's placement onto the UnitDef the battle spawns — not just into the
            // preview card. A body that arrives with an empty RoleId falls back to a borrowed hero
            // silhouette, which is the exact lie the enemy cards were fixed for.
            var cat = (IRunContent)new Catalog();
            var comps = new List<List<(UnitDef Def, Hex Pos)>>();
            for (int act = 1; act <= 3; act++)
            {
                comps.Add(cat.Boss(act, new Rng((ulong)act)));
                foreach (FightTier tier in new[] { FightTier.Stable, FightTier.Fraying, FightTier.Collapsing })
                    for (int node = 0; node < 4; node++)
                        comps.Add(cat.Encounter(act, node, tier, new Rng((ulong)(node + 1))));
            }

            Assert.All(comps, c => Assert.All(c, e =>
                Assert.False(string.IsNullOrWhiteSpace(e.Def.RoleId),
                             $"{e.Def.Name} spawns with no role — the board would borrow a hero body")));
        }

        [Fact]
        public void EveryAuthoredEncounterStampsItsRolesOnTheDefsThemselves()
        {
            // The catalog is NOT the only way an encounter gets built — `Encounters.ById` feeds the
            // render fixtures and the authoring probes, and it skips the catalog entirely. Stamping
            // the role at a resolution point therefore shipped roleless bodies to exactly the
            // fixtures this item is verified with. Walk the authored pools directly.
            var authored = new List<EncounterDef>();
            foreach (var factory in Encounters.NodePool)
                for (int act = 1; act <= 3; act++) authored.Add(factory(act));
            foreach (var factory in Encounters.BossPool) authored.Add(factory());
            Assert.NotEmpty(authored);

            Assert.All(authored, d => Assert.All(d.Enemies, e =>
            {
                Assert.False(string.IsNullOrWhiteSpace(e.RoleId), $"{d.Id}/{e.Def.Name}: placement has no role");
                Assert.Equal(e.RoleId, e.Def.RoleId); // the def is what the board and the wire see
            }));
        }

        [Fact]
        public void TheBoardAndThePreviewAgreeOnTheRole()
        {
            // Same draw, same rng: the role the player reads on the preview card must be the role
            // the body wears on the board. Two tables would drift the moment one is edited.
            var cat = (IRunContent)new Catalog();
            for (int act = 1; act <= 3; act++)
            {
                var brief = cat.EncounterBrief(act, 0, FightTier.Stable, new Rng((ulong)act));
                var comp = cat.Encounter(act, 0, FightTier.Stable, new Rng((ulong)act));
                Assert.Equal(brief.Units.Select(u => u.RoleId), comp.Select(e => e.Def.RoleId));
            }
        }

        [Fact]
        public void PreviewBriefAndPreviewEnemiesAgreeThroughTheController()
        {
            // End to end through the salted rng, on the REAL catalog: the two things the shell
            // actually calls must resolve to one encounter. Reconstructing either outside the
            // controller shows the player an army that does not spawn.
            var run = new RunController(31, new Catalog(), Kit.Warband());
            while (run.CurrentNodeKind != NodeKind.Fight)
                run.ResolveEvent();

            var brief = run.PreviewBrief(FightTier.Stable);
            var enemies = run.PreviewEnemies(FightTier.Stable);
            Assert.Equal(enemies.Count, brief.Units.Count);
            Assert.Equal(enemies.Select(e => (e.Def.Name, e.Def.MaxHp, e.Pos.Row)),
                         brief.Units.Select(u => (u.Name, u.MaxHp, u.Row)));
        }
    }

    /// <summary>Tiny local trigger builder — the content DSL is internal to Warband.Content.</summary>
    internal static class D2
    {
        public static Trigger SilenceAtStart()
        {
            var t = new Trigger { On = EventKind.BattleStart };
            t.Do.Add(new EffectDef
            {
                Kind = EffectKind.ApplyStatus,
                Status = StatusKind.Silence,
                Amount = 0,
                StatusTicks = -1,
                Select = new Selector { Kind = SelKind.NearestEnemy },
            });
            return t;
        }
    }
}
