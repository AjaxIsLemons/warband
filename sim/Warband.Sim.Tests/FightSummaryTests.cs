using System.Collections.Generic;
using System.IO;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The post-fight screen's fold. These pin ATTRIBUTION on authored fights whose outcome is
    /// arithmetic, not emergent: who killed whom, with what, how far past zero — plus the
    /// accounting rules a chart binds to (team shares sum, absorbed is part of taken, storm
    /// damage belongs to nobody).
    /// </summary>
    public class FightSummaryTests
    {
        [Fact]
        public void CreditsTheKillingBlowWithCauseAndOverkill()
        {
            // 10 atk vs 25 hp: three swings, the third overshoots by 5. Nothing else can act.
            var result = new Battle(BattleTests.Duel(BattleTests.Grunt(), BattleTests.Pacifist(25))).Run();
            var summary = FightSummary.Build(result);

            Assert.Equal(Winner.Team0, summary.Winner);

            var killer = summary.Unit(0)!;
            Assert.Equal(30, killer.DamageDealt);
            Assert.Equal(30, killer.DamageBy(Cause.Attack));
            Assert.Equal(1, killer.Kills);
            Assert.False(killer.Died);
            Assert.Equal(-1, killer.DeathTick);

            var victim = summary.Unit(1)!;
            Assert.Equal(30, victim.DamageTaken);
            Assert.Equal(0, victim.DamageDealt);
            Assert.True(victim.Died);
            Assert.Equal(0, victim.KilledBy);
            Assert.Equal(Cause.Attack, victim.KilledByCause);

            var beat = Assert.Single(summary.Beats);
            Assert.Equal(1, beat.Victim);
            Assert.Equal(0, beat.Killer);
            Assert.Equal(Cause.Attack, beat.Cause);
            Assert.Equal(5, beat.Overkill);            // 25 hp, 30 dealt
            Assert.False(beat.KillerInferred);         // the Death event named the killer itself
            Assert.Equal(victim.DeathTick, beat.Tick);

            // Damage-dealt order is the chart order.
            Assert.Equal(new[] { 0, 1 }, summary.Units.Select(u => u.UnitId).ToArray());
        }

        [Fact]
        public void TeamTotalsRollUpAndSharesSumToOneHundred()
        {
            // Two attackers on team 0 with different output, so the shares are not a trivial 50/50.
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 200, atk: 12), Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 200, atk: 5), Hex.FromRowCol(3, 3)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 160, atk: 9), Hex.FromRowCol(4, 2)),
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 160, atk: 9), Hex.FromRowCol(4, 3)),
            };
            var summary = FightSummary.Build(new Battle(units).Run());

            foreach (var team in summary.Teams)
            {
                var members = summary.Units.Where(u => u.Team == team.Team).ToList();
                Assert.Equal(2, members.Count);
                Assert.Equal(2, team.Units);
                Assert.Equal(members.Sum(u => u.DamageDealt), team.DamageDealt);
                Assert.Equal(members.Sum(u => u.DamageTaken), team.DamageTaken);
                Assert.Equal(members.Count(u => u.Died), team.Deaths);
                Assert.Equal(team.Units - team.Deaths, team.Survivors);
                if (team.DamageDealt > 0)
                    Assert.Equal(100.0, members.Sum(u => u.DamagePctOfTeam), 6);
            }

            // Every kill in this fight is a unit kill, so credits and deaths balance.
            Assert.Equal(summary.Beats.Count, summary.Teams.Sum(t => t.Kills));
            Assert.Equal(summary.Beats.Count, summary.Teams.Sum(t => t.Deaths));
        }

        [Fact]
        public void ShieldAbsorbedIsPartOfDamageTakenAndHealingIsCredited()
        {
            // Tank shields itself at the bell; a censer ally heals it back up as it takes hits.
            var tank = BattleTests.Grunt(hp: 200, atk: 8);
            tank.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 70, Select = new Selector { Kind = SelKind.Self } } },
            });
            var censer = BattleTests.Grunt(hp: 120, atk: 9);
            censer.HealAutos = true;   // censer law: swings heal the lowest-HP ally
            censer.Range = 2;

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, tank, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, censer, Hex.FromRowCol(2, 2)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 300, atk: 11), Hex.FromRowCol(4, 2)),
            };
            var summary = FightSummary.Build(new Battle(units).Run());

            var t = summary.Unit(0)!;
            Assert.True(t.ShieldAbsorbed > 0);
            Assert.True(t.DamageTaken >= t.ShieldAbsorbed);  // absorbed is a SLICE of taken, not extra

            var healer = summary.Unit(1)!;
            Assert.True(healer.HealingDone > 0);
            Assert.Equal(0, healer.DamageDealt);             // censer swings never damage
            Assert.True(t.HealingReceived > 0);
            Assert.Equal(healer.HealingDone, t.HealingReceived);
            Assert.Equal(healer.HealingDone, summary.TeamTotals(0)!.HealingDone);
        }

        [Fact]
        public void StormKillsCreditNobodyAndLandUnattributed()
        {
            // Nobody can deal damage: the storm ends it. Death.Source stays -1 because storm
            // damage never overwrites LastDamagedBy — the environment has no unit to credit.
            var result = new Battle(BattleTests.Duel(BattleTests.Pacifist(100), BattleTests.Pacifist(100))).Run();
            var summary = FightSummary.Build(result);

            Assert.Equal(Winner.Draw, summary.Winner);
            Assert.NotEmpty(summary.Beats);
            foreach (var beat in summary.Beats)
            {
                Assert.Equal(-1, beat.Killer);
                Assert.Equal(Cause.Storm, beat.Cause);
                Assert.False(beat.KillerInferred);
            }
            Assert.Equal(0, summary.Teams.Sum(t => t.Kills));
            Assert.Equal(0, summary.Teams.Sum(t => t.DamageDealt));
            Assert.True(summary.UnattributedDamage > 0);
            // Conservation only closes once the storm's share is added back.
            Assert.Equal(summary.Teams.Sum(t => t.DamageTaken),
                         summary.Teams.Sum(t => t.DamageDealt) + summary.UnattributedDamage);
        }

        [Fact]
        public void FoldsIdenticallyFromReplayBytes()
        {
            // The client path: it has (initial units, events) off the wire and nothing else.
            var result = new Battle(BattleTests.Duel(BattleTests.Grunt(hp: 150), BattleTests.Grunt(hp: 90))).Run();

            var buffer = new MemoryStream();
            Replay.Write(buffer, result.InitialUnits, result.Events);
            var (initial, events) = Replay.Read(new MemoryStream(buffer.ToArray()));

            var live = FightSummary.Build(result);
            var reloaded = FightSummary.Build(initial, events);

            Assert.Equal(live.Winner, reloaded.Winner);
            Assert.Equal(live.EndTick, reloaded.EndTick);
            Assert.Equal(live.UnattributedDamage, reloaded.UnattributedDamage);
            Assert.Equal(live.Units.Select(u => (u.UnitId, u.DamageDealt, u.DamageTaken, u.Kills, u.DeathTick, u.KilledBy)),
                         reloaded.Units.Select(u => (u.UnitId, u.DamageDealt, u.DamageTaken, u.Kills, u.DeathTick, u.KilledBy)));
            Assert.Equal(live.Beats.Select(b => (b.Tick, b.Victim, b.Killer, b.Cause, b.Overkill)),
                         reloaded.Beats.Select(b => (b.Tick, b.Victim, b.Killer, b.Cause, b.Overkill)));
        }

        [Fact]
        public void DamageBucketsSplitByCause()
        {
            // A caster with a DoT rider: attack, ability and dot damage all from one unit.
            var pyro = BattleTests.Grunt(hp: 120, atk: 6);
            pyro.Range = 3;
            pyro.ManaMax = 25;
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 25 });
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Dot, Amount = 4, StatusTicks = 60 });

            var summary = FightSummary.Build(new Battle(BattleTests.Duel(pyro, BattleTests.Grunt(hp: 400, atk: 4))).Run());
            var caster = summary.Unit(0)!;

            Assert.True(caster.DamageBy(Cause.Attack) > 0);
            Assert.True(caster.DamageBy(Cause.Ability) > 0);
            Assert.True(caster.DamageBy(Cause.Dot) > 0);
            Assert.Equal(caster.DamageDealt, caster.DamageBuckets().Sum(b => b.Amount));
            // Buckets come back in Cause-enum order, no empties.
            var buckets = caster.DamageBuckets();
            Assert.All(buckets, b => Assert.True(b.Amount > 0));
            Assert.Equal(buckets.OrderBy(b => (int)b.Cause).ToList(), buckets);
        }
    }
}
