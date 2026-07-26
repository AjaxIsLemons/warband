using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// The real IRunContent (board item 2): the 8 settled kits + the 11-weapon catalog + the
    /// starter banner set + the AUTHORED enemy encounters, wired into the run machine.
    /// Difficulty anchors to act + tier, never W/L (ADR 0002).
    /// </summary>
    public sealed class Catalog : IRunContent
    {
        private readonly RunConfig _cfg;
        public Catalog(RunConfig? cfg = null) => _cfg = cfg ?? new RunConfig();

        /// <summary>
        /// The trinket layer (2026-07-25 systems review §6). heroes.md has always said the anatomy
        /// is "Weapon + Trinket", but the trinket half was one +20 HP stub — so half the loadout
        /// surface did nothing while the single most build-defining number in the combat model
        /// (ManaMax, i.e. how often the signature fires) could not be touched by any of the 80 spec
        /// nodes or 11 weapons.
        ///
        /// The three item layers now own disjoint jobs, which is what keeps them from blurring:
        ///   Weapon      — the attack profile (damage, interval, range, shape, cast cadence)
        ///   Trinket     — the CHASSIS stat-shape: mana capacity, durability, reach
        ///   Inscription — team rules that cross heroes (ADR 0017)
        /// Trinkets are therefore the "repair my hero's weakness" axis, which is what a game with
        /// deliberately sticky heroes needs: keep the champion, change how it fails.
        /// </summary>
        public static readonly Dictionary<string, TrinketDef> Trinkets = new Dictionary<string, TrinketDef>
        {
            ["hourglass"] = new TrinketDef { Name = "Cracked Hourglass", HpBonus = 20 },

            // The cast-cadence pair — the same axis pulled in both directions, so "how often does
            // my signature go off" finally becomes a purchase rather than a fixed chassis fact.
            ["quickstone"] = new TrinketDef
            {
                // Casts sooner, and each one lands lighter.
                Name = "Quickened Stone", ManaMaxDelta = -12,
                StatRules = { Rule(StatKind.AttackFlat, -2) },
            },
            ["deepwell"] = new TrinketDef
            {
                // The long draw: a rarer, heavier signature — and the swings that fund it hit harder.
                Name = "Deepwell Reliquary", ManaMaxDelta = 15,
                StatRules = { Rule(StatKind.AttackFlat, 3) },
            },
            ["gravemark"] = new TrinketDef
            {
                // Kills feed the meter — the snowball trinket for anything that finishes bodies.
                Name = "Gravemark Charm",
                Triggers = { On(EventKind.Death, W(SrcOwner), Mana(Self, 12)) },
            },
            ["martyrsknot"] = new TrinketDef
            {
                // Pain feeds the meter — the frontline mirror of Gravemark, and the answer for a
                // hero whose problem is that it dies before its engine comes online.
                Name = "Martyr's Knot", HpBonus = 30,
                Triggers = { On(EventKind.DamageDealt, W(TgtOwner, RootEv), Mana(Self, 4)) },
            },
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

        /// <summary>
        /// An AUTHORED node encounter drawn from <see cref="Encounters.NodePool"/>, scaled by act
        /// and wager tier only — never by record (ADR 0002).
        ///
        /// This used to be random hero kits with a stat multiplier, which is why deployment worked
        /// but never MATTERED: five different fights all posed the same problem. Enemies now have
        /// their own designs (Jake, 2026-07-25) and each encounter poses a different one.
        ///
        /// The act also decides WHICH encounters may appear (Encounters.PoolFor), and the shared
        /// Encounters.Scale owns the difficulty curve so the authoring probe measures the same
        /// numbers that ship.
        /// </summary>
        public List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng) =>
            Comp(NodeComp(act, tier, rng));

        /// <summary>Same draw, same rng, same scaling — built from the SAME method that builds the
        /// spawn, so the preview cannot describe a fight the player will not get. (Before ADR 0024
        /// these were two code paths agreeing by convention, and the boss path had already drifted:
        /// it hardcoded the bonded pair.)</summary>
        public EncounterBrief EncounterBrief(int act, int nodeIndex, FightTier tier, Rng rng) =>
            BriefOf(NodeComp(act, tier, rng));

        public EncounterBrief BossBrief(int act, Rng rng) => BriefOf(BossComp(act));

        /// <summary>
        /// The act boss (ADR 0016 / ADR 0024). AUTHORED per act — act 1 closes on the Last Oath's
        /// bonded pair, act 2 on the Ashfall Battery's protected gun, act 3 on the Waning Crown,
        /// whose bell your own kills advance. Acts beyond the authored three (the endless horizon)
        /// keep the last boss and scale it.
        /// </summary>
        public List<(UnitDef Def, Hex Pos)> Boss(int act, Rng rng) => Comp(BossComp(act));

        /// <summary>The act's node encounter, drawn and scaled. Fresh mutable defs every call
        /// (EncounterDef's contract), so scaling here can never leak into the catalog.</summary>
        private static EncounterDef NodeComp(int act, FightTier tier, Rng rng)
        {
            var d = Encounters.Select(act, rng);
            foreach (var e in d.Enemies) Encounters.Scale(e.Def, act, tier);
            return d;
        }

        private static EncounterDef BossComp(int act)
        {
            var d = Encounters.BossFor(act);
            int pct = Encounters.BossScalePct(act);
            if (pct == 100) return d;
            foreach (var e in d.Enemies)
            {
                e.Def.MaxHp = e.Def.MaxHp * pct / 100;
                e.Def.Attack = e.Def.Attack * pct / 100;
            }
            return d;
        }

        private static List<(UnitDef Def, Hex Pos)> Comp(EncounterDef d)
        {
            var list = new List<(UnitDef, Hex)>();
            foreach (var e in d.Enemies) list.Add((e.Def, e.Pos));
            return list;
        }

        private static EncounterBrief BriefOf(EncounterDef d)
        {
            var brief = new EncounterBrief
            {
                Id = d.Id, Name = d.Name, Pressure = d.Pressure,
                RuleName = d.RuleName, RuleText = d.RuleText,
            };
            foreach (var e in d.Enemies)
                brief.Units.Add(new EncounterUnitBrief
                {
                    Name = e.Def.Name,
                    Role = e.Role,
                    RoleId = e.RoleId,
                    Accent = Enemies.RoleAccent(e.RoleId),
                    ChassisId = e.Def.ChassisId,
                    WeaponName = e.Def.WeaponName,
                    MaxHp = e.Def.MaxHp,
                    Attack = e.Def.Attack,
                    AttackIntervalTicks = e.Def.AttackInterval,
                    Range = e.Def.Range,
                    Row = e.Pos.Row,
                    Behavior = Enemies.Behavior(e.Def.Name),
                });
            return brief;
        }
    }
}
