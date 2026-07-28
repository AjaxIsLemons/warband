using System;
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

        /// <summary>Stamp each banner's registry key onto the triggers it contributes, so a team
        /// rule can name itself on the wire like a unit's own passive does. Team triggers never pass
        /// through <see cref="Loadout.Compose"/> — they are handed straight to Battle by
        /// RunController and the scenario builder — so without this they would be the one part of
        /// the engine that stays anonymous, and it is the part ADR 0016's north star lives in
        /// (these are the legacy Banner form of Inscriptions, item 5a).
        /// Done once at static init: these are catalog constants, not per-composition state.</summary>
        private static Dictionary<string, BannerDef> Identify(Dictionary<string, BannerDef> banners)
        {
            foreach (var kv in banners)
                for (int i = 0; i < kv.Value.TeamTriggers.Count; i++)
                    kv.Value.TeamTriggers[i].RuleId =
                        i == 0 ? "banner." + kv.Key : "banner." + kv.Key + "#" + (i + 1);
            return banners;
        }

        public static readonly Dictionary<string, BannerDef> Banners = Identify(new Dictionary<string, BannerDef>
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
        });

        /// <summary>
        /// The content fingerprint (ADR 0008's `contentVersion`). Computed once per process from the
        /// ACTUAL content graph rather than a hand-bumped constant — the failure mode being guarded
        /// against is somebody retuning a number, which is exactly the case a human would forget to
        /// bump a constant for.
        ///
        /// Keys are sorted so the fingerprint does not move when a registry is reordered, but the
        /// ordered CONTENTS are hashed, so adding, removing or renaming anything does move it.
        /// Encounters and bosses are included by building them: they are functions of act, so each
        /// is materialised at every act it can appear in and the resulting bodies are hashed.
        /// </summary>
        public string ContentVersion => LazyVersion.Value;

        private static readonly System.Lazy<string> LazyVersion =
            new System.Lazy<string>(ComputeContentVersion);

        private static string ComputeContentVersion()
        {
            var h = new ContentHash();
            h.Add("warband-content/1");

            foreach (string id in Sorted(Kits.Chassis.Keys)) { h.Add(id); h.AddChassis(Kits.Chassis[id]); }
            foreach (string id in Sorted(Weapons.All.Keys)) { h.Add(id); h.AddWeapon(Weapons.All[id]); }
            foreach (string id in Sorted(Trinkets.Keys)) { h.Add(id); h.AddTrinket(Trinkets[id]); }
            foreach (string id in Sorted(Kits.Nodes.Keys)) { h.Add(id); h.AddNode(Kits.Nodes[id]); }
            foreach (string id in Sorted(Banners.Keys))
            {
                h.Add(id);
                h.AddTriggers(Banners[id].TeamTriggers);
                h.Add(Banners[id].Name);
            }

            // The spec offer pools decide which nodes a run can even reach, so a changed offer
            // table changes outcomes as surely as a changed node. Folded element-by-element, so a
            // two-entry pool hashes exactly as the old (A, B) pair did — arity alone is not a
            // content change.
            foreach (string key in Sorted(Kits.Offers.Keys))
            {
                h.Add(key);
                foreach (string node in Kits.Offers[key]) h.Add(node);
            }
            foreach (string id in Sorted(Kits.ForkRanks.Keys))
                h.Add(id).Add((int)Kits.ForkRanks[id]);

            for (int act = 1; act <= 3; act++)
            {
                h.Add("act").Add(act);
                foreach (var factory in Encounters.NodePool) AddEncounter(h, factory(act));
                foreach (var factory in Encounters.PoolFor(act)) h.Add("pool").Add(factory(act).Id);
                AddEncounter(h, Encounters.BossFor(act));
                h.Add("bossScale").Add(Encounters.BossScalePct(act));
            }
            return h.Hex;
        }

        private static void AddEncounter(ContentHash h, EncounterDef d)
        {
            h.Add(d.Id).Add(d.Name).Add(d.RuleName);
            h.Add(d.Enemies.Count);
            foreach (var e in d.Enemies)
            {
                h.Add(e.RoleId).Add(e.Pos.Q).Add(e.Pos.R);
                h.AddUnit(e.Def);
            }
        }

        private static List<string> Sorted(IEnumerable<string> keys)
        {
            var list = new List<string>(keys);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        public ChassisDef Chassis(string id) => Kits.Chassis[id];
        public WeaponDef Weapon(string id) => Weapons.All[id];
        public TrinketDef Trinket(string id) => Trinkets[id];
        /// <summary>Resolves candidate nodes too — the sweep must be able to compose and fight
        /// them. Reachability is gated at <see cref="SpecOptions"/>, because an offer is the only
        /// way a node enters a run.</summary>
        public SpecNode Node(string id) =>
            Kits.Nodes.TryGetValue(id, out var node) ? node : Kits.CandidateNodes[id];
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

        /// <summary>
        /// Opt in to authored-but-unreachable content (<see cref="Kits.CandidateNodes"/>). Sweep
        /// and tests only — a RunController is handed a Catalog with this off, so no candidate can
        /// ever be offered to a player. Off by default so forgetting to set it is the safe failure.
        /// </summary>
        public bool IncludeCandidates;

        public IReadOnlyList<string> SpecOptions(string chassisId, Rank rank, string? pathId)
        {
            string key = $"{chassisId}|{rank}|{pathId ?? "-"}";
            if (!IncludeCandidates)
            {
                if (Kits.Offers.TryGetValue(key, out var authored)) return authored;
                throw new InvalidOperationException(
                    $"No live specialization offer is authored for '{key}'. " +
                    "The hero may still owe its prior fork choice.");
            }

            var merged = new List<string>();
            if (Kits.Offers.TryGetValue(key, out var live)) merged.AddRange(live);
            if (Kits.CandidateOffers.TryGetValue(key, out var extra))
                foreach (string id in extra)
                    if (!merged.Contains(id)) merged.Add(id);
            return merged;
        }

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
