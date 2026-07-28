using System;
using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>Range-aware default formation, shared by bot boards and the harness's
    /// default placement: melee hold the front row, ranged sit the back, center-out.</summary>
    internal static class Formation
    {
        internal static readonly int[] ColOrder = { 2, 3, 1, 4, 0, 5 };

        internal static int RangeOf(IRunContent content, HeroInstance hero) =>
            hero.WeaponId != null ? content.Weapon(hero.WeaponId).Range
                                  : content.Chassis(hero.ChassisId).StarterWeapon.Range;

        /// <summary>Owner-half hex for the n-th front (row 3, spilling to 2) or back
        /// (row 0, spilling to 1) unit.</summary>
        internal static Hex Slot(bool front, int n) =>
            front ? Hex.FromRowCol(3 - n / 6, ColOrder[n % 6])
                  : Hex.FromRowCol(n / 6, ColOrder[n % 6]);
    }

    /// <summary>
    /// Synthetic-fill bot boards for thin ghost pools (roadmap 1d; pitch: "cold start
    /// solved"). P0 has no server, so every act boss comes from here. Boards are credible
    /// by construction: sized to expected slot growth, deepened by act + record (the
    /// pool's keying, ADR 0002), geared and placed by weapon range. All tuning is
    /// PLACEHOLDER (content doctrine).
    /// </summary>
    public static class BotGhosts
    {
        public static GhostSnapshot Generate(IRunContent content, RunConfig cfg,
                                             int act, int bossWins, Rng rng)
        {
            int fought = act - 1;                // bosses faced before this one
            var snap = new GhostSnapshot
            {
                ContentVersion = content.ContentVersion,
                Act = act,
                WinsAtCapture = Math.Min(bossWins, fought),
                LossesAtCapture = Math.Max(0, fought - bossWins),
            };

            // Size: a player who bought every act-close slot (ADR 0006).
            int count = Math.Min(cfg.StartingFieldSlots + fought, cfg.MaxFieldSlots);
            var pool = new List<string>(content.HeroPool(act));
            if (pool.Count < count) count = pool.Count;
            var heroes = new List<HeroInstance>();
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(pool.Count);
                heroes.Add(new HeroInstance { ChassisId = pool[pick] });
                pool.RemoveAt(pick);
            }

            // Depth: act paces the baseline, record keys the pool.
            int rankUps = fought + snap.WinsAtCapture;
            for (int r = 0; r < rankUps; r++)
            {
                var eligible = heroes.FindAll(h => h.Rank < Rank.S);
                if (eligible.Count == 0) break;
                var hero = eligible[rng.Next(eligible.Count)];
                hero.Rank++;
                var specPool = content.SpecOptions(hero.ChassisId, hero.Rank, hero.PathId);
                string chosen = specPool[rng.Next(specPool.Count)];
                hero.SpecNodeIds.Add(chosen);
                if (hero.Rank == content.ForkRank(hero.ChassisId)) hero.PathId = chosen;
            }

            // Gear: one item per act past the first, alternating kinds.
            var weapons = content.WeaponPool(act);
            var trinkets = content.TrinketPool(act);
            for (int i = 0; i < fought; i++)
            {
                var hero = heroes[rng.Next(heroes.Count)];
                if (i % 2 == 0 && weapons.Count > 0)
                    hero.WeaponId = weapons[rng.Next(weapons.Count)];
                else if (trinkets.Count > 0 && hero.TrinketIds.Count == 0)
                    hero.TrinketIds.Add(trinkets[rng.Next(trinkets.Count)]);
            }

            var banners = content.BannerPool(act);
            if (act >= 3 && banners.Count > 0)
                snap.BannerIds.Add(banners[rng.Next(banners.Count)]);

            int fronts = 0, backs = 0;
            foreach (var hero in heroes)
            {
                bool front = Formation.RangeOf(content, hero) <= 1;
                snap.Units.Add(new GhostUnit
                {
                    Hero = hero,
                    Pos = Formation.Slot(front, front ? fronts++ : backs++),
                });
            }
            return snap;
        }
    }
}
