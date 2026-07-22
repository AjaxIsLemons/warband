using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>Player decisions for a harness run. Null hooks fall back to the default
    /// bot: Even tier, range-aware placement, greedy-legal shopping.</summary>
    public sealed class RunPolicy
    {
        public Func<RunController, FightTier>? Tier;
        public Func<RunController, IReadOnlyList<Hex>>? Place;
        public Action<RunController>? Shop;
    }

    public sealed class FightRecord
    {
        public int Act;
        public int Node;
        public bool IsBoss;
        public FightTier Tier;                   // meaningless when IsBoss
        public bool Won;
        public int Kills;
        public int Enemies;
        public int Gold;
        public int EndTick;
    }

    public sealed class RunReport
    {
        public ulong Seed;
        public RunState Final = null!;
        public List<FightRecord> Fights = new List<FightRecord>();
        public int GoldFromNodes;                // fights + events + boss income
        public int GoldSpentInShops;             // net of sell-backs
    }

    public sealed class TierStat
    {
        public int Chosen;
        public int Wins;
        public int Gold;
    }

    public sealed class AggregateReport
    {
        public int Runs;
        public int Victories;
        public int Flawless;
        public double AvgBossWins;
        public double AvgFinalGold;
        public double AvgFightTicks;
        public Dictionary<FightTier, TierStat> Tiers = new Dictionary<FightTier, TierStat>();

        public static AggregateReport From(IReadOnlyList<RunReport> reports)
        {
            var agg = new AggregateReport { Runs = reports.Count };
            long ticks = 0, fights = 0;
            foreach (var r in reports)
            {
                if (r.Final.Victory) agg.Victories++;
                if (r.Final.Flawless) agg.Flawless++;
                agg.AvgBossWins += r.Final.BossWins;
                agg.AvgFinalGold += r.Final.Gold;
                foreach (var f in r.Fights)
                {
                    ticks += f.EndTick;
                    fights++;
                    if (f.IsBoss) continue;
                    if (!agg.Tiers.TryGetValue(f.Tier, out var t))
                        agg.Tiers[f.Tier] = t = new TierStat();
                    t.Chosen++;
                    if (f.Won) t.Wins++;
                    t.Gold += f.Gold;
                }
            }
            if (reports.Count > 0)
            {
                agg.AvgBossWins /= reports.Count;
                agg.AvgFinalGold /= reports.Count;
            }
            if (fights > 0) agg.AvgFightTicks = (double)ticks / fights;
            return agg;
        }
    }

    /// <summary>
    /// Full-run headless harness (roadmap 1e — metasim lesson: model the economy, not
    /// just the fights). Plays complete runs with a policy and reports fight + economy
    /// metrics. The archetype sweep (roadmap 3) builds on this.
    /// </summary>
    public static class RunHarness
    {
        public static RunReport Play(ulong seed, IRunContent content,
                                     RunConfig? config = null, RunPolicy? policy = null)
        {
            var cfg = config ?? new RunConfig();
            var run = new RunController(seed, content, StarterWarband(content, cfg), cfg);
            var report = new RunReport { Seed = seed, Final = run.State };

            while (run.State.Phase != RunPhase.Complete)
            {
                if (run.State.Phase == RunPhase.Node)
                {
                    var kind = run.CurrentNodeKind;
                    if (kind == NodeKind.Event)
                    {
                        report.GoldFromNodes += run.ResolveEvent();
                        continue;
                    }
                    var placement = policy?.Place?.Invoke(run) ?? DefaultPlacement(run, content);
                    bool isBoss = kind == NodeKind.Boss;
                    var tier = isBoss ? default : policy?.Tier?.Invoke(run) ?? FightTier.Even;
                    var o = isBoss ? run.ResolveBoss(placement) : run.ResolveFight(tier, placement);
                    report.GoldFromNodes += o.GoldEarned;
                    report.Fights.Add(new FightRecord
                    {
                        Act = run.State.Act, Node = run.State.NodeIndex, IsBoss = isBoss,
                        Tier = tier, Won = o.Won, Kills = o.EnemiesKilled,
                        Enemies = o.EnemyCount, Gold = o.GoldEarned, EndTick = o.Battle.EndTick,
                    });
                }
                else
                {
                    int before = run.State.Gold;
                    (policy?.Shop ?? DefaultShop)(run);
                    report.GoldSpentInShops += before - run.State.Gold;
                    run.LeaveShop();
                }
            }
            return report;
        }

        public static List<RunReport> PlayMany(int runs, ulong seedBase, IRunContent content,
                                               RunConfig? config = null, RunPolicy? policy = null)
        {
            var reports = new List<RunReport>(runs);
            for (int i = 0; i < runs; i++)
                reports.Add(Play(seedBase + (ulong)i, content, config, policy));
            return reports;
        }

        /// <summary>First pool chassis fill the starting slots.</summary>
        public static List<HeroInstance> StarterWarband(IRunContent content, RunConfig cfg)
        {
            var pool = content.HeroPool(1);
            var band = new List<HeroInstance>();
            for (int i = 0; i < cfg.StartingFieldSlots && i < pool.Count; i++)
                band.Add(new HeroInstance { ChassisId = pool[i] });
            return band;
        }

        public static IReadOnlyList<Hex> DefaultPlacement(RunController run, IRunContent content)
        {
            var result = new List<Hex>();
            int fronts = 0, backs = 0;
            foreach (var hero in run.State.Field)
            {
                bool front = Formation.RangeOf(content, hero) <= 1;
                result.Add(Formation.Slot(front, front ? fronts++ : backs++));
            }
            return result;
        }

        /// <summary>Greedy-legal bot: slot if affordable, then offers left-to-right
        /// (fork choices always take option A), then equip empty slots, then field the bench.</summary>
        public static void DefaultShop(RunController run)
        {
            var s = run.State;
            if (run.SlotOfferOpen && s.Gold >= run.SlotOfferCost) run.BuySlot();

            for (int i = 0; i < s.ShopOffers.Count; i++)
            {
                var offer = s.ShopOffers[i];
                if (offer == null || s.Gold < offer.Price) continue;
                if (offer.Kind == OfferKind.Hero)
                {
                    bool ownedMax = s.Field.Concat(s.Bench)
                        .Any(h => h.ChassisId == offer.Id && h.Rank == Rank.S);
                    bool owned = s.Field.Concat(s.Bench)
                        .Any(h => h.ChassisId == offer.Id && h.Rank < Rank.S);
                    if (ownedMax || (!owned && !run.HasRoomForRecruit)) continue;
                    run.BuyOffer(i);
                    if (s.PendingSpec != null) run.ChooseSpec(0);
                }
                else
                    run.BuyOffer(i);
            }

            for (int i = s.Inventory.Count - 1; i >= 0; i--)
            {
                var item = s.Inventory[i];
                int idx = item.Kind == ItemKind.Weapon
                    ? s.Field.FindIndex(h => h.WeaponId == null)
                    : s.Field.FindIndex(h => h.TrinketIds.Count == 0);
                if (idx < 0) continue;
                if (item.Kind == ItemKind.Weapon) run.EquipWeapon(RosterZone.Field, idx, i);
                else run.EquipTrinket(RosterZone.Field, idx, i);
            }

            while (s.Bench.Count > 0 && s.Field.Count < s.FieldSlots)
                run.BenchToField(0);
        }
    }
}
