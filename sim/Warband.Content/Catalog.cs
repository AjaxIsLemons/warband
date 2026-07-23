using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// The real IRunContent (board item 2): the 8 settled kits + the 11-weapon catalog +
    /// the starter banner set, wired into the run machine. Monsters reuse hero kits
    /// (first-playable law); difficulty anchors to act + tier, never W/L (ADR 0002).
    /// </summary>
    public sealed class Catalog : IRunContent
    {
        private readonly RunConfig _cfg;
        public Catalog(RunConfig? cfg = null) => _cfg = cfg ?? new RunConfig();

        public static readonly Dictionary<string, TrinketDef> Trinkets = new Dictionary<string, TrinketDef>
        {
            // The single first-playable trinket (12-item cap: 11 weapons + this).
            ["hourglass"] = new TrinketDef { Name = "Cracked Hourglass", HpBonus = 20 },
        };

        public static readonly Dictionary<string, BannerDef> Banners = new Dictionary<string, BannerDef>
        {
            // Starter set drawn from the dives' banner-hook sections. All placeholder.
            ["firstblood"] = new BannerDef
            {
                Name = "Banner of the First Hour", // when an enemy falls, the warband surges
                TeamTriggers = { On(EventKind.Death, W(TgtAlly(not: true)),
                    Status(StatusKind.Haste, 200, Allies(99, exSelf: false), ticks: 30)) },
            },
            ["leapstun"] = new BannerDef
            {
                Name = "Banner of the Held Line",  // heroes.md's original example: Leaps answered
                TeamTriggers = { On(EventKind.Leap, W(SrcEnemy),
                    Status(StatusKind.Stun, 0, EvSrc, ticks: 8)) },
            },
            ["brand"] = new BannerDef
            {
                Name = "Banner of the Brand",      // allies' attacks apply Burn (Pyro amplifier)
                TeamTriggers = { On(EventKind.DamageDealt, W(ByAttack, SrcAlly, RootEv),
                    Status(StatusKind.Burn, 1, EvTgt)) },
            },
            ["bronzehour"] = new BannerDef
            {
                Name = "Banner of the Bronze Hour", // the muster holds: opening shields
                TeamTriggers = { AtStart(Shield(Allies(99, exSelf: false), 20)) },
            },
            ["chorus"] = new BannerDef
            {
                Name = "Banner of the Chorus",     // every ally cast rings a small shield
                TeamTriggers = { On(EventKind.Cast, W(SrcAlly), Shield(EvSrc, 5)) },
            },
        };

        public ChassisDef Chassis(string id) => Kits.Chassis[id];
        public WeaponDef Weapon(string id) => Weapons.All[id];
        public TrinketDef Trinket(string id) => Trinkets[id];
        public SpecNode Node(string id) => Kits.Nodes[id];
        BannerDef IRunContent.Banner(string id) => Banners[id];

        private static readonly List<string> HeroIds = new List<string>
            { "cleric", "bulwark", "shade", "sharpshot", "pyromancer", "berserker", "phalanx", "banneret" };
        private static readonly List<string> WeaponIds = new List<string>(Weapons.All.Keys);
        private static readonly List<string> TrinketIds = new List<string>(Trinkets.Keys);
        private static readonly List<string> BannerIds = new List<string>(Banners.Keys);

        public IReadOnlyList<string> HeroPool(int act) => HeroIds;
        public IReadOnlyList<string> WeaponPool(int act) => WeaponIds;
        public IReadOnlyList<string> TrinketPool(int act) => TrinketIds;
        public IReadOnlyList<string> BannerPool(int act) => BannerIds;

        public (string A, string B) SpecOptions(string chassisId, Rank rank, string? pathId) =>
            Kits.Offers[$"{chassisId}|{rank}|{pathId ?? "-"}"];

        public Rank ForkRank(string chassisId) => Kits.ForkRanks[chassisId];

        /// <summary>Monsters are hero kits at recruit rank (first-playable law), scaled by
        /// act and wager tier only — never by record (ADR 0002).</summary>
        public List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng)
        {
            int count = System.Math.Min(2 + (act - 1) / 2 + ((int)tier + 1) / 2, 5);
            int hpPct = 60 + 15 * (act - 1) + 10 * (int)tier;      // recruits, undertuned on purpose
            int atkPct = 60 + 12 * (act - 1) + 10 * (int)tier;
            var list = new List<(UnitDef, Hex)>();
            for (int i = 0; i < count; i++)
            {
                string id = HeroIds[rng.Next(HeroIds.Count)];
                var composed = Loadout.Compose(Kits.Chassis[id]);
                var def = composed.Def;
                def.Name = "feral " + def.Name;                     // an echo gone wrong
                def.MaxHp = def.MaxHp * hpPct / 100;
                def.Attack = def.Attack * atkPct / 100;
                int row = def.Range <= 1 ? 5 : 6 + rng.Next(2);     // melee forward, ranged back
                list.Add((def, Hex.FromRowCol(row, (i * 2 + rng.Next(2)) % Battle.BoardCols)));
            }
            return list;
        }

        public GhostSnapshot BossGhost(int act, int bossWins, Rng rng) =>
            BotGhosts.Generate(this, _cfg, act, bossWins, rng);
    }
}
