using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// The 8 settled kits (dive campaign, Design/dives/*). Every node traces to its dive
    /// doc; magnitudes are placeholder. Where a node needed a shape the sim lacks, the
    /// nearest grammar expression is used and marked SIMPLIFIED — candidates for the
    /// outlier sweep's "never-picked" report, not silent cuts.
    /// Spec-offer rows live in Offers: one flat table = Jake's "easily changeable
    /// tier-up bonuses." Rank stat bumps live on each chassis (RankHp/RankAttack).
    /// </summary>
    public static class Kits
    {
        public static readonly Dictionary<string, ChassisDef> Chassis = new Dictionary<string, ChassisDef>();
        public static readonly Dictionary<string, SpecNode> Nodes = new Dictionary<string, SpecNode>();
        /// <summary>
        /// The authored POOL a rank-up draws from — not necessarily what the player sees.
        /// The run layer draws from this (RunController.SpecPick): the fork rank always offers
        /// the whole pool, because withholding a path would hide a leg of the hero's identity
        /// triangle; every other rank draws a seeded subset, which is where run-to-run variety
        /// comes from. A pool of two therefore behaves exactly as the old 1-of-2 table did.
        /// </summary>
        public static readonly Dictionary<string, List<string>> Offers = new Dictionary<string, List<string>>();
        public static readonly Dictionary<string, Rank> ForkRanks = new Dictionary<string, Rank>();

        /// <summary>
        /// CANDIDATE content: authored, testable, sweepable — and unreachable in a real run.
        ///
        /// Deliberately held in registries of their own rather than a flag on the live ones, so
        /// leaking a candidate into a run takes a code change, not a mistake. Two consequences
        /// that matter:
        ///
        /// 1. The content fingerprint (Catalog.ComputeContentVersion) folds `Nodes` and `Offers`
        ///    ONLY. Content that cannot be reached cannot influence a fight, so it is not part of
        ///    a run's content identity — which is why authoring candidates does not invalidate
        ///    saves or replays. The fingerprint moves when a candidate is PROMOTED, correctly.
        /// 2. `Catalog.Node` still resolves candidates, so the sweep and tests can compose and
        ///    fight them. Only `SpecOptions` gates them, because offers are the only way a node
        ///    reaches a run.
        ///
        /// Promotion is one edit: move the rows from Candidate* into the live tables.
        /// Doing so is a CONTENT decision, capped by the first-playable budget (roadmap) and
        /// gated on playtest #1 — not something to do because the content compiles.
        /// </summary>
        public static readonly Dictionary<string, SpecNode> CandidateNodes = new Dictionary<string, SpecNode>();
        public static readonly Dictionary<string, List<string>> CandidateOffers = new Dictionary<string, List<string>>();

        private static void Offer(string chassis, Rank rank, string? path, params string[] nodes) =>
            Offers[$"{chassis}|{rank}|{path ?? "-"}"] = new List<string>(nodes);

        /// <summary>Extra pool entries merged onto the live row of the same key when candidates
        /// are enabled. A key with no live row is candidate-only and simply never resolves.</summary>
        private static void CandidateOffer(string chassis, Rank rank, string? path, params string[] nodes) =>
            CandidateOffers[$"{chassis}|{rank}|{path ?? "-"}"] = new List<string>(nodes);

        private static SpecNode Candidate(string id, SpecNode node)
        {
            node.Name = id;
            CandidateNodes[id] = node;
            return node;
        }

        private static SpecNode Node(string id, SpecNode node) { node.Name = id; Nodes[id] = node; return node; }

        static Kits()
        {
            Cleric(); Bulwark(); Shade(); Sharpshot();
            Pyromancer(); Berserker(); Phalanx(); Banneret();
            // The dictionary key IS the chassis id — stamp it rather than repeating it in every
            // definition, so the two can never drift.
            foreach (var kv in Chassis) kv.Value.Id = kv.Key;
        }

        // ---- Cleric — Sister Maren of the Waning Bell (dive #1) --------------------

        private static void Cleric()
        {
            Chassis["cleric"] = new ChassisDef
            {
                Name = "Cleric", MaxHp = 130, ManaMax = 40, RankHp = 25, RankAttack = 2,
                MoveInterval = 5,
                StarterWeapon = Weapons.All["censer"],
                Specializations = { "censer", "staff" },
                // Mercy Aura [MUSTER, ADR 0014]: allies placed within 2 Regen for the fight.
                Passives = { AtStart(Status(StatusKind.Regen, 2, Allies(2))) },
                // Sanctified Pyre: nova on HERSELF — damage enemies, heal allies (self incl).
                Signature = { Dmg(Enemies(1), 12), Heal(Allies(1, exSelf: false), 10) },
            };
            ForkRanks["cleric"] = Rank.B;
            Offer("cleric", Rank.B, null, "cleric.warpriest", "cleric.lifebinder");

            Node("cleric.warpriest", new SpecNode   // DEEPEN the bruiser: bigger Pyre + Burn
            {
                // The Pyre grows and starts to burn — same verb, more of it.
                SignaturePatch = Patch(radius: 1, add: Status(StatusKind.Burn, 3, Enemies(2))),
            });
            Node("cleric.lifebinder", new SpecNode  // SWAP to backline: pulse on the lowest ally
            {
                SignatureOverride = new List<EffectDef>
                    { Heal(Lowest, 18), Status(StatusKind.Haste, 300, Lowest, ticks: 30) },
                // The SWAP is a placement decision (dive #1) — so it now moves her. Before this,
                // "retreat from the scrum" was advice to the player that the unit itself ignored:
                // both forks walked at the nearest enemy at identical speed.
                Standoff = 3,
            });

            Offer("cleric", Rank.A, "cleric.warpriest", "cleric.warpriest.scorched", "cleric.warpriest.contagion");
            Node("cleric.warpriest.scorched", new SpecNode  // swings splash the nearest Burning enemy
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, RootEv),
                    Dmg(Nearest(must: StatusKind.Burn), 0, pctOfEvent: 30)) },
            });
            Node("cleric.warpriest.contagion", new SpecNode // the corpse's remaining pool moves on — harder, not longer
            {
                Triggers = { On(EventKind.Death, W(TgtAlly(not: true), TgtHas(StatusKind.Burn)),
                    PassStack(StatusKind.Burn, Nearest(atCorpse: true, exAnchor: true))) },
            });
            Offer("cleric", Rank.S, "cleric.warpriest", "cleric.warpriest.conflagration", "cleric.warpriest.zeal");
            Node("cleric.warpriest.conflagration", new SpecNode // detonate every Burning enemy in radius
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner),
                    DmgPerStack(Enemies(2, must: StatusKind.Burn), 1, StatusKind.Burn),
                    Strip(StatusKind.Burn, Enemies(2, must: StatusKind.Burn))) },
            });
            Node("cleric.warpriest.zeal", new SpecNode // while any enemy Burns: faster, and her swings feed her
            {
                StatRules = { Rule(StatKind.AttackSpeed, 300, StatScale.None,
                    new Cond { Kind = CondKind.AnyEnemyHasStatus, Status = StatusKind.Burn }) },
                Triggers = { On(EventKind.DamageDealt,
                    W(SrcOwner, ByAttack, RootEv, new Cond { Kind = CondKind.AnyEnemyHasStatus, Status = StatusKind.Burn }),
                    Heal(Self, 0, pctOfEvent: 30)) },
            });

            Offer("cleric", Rank.A, "cleric.lifebinder", "cleric.lifebinder.grace", "cleric.lifebinder.chorus");
            Node("cleric.lifebinder.grace", new SpecNode    // the pulse leaves healing ground
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Glyph(Lowest, new FieldDef
                {
                    Radius = 1, Ticks = 40, PulseAffects = Affects.Allies,
                    Pulse = { Heal(Self /* ignored: pulse hits occupants */, 4) },
                })) },
            });
            Node("cleric.lifebinder.chorus", new SpecNode   // every 3rd swing: Haste ally / Slow enemy
            {
                Triggers =
                {
                    On(EventKind.Attack, W(SrcOwner, Nth(3), RootEv, TgtAlly()), Status(StatusKind.Haste, 300, EvTgt, ticks: 20)),
                    On(EventKind.Attack, W(SrcOwner, Nth(3), RootEv, TgtAlly(not: true)), Status(StatusKind.Slow, 300, EvTgt, ticks: 20)),
                },
            });
            Offer("cleric", Rank.S, "cleric.lifebinder", "cleric.lifebinder.greatchorus", "cleric.lifebinder.sanctuary");
            Node("cleric.lifebinder.greatchorus", new SpecNode // fires twice — second re-resolves the lowest
            {
                SignaturePatch = Patch(repeat: 2),
            });
            Node("cleric.lifebinder.sanctuary", new SpecNode
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Shield(Lowest, 12)) },
            });
        }

        // ---- Bulwark — Brakka, Shieldmaid of the Bronze Hour (dive #2) -------------

        private static void Bulwark()
        {
            Chassis["bulwark"] = new ChassisDef
            {
                Name = "Bulwark", MaxHp = 200, ManaMax = 30, RankHp = 40, RankAttack = 1,
                MoveInterval = 7,                    // the wall arrives last and leaves never
                StarterWeapon = Weapons.All["towershield"],
                Specializations = { "towershield", "mace" },
                Passives = { AtStart(Shield(Self, 30)) },           // Bastion
                Signature = { Dmg(Nearest(), 10), Status(StatusKind.Stun, 0, Nearest(), ticks: 10) }, // Shield Slam
            };
            ForkRanks["bulwark"] = Rank.B;
            Offer("bulwark", Rank.B, null, "bulwark.juggernaut", "bulwark.warden");

            Node("bulwark.juggernaut", new SpecNode // Slam hits every adjacent enemy, longer Stun
            {
                SignatureOverride = new List<EffectDef>
                    { Dmg(Enemies(1), 10), Status(StatusKind.Stun, 0, Enemies(1), ticks: 15) },
            });
            Node("bulwark.warden", new SpecNode     // the challenge: Taunt r3 + self-Shield, no damage
            {
                SignatureOverride = new List<EffectDef>
                    { Status(StatusKind.Taunt, 0, Enemies(3), ticks: 40), Shield(Self, 20) },
            });

            Offer("bulwark", Rank.A, "bulwark.juggernaut", "bulwark.juggernaut.concussive", "bulwark.juggernaut.shockwave");
            Node("bulwark.juggernaut.concussive", new SpecNode
            {
                Triggers = { On(EventKind.Attack, W(SrcOwner, Nth(3), RootEv, TgtAlly(not: true)),
                    Status(StatusKind.Slow, 300, EvTgt, ticks: 20)) },
            });
            Node("bulwark.juggernaut.shockwave", new SpecNode // Shield PER enemy the Slam hits (one event each)
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAbility), Shield(Self, 8)) },
            });
            Offer("bulwark", Rank.S, "bulwark.juggernaut", "bulwark.juggernaut.faultline", "bulwark.juggernaut.grudgekeeper");
            Node("bulwark.juggernaut.faultline", new SpecNode
            {
                SignaturePatch = Patch(radius: 1),   // the Slam reaches a ring farther
            });
            Node("bulwark.juggernaut.grudgekeeper", new SpecNode // the wall swings with its own weight
            {
                StatRules = { Rule(StatKind.AttackFlat, 2, StatScale.ShieldPer10) },
            });

            Offer("bulwark", Rank.A, "bulwark.warden", "bulwark.warden.bellow", "bulwark.warden.rebuke");
            Node("bulwark.warden.bellow", new SpecNode // taunted enemies swing weaker at him
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.AttackDown, 4, Enemies(3), ticks: 40)) },
            });
            Node("bulwark.warden.rebuke", new SpecNode // swings vs HIS taunted grant Shield
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, TgtHas(StatusKind.Taunt)), Shield(Self, 3)) },
            });
            Offer("bulwark", Rank.S, "bulwark.warden", "bulwark.warden.challenge", "bulwark.warden.retribution");
            Node("bulwark.warden.challenge", new SpecNode
            {
                // Wider Taunt, heavier self-Shield. Taunt's magnitude is 0, so the amount scale
                // lands only on the Shield — the patch says what it means without listing the
                // Taunt again.
                SignaturePatch = Patch(radius: 1, amountPct: 175),
            });
            Node("bulwark.warden.retribution", new SpecNode // thorns while they carry his Taunt
            {
                Triggers = { On(EventKind.DamageDealt, W(TgtOwner, SrcHas(StatusKind.Taunt)),
                    Dmg(EvSrc, 0, pctOfEvent: 40)) },
            });
        }

        // ---- Shade — Null, the Redacted (dive #3, LATE-BLOOMER: fork at A) ----------

        private static void Shade()
        {
            Chassis["shade"] = new ChassisDef
            {
                Name = "Shade", MaxHp = 95, ManaMax = 35, RankHp = 18, RankAttack = 3,
                MoveInterval = 3,                    // the fastest body on the board
                // The knife picks its moment: he acquires the WEAKEST enemy, not the closest.
                // With Ambush's opening Leap and the kill→re-Leap engine this is what makes the
                // roster contract ("chains through vulnerable targets") true in the sim rather
                // than only on the page — he strings wounded bodies together instead of
                // brawling whatever he happens to land beside.
                TargetPref = TargetPref.LowestHp,
                StarterWeapon = Weapons.All["daggers"],
                Specializations = { "daggers", "bow" },
                Passives = { AtStart(Leap(Farthest)) },              // Ambush
                Signature = { Dmg(Cur, 25) },                        // Backstab
            };
            ForkRanks["shade"] = Rank.A;                             // ADR 0011 late-bloomer law

            // B: matured signature baseline (kill → re-Leap) rides in BOTH options.
            Offer("shade", Rank.B, null, "shade.killerstempo", "shade.opportunist");
            var maturedSig = On(EventKind.Death, W(SrcOwner), Leap(Farthest));
            Node("shade.killerstempo", new SpecNode
            {
                Triggers = { maturedSig, On(EventKind.Death, W(SrcWithin(2)), Status(StatusKind.Haste, 400, Self, ticks: 20)) },
            });
            Node("shade.opportunist", new SpecNode
            {
                Triggers = { maturedSig, On(EventKind.DamageDealt, W(SrcOwner, ByAbility, TgtBelow(50)),
                    Dmg(EvTgt, 0, pctOfEvent: 50)) },
            });

            Offer("shade", Rank.A, null, "shade.reaper", "shade.phantom");   // THE FORK (risk profile)
            Node("shade.reaper", new SpecNode // the gamble: big crit bonus, crits below 30% Execute
            {
                SpawnStatuses = { (StatusKind.CritUp, 25) },
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, Crit, TgtBelow(30)), Execute(EvTgt)) },
            });
            Node("shade.phantom", new SpecNode // the phase: burst-in-window → Phase; re-entry Leap + permanent AttackUp
            {
                Triggers =
                {
                    On(EventKind.DamageDealt, W(TgtOwner, RecentDmg(25), OwnerHas(StatusKind.Phase, not: true)),
                        Status(StatusKind.Phase, 0, Self, ticks: 30)),
                    On(EventKind.StatusExpired, W(TgtOwner, StatusIs(StatusKind.Phase)),
                        Leap(Farthest), Status(StatusKind.AttackUp, 3, Self)),
                },
            });

            Offer("shade", Rank.S, "shade.reaper", "shade.reaper.twist", "shade.reaper.widowmaker");
            Node("shade.reaper.twist", new SpecNode // crits Mark the victim; Backstab twists marked wounds
            {
                Triggers =
                {
                    On(EventKind.DamageDealt, W(SrcOwner, ByAttack, Crit), Status(StatusKind.Mark, 0, EvTgt, ticks: 30)),
                    On(EventKind.DamageDealt, W(SrcOwner, ByAbility, TgtHas(StatusKind.Mark)), Dmg(EvTgt, 0, pctOfEvent: 50)),
                },
            });
            Node("shade.reaper.widowmaker", new SpecNode { SpawnStatuses = { (StatusKind.CritMultUp, 1500) } });
            Offer("shade", Rank.S, "shade.phantom", "shade.phantom.hereandgone", "shade.phantom.coldreturn");
            Node("shade.phantom.hereandgone", new SpecNode // offense-scheduled phasing
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Phase, 0, Self, ticks: 15)) },
            });
            Node("shade.phantom.coldreturn", new SpecNode // first swing after each Phase is an auto crit
            {
                Triggers = { On(EventKind.StatusExpired, W(TgtOwner, StatusIs(StatusKind.Phase)),
                    Status(StatusKind.NextSwingCrit, 0, Self, swings: 1)) },
            });
        }

        // ---- Sharpshot — Calamity Vance, the Last Deadeye (dive #4) -----------------

        private static void Sharpshot()
        {
            Chassis["sharpshot"] = new ChassisDef
            {
                Name = "Sharpshot", MaxHp = 90, ManaMax = 45, RankHp = 15, RankAttack = 3,
                MoveInterval = 5,
                // Full Draw pays her per hex of distance, but every unit advanced to exactly max
                // range and then stood there, so her signature stat could only ever decay from
                // first contact onward and she had no tool to defend it. She now gives ground to
                // hold her firing distance — the gradient becomes something she plays, not
                // something that happens to her.
                Standoff = 4,
                StarterWeapon = Weapons.All["bow"],
                Specializations = { "bow", "musket" },
                // Full Draw: +2 damage per hex to her target — the visible gradient.
                StatRules = { Rule(StatKind.AttackFlat, 2, StatScale.DistanceToTarget) },
                Signature = { Dmg(Line(), 14) },                     // Piercing Bolt
            };
            ForkRanks["sharpshot"] = Rank.B;
            Offer("sharpshot", Rank.B, null, "sharpshot.sniper", "sharpshot.volleyer");

            Node("sharpshot.sniper", new SpecNode // the nuker: the bolt aims at the FARTHEST enemy, board-length line
            {
                SignatureOverride = new List<EffectDef> { Dmg(LineFar(0), 35) },
            });
            Node("sharpshot.volleyer", new SpecNode // the scaler: the cast feeds the autos (v1.0 rework)
            {
                SignatureOverride = new List<EffectDef>(),           // the cast IS the trigger below
                Triggers = { On(EventKind.Cast, W(SrcOwner),
                    Status(StatusKind.MultiShotRamp, 1, Self),
                    Status(StatusKind.MultiShotWindow, 60, Self, swings: 4)) },
            });

            Offer("sharpshot", Rank.A, "sharpshot.sniper", "sharpshot.sniper.twinnock", "sharpshot.sniper.overpen");
            Node("sharpshot.sniper.twinnock", new SpecNode // every 3rd swing fires twice at 50%
            {
                Triggers = { On(EventKind.Attack, W(SrcOwner, Nth(3), RootEv), Swing(EvTgt, pct: 50)) },
            });
            Node("sharpshot.sniper.overpen", new SpecNode // each body passed adds +25% for those farther down
            {
                SignaturePatch = Patch(escalate: 25),
            });
            Offer("sharpshot", Rank.S, "sharpshot.sniper", "sharpshot.sniper.onebreath", "sharpshot.sniper.killwindow");
            Node("sharpshot.sniper.onebreath", new SpecNode // half as often, twice as hard
            {
                StatRules = { Rule(StatKind.AttackSpeed, -500) },
                SpawnStatuses = { (StatusKind.SwingAmpPct, 100) }, // a true double (permanent: not swing-scoped)
            });
            Node("sharpshot.sniper.killwindow", new SpecNode // kills refund half her mana
            {
                Triggers = { On(EventKind.Death, W(SrcOwner), Mana(Self, 22)) },
            });

            Offer("sharpshot", Rank.A, "sharpshot.volleyer", "sharpshot.volleyer.rollingfire", "sharpshot.volleyer.splitheads");
            Node("sharpshot.volleyer.rollingfire", new SpecNode
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Haste, 300, Self, swings: 3)) },
            });
            Node("sharpshot.volleyer.splitheads", new SpecNode // extra arrows splinter into whoever stands beside their victim
            {
                // Extras are depth-1 rider-hits — Not(IsRootEvent) picks them out.
                Triggers = { On(EventKind.DamageDealt,
                    W(SrcOwner, ByAttack, new Cond { Kind = CondKind.IsRootEvent, Not = true }),
                    Dmg(Enemies(1, atVictim: true, exAnchor: true), 0, pctOfEvent: 40)) },
            });
            Offer("sharpshot", Rank.S, "sharpshot.volleyer", "sharpshot.volleyer.arrowstorm", "sharpshot.volleyer.trueflight");
            Node("sharpshot.volleyer.arrowstorm", new SpecNode // starts as if she'd already cast twice
            {
                SpawnStatuses = { (StatusKind.MultiShotRamp, 2) },
                Triggers = { AtStart(Status(StatusKind.MultiShotWindow, 60, Self, swings: 4)) },
            });
            Node("sharpshot.volleyer.trueflight", new SpecNode // width becomes weight: extras to 100%
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.MultiShotWindow, 40, Self, swings: 4)) },
            });

            SharpshotSpotterCandidate();
        }

        /// <summary>
        /// CANDIDATE — Sharpshot's third path (unreachable; see <see cref="CandidateNodes"/>).
        ///
        /// Why this hero first: ADR 0011's own roster audit flags Sniper and Volleyer as
        /// **double-DEEPEN** — both are "ranged dps, more so" — which breaks the fork law that at
        /// least one path per class must ADD or SWAP. Spotter is the missing SWAP: ranged dps →
        /// support. It is also the only proposed third path that needs no new sim vocabulary,
        /// because <see cref="StatusKind.DamageTakenUp"/> already exists (Reckless Swing's dial),
        /// so the Mark's teamwide amplification is a plain debuff every ally reads for free.
        ///
        /// DEVIATIONS from `Inbox/warband_roster_expansion_plan.md`, and why. The plan's A/S nodes
        /// are mostly team-facing ("every third ALLIED attack", "allies at least 3 hexes away",
        /// "allies that damaged it"), but a SpecNode carries owner-side Triggers only — the
        /// team-facing layer is Banner/Inscription TeamTriggers. So the nodes below keep the plan's
        /// INTENT (focus fire, uptime, bounded chain) and express it through what Calamity herself
        /// does with the Mark. Kill Order pays the warband in Mana rather than tracking damage
        /// contributors, which would need a per-enemy ledger the sim does not keep.
        ///
        /// Numbers are placeholder per ADR 0011's value doctrine — the sweep decides, not this file.
        /// </summary>
        private static void SharpshotSpotterCandidate()
        {
            // Joins the FORK pool, so the fork-rank law shows all three paths — identity is never
            // withheld (RunController.SpecPick).
            CandidateOffer("sharpshot", Rank.B, null, "sharpshot.spotter");

            Candidate("sharpshot.spotter", new SpecNode // the SWAP: her damage moves into the party
            {
                // The bolt stops being the payload and becomes the paint: it flies at the farthest
                // enemy for a fraction of Sniper's weight, and everyone else collects.
                SignatureOverride = new List<EffectDef>
                {
                    Dmg(LineFar(0), 6),
                    Status(StatusKind.DamageTakenUp, 15, Farthest, ticks: 60),
                    Status(StatusKind.Mark, 0, Farthest, ticks: 60),   // the tag her tree reads
                },
            });

            // A — three amplifiers: uptime · deepen · her own damage back.
            CandidateOffer("sharpshot", Rank.A, "sharpshot.spotter",
                "sharpshot.spotter.rangefinder", "sharpshot.spotter.openseason",
                "sharpshot.spotter.marksmansdue");

            Candidate("sharpshot.spotter.rangefinder", new SpecNode // her autos paint too
            {
                Triggers = { On(EventKind.Attack, W(SrcOwner, TgtHas(StatusKind.Mark, not: true), RootEv),
                    Status(StatusKind.DamageTakenUp, 15, EvTgt, ticks: 30),
                    Status(StatusKind.Mark, 0, EvTgt, ticks: 30)) },
            });
            Candidate("sharpshot.spotter.openseason", new SpecNode // 15% → 25%, paid for in her own weight
            {
                SignaturePatch = Patch(amountPct: 60,
                    add: Status(StatusKind.DamageTakenUp, 10, Farthest, ticks: 60)),
            });
            Candidate("sharpshot.spotter.marksmansdue", new SpecNode // she is also an ally of hers
            {
                Triggers = { On(EventKind.Attack, W(SrcOwner, TgtHas(StatusKind.Mark), RootEv),
                    Swing(EvTgt, pct: 50)) },
            });

            // S — three crowns: bounded chain · permanence · the party payoff.
            CandidateOffer("sharpshot", Rank.S, "sharpshot.spotter",
                "sharpshot.spotter.passingthescope", "sharpshot.spotter.longwatch",
                "sharpshot.spotter.killorder");

            Candidate("sharpshot.spotter.passingthescope", new SpecNode // the mark outlives its target
            {
                Triggers = { On(EventKind.Death, W(TgtHas(StatusKind.Mark), TgtAlly(not: true)),
                    Status(StatusKind.DamageTakenUp, 15, Farthest, ticks: 30),
                    Status(StatusKind.Mark, 0, Farthest, ticks: 30)) },
            });
            Candidate("sharpshot.spotter.longwatch", new SpecNode // the paint never fully washes off
            {
                SignaturePatch = Patch(add: Status(StatusKind.DamageTakenUp, 10, Farthest, ticks: -1)),
            });
            Candidate("sharpshot.spotter.killorder", new SpecNode // focus fire buys the whole warband tempo
            {
                Triggers = { On(EventKind.Death, W(TgtHas(StatusKind.Mark), TgtAlly(not: true)),
                    Mana(Allies(99, exSelf: false), 8)) },
            });
        }

        // ---- Pyromancer — Ilion-7, Cinder of a Dead Star (dive #5) ------------------

        private static FieldDef Blaze(int radius, int ticks) => new FieldDef
        {
            Radius = radius, Ticks = ticks, PulseAffects = Affects.Enemies,
            Pulse = { Status(StatusKind.Burn, 2, Self /* ignored: hits occupants */) },
        };

        private static void Pyromancer()
        {
            Chassis["pyromancer"] = new ChassisDef
            {
                Name = "Pyromancer", MaxHp = 85, ManaMax = 50, RankHp = 15, RankAttack = 2,
                MoveInterval = 6,
                Standoff = 3,                        // she keeps her own fire between herself and them
                StarterWeapon = Weapons.All["staff"],
                Specializations = { "staff", "censer" },
                // Firebrand: her swings apply 1 Burn.
                Passives = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, RootEv), Status(StatusKind.Burn, 1, EvTgt)) },
                Signature = { Glyph(Cur, Blaze(radius: 1, ticks: 80)) },   // Fire Glyph
            };
            ForkRanks["pyromancer"] = Rank.B;
            Offer("pyromancer", Rank.B, null, "pyromancer.inferno", "pyromancer.starfall");

            Node("pyromancer.inferno", new SpecNode // the ground is the weapon: r2 + spread on kill
            {
                SignaturePatch = Patch(fieldRadius: 2),
                Triggers = { On(EventKind.Death, W(TgtAlly(not: true), TgtHas(StatusKind.Burn)),
                    Glyph(EvTgt, Blaze(radius: 1, ticks: 60))) },
            });
            Node("pyromancer.starfall", new SpecNode // the target is the weapon: no field at all
            {
                SignatureOverride = new List<EffectDef> { Dmg(Cur, 30), Status(StatusKind.Burn, 8, Cur) },
            });

            Offer("pyromancer", Rank.A, "pyromancer.inferno", "pyromancer.inferno.chokingsmoke", "pyromancer.inferno.stoke");
            Node("pyromancer.inferno.chokingsmoke", new SpecNode // enemies she hurts INSIDE her fields choke
            {
                // Fires on every damage she sources into a field-stander — incl. her own
                // Burn ticks, so standing in the fire means staying slowed.
                Triggers = { On(EventKind.DamageDealt,
                    W(SrcOwner, new Cond { Kind = CondKind.TargetInFieldOfOwner }),
                    Status(StatusKind.Slow, 150, EvTgt, ticks: 15)) },
            });
            Node("pyromancer.inferno.stoke", new SpecNode // her swings stoke enemies standing in the coals
            {
                Triggers = { On(EventKind.DamageDealt,
                    W(SrcOwner, ByAttack, RootEv, new Cond { Kind = CondKind.TargetInFieldOfOwner }),
                    Status(StatusKind.Burn, 2, EvTgt)) },
            });
            Offer("pyromancer", Rank.S, "pyromancer.inferno", "pyromancer.inferno.worldalight", "pyromancer.inferno.everburn");
            Node("pyromancer.inferno.worldalight", new SpecNode // ignite under EVERY Burning enemy
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner),
                    Glyph(Enemies(99, must: StatusKind.Burn), Blaze(radius: 0, ticks: 60))) },
            });
            Node("pyromancer.inferno.everburn", new SpecNode // the board slowly becomes fire
            {
                // Inherits Inferno's radius instead of restating it — the two nodes can no longer
                // disagree about how big her fire is.
                SignaturePatch = Patch(fieldTicks: -1),
            });

            Offer("pyromancer", Rank.A, "pyromancer.starfall", "pyromancer.starfall.detonate", "pyromancer.starfall.kindling");
            Node("pyromancer.starfall.detonate", new SpecNode // consume the pool, +2 per stack
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner),
                    DmgPerStack(Cur, 2, StatusKind.Burn), Strip(StatusKind.Burn, Cur)) },
            });
            Node("pyromancer.starfall.kindling", new SpecNode
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, RootEv), Status(StatusKind.Burn, 1, EvTgt)) },
            });
            Offer("pyromancer", Rank.S, "pyromancer.starfall", "pyromancer.starfall.dyingstar", "pyromancer.starfall.whiteheat");
            Node("pyromancer.starfall.dyingstar", new SpecNode // kill → free recast on the nearest Burning enemy
            {
                Triggers = { On(EventKind.Death, W(SrcOwner), Recast(Nearest(must: StatusKind.Burn))) },
            });
            Node("pyromancer.starfall.whiteheat", new SpecNode // enemy Burn ticks deal double
            {
                Triggers = { AtStart(Status(StatusKind.BurnAmp, 100, Enemies(99))) },
            });
        }

        // ---- Berserker — Ulfrik, Who Burns His Hours (dive #6) ----------------------

        private static void Berserker()
        {
            Chassis["berserker"] = new ChassisDef
            {
                Name = "Berserker", MaxHp = 160, ManaMax = 40, RankHp = 30, RankAttack = 3,
                MoveInterval = 4,                    // he closes
                StarterWeapon = Weapons.All["greataxe"],
                Specializations = { "greataxe", "daggers" },
                // Burning Hours: faster the lower his HP (per 10% missing).
                StatRules = { Rule(StatKind.AttackSpeed, 100, StatScale.MissingHpPct10) },
                Signature = new List<EffectDef>(),                   // Frenzy is the trigger below
                Passives = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Frenzied, 0, Self, swings: 4)) },
            };
            ForkRanks["berserker"] = Rank.B;
            Offer("berserker", Rank.B, null, "berserker.bloodreaver", "berserker.rampager");

            Node("berserker.bloodreaver", new SpecNode // ADD drain bruiser: Frenzy swings lifesteal
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, OwnerHas(StatusKind.Frenzied)),
                    Heal(Self, 0, pctOfEvent: 60)) },
            });
            Node("berserker.rampager", new SpecNode // the whirlwind: Frenzy swings splash around him
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, OwnerHas(StatusKind.Frenzied), RootEv),
                    Dmg(Enemies(1), 0, pctOfEvent: 40)) },
            });

            Offer("berserker", Rank.A, "berserker.bloodreaver", "berserker.bloodreaver.scent", "berserker.bloodreaver.redharvest");
            Node("berserker.bloodreaver.scent", new SpecNode
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, TgtBelow(50), RootEv),
                    Dmg(EvTgt, 0, pctOfEvent: 30)) },
            });
            Node("berserker.bloodreaver.redharvest", new SpecNode // Frenzy kills extend the window
            {
                Triggers = { On(EventKind.Death, W(SrcOwner, OwnerHas(StatusKind.Frenzied)),
                    Status(StatusKind.Frenzied, 0, Self, swings: 2)) },
            });
            Offer("berserker", Rank.S, "berserker.bloodreaver", "berserker.bloodreaver.deathless", "berserker.bloodreaver.crimsontide");
            Node("berserker.bloodreaver.deathless", new SpecNode // the sanctioned cheat-death
            {
                SpawnStatuses = { (StatusKind.CheatDeath, 0) },
                Triggers = { On(EventKind.CheatDeath, W(TgtOwner), Status(StatusKind.Frenzied, 0, Self, swings: 4)) },
            });
            Node("berserker.bloodreaver.crimsontide", new SpecNode { SpawnStatuses = { (StatusKind.OverhealToShield, 0) } });

            Offer("berserker", Rank.A, "berserker.rampager", "berserker.rampager.reckless", "berserker.rampager.aftershock");
            Node("berserker.rampager.reckless", new SpecNode // the risk dial — feeds his own innate
            {
                SpawnStatuses = { (StatusKind.AttackUp, 4), (StatusKind.DamageTakenUp, 25) },
            });
            Node("berserker.rampager.aftershock", new SpecNode // the window closes with a full ring
            {
                Triggers = { On(EventKind.StatusExpired, W(TgtOwner, StatusIs(StatusKind.Frenzied)),
                    Dmg(Enemies(1), 15)) },
            });
            Offer("berserker", Rank.S, "berserker.rampager", "berserker.rampager.avalanche", "berserker.rampager.noquarter");
            Node("berserker.rampager.avalanche", new SpecNode // the longer storm
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Frenzied, 0, Self, swings: 2)) },
            });
            Node("berserker.rampager.noquarter", new SpecNode // width becomes weight: cleave to 100%
            {
                CleaveBonusPct = 75, // greataxe base 25 → full damage
            });
        }

        // ---- Phalanx — Leonnatos of the Unbroken Line (dive #7) ---------------------

        private static void Phalanx()
        {
            Chassis["phalanx"] = new ChassisDef
            {
                Name = "Phalanx", MaxHp = 150, ManaMax = 35, RankHp = 30, RankAttack = 2,
                MoveInterval = 6,                    // the line moves at the line's pace
                StarterWeapon = Weapons.All["pike"],
                Specializations = { "pike", "towershield" },
                Passives =
                {
                    // Riposte: one charge, refreshed on cast; the answer is directional (law).
                    AtStart(Status(StatusKind.CounterCharge, 1, Self)),
                    On(EventKind.Cast, W(SrcOwner), Status(StatusKind.CounterCharge, 1, Self)),
                    On(EventKind.Attack, W(TgtOwner, OwnerHas(StatusKind.CounterCharge), RootEv),
                        Swing(EvSrc, counter: true), Strip(StatusKind.CounterCharge, Self)),
                },
                Signature = { Dmg(Line(3), 12) },                    // Skewer: target + the hex behind
            };
            ForkRanks["phalanx"] = Rank.B;
            Offer("phalanx", Rank.B, null, "phalanx.pikewall", "phalanx.lancer");

            Node("phalanx.pikewall", new SpecNode // the wall: counter EVERYTHING + the Leap punish
            {
                Triggers =
                {
                    On(EventKind.Attack, W(TgtOwner, RootEv), Swing(EvSrc, counter: true)),
                    On(EventKind.Leap, W(SrcEnemy, SrcWithin(2)),
                        Swing(EvSrc, counter: true), Status(StatusKind.Taunt, 0, EvSrc, ticks: 40)),
                },
            });
            Node("phalanx.lancer", new SpecNode // the lunge: every enemy on the line
            {
                SignaturePatch = Patch(line: 4),
            });

            Offer("phalanx", Rank.A, "phalanx.pikewall", "phalanx.pikewall.spearpoint", "phalanx.pikewall.sharprebuke");
            Node("phalanx.pikewall.spearpoint", new SpecNode // the spacing reward, at exactly max reach
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, TgtAtRange(2), RootEv),
                    Dmg(EvTgt, 0, pctOfEvent: 50)) },
            });
            Node("phalanx.pikewall.sharprebuke", new SpecNode // his ripostes Disarm whoever they strike
            {
                Triggers = { On(EventKind.DamageDealt, W(SrcOwner, ByCounter),
                    Status(StatusKind.Disarm, 0, EvTgt, ticks: 15)) },
            });
            Offer("phalanx", Rank.S, "phalanx.pikewall", "phalanx.pikewall.unbrokenline", "phalanx.pikewall.givenoground");
            Node("phalanx.pikewall.unbrokenline", new SpecNode // allies PLACED adjacent share his Riposte (muster)
            {
                Triggers =
                {
                    AtStart(Status(StatusKind.CounterCharge, 1, Allies(1))),
                    On(EventKind.Cast, W(SrcOwner), Status(StatusKind.CounterCharge, 1, Allies(1))),
                    On(EventKind.Attack, W(TgtAlly(), TgtHas(StatusKind.CounterCharge), RootEv),
                        Swing(EvSrc, counter: true), Strip(StatusKind.CounterCharge, EvTgt)),
                },
            });
            Node("phalanx.pikewall.givenoground", new SpecNode // fortified while his Taunt holds someone
            {
                Triggers = { On(EventKind.StatusApplied, W(SrcOwner, StatusIs(StatusKind.Taunt)),
                    Status(StatusKind.DamageTakenDown, 20, Self, ticks: 40)) },
            });

            Offer("phalanx", Rank.A, "phalanx.lancer", "phalanx.lancer.overreach", "phalanx.lancer.deepthrust");
            Node("phalanx.lancer.overreach", new SpecNode // every auto also stabs the hexes BEHIND the target
            {
                Triggers = { On(EventKind.Attack, W(SrcOwner, RootEv), Dmg(Line(3, behindOnly: true), 4)) },
            });
            Node("phalanx.lancer.deepthrust", new SpecNode // the lunge drives harder per body it passes
            {
                // The wart this used to carry is gone: as a patch, the escalation SURVIVES Sarissa's
                // crown, so "Breach the Line" is finally board-length AND escalating — the build the
                // dive promised, instead of a crown that silently ate its own amplifier.
                SignaturePatch = Patch(escalate: 30),
            });
            Offer("phalanx", Rank.S, "phalanx.lancer", "phalanx.lancer.sarissa", "phalanx.lancer.perfectform");
            Node("phalanx.lancer.sarissa", new SpecNode // the lunge runs board-length
            {
                SignaturePatch = Patch(line: 0),
            });
            Node("phalanx.lancer.perfectform", new SpecNode // the untouched-spearman tempo
            {
                StatRules = { Rule(StatKind.AttackSpeed, 300, StatScale.None, NoEnemyWithin(1)) },
            });
        }

        // ---- Banneret — Capitana Vespera, Banner of the Turning Age (dive #8) -------

        private static void Banneret()
        {
            Chassis["banneret"] = new ChassisDef
            {
                Name = "Banneret", MaxHp = 120, ManaMax = 25, RankHp = 25, RankAttack = 1,
                MoveInterval = 5,
                StarterWeapon = Weapons.All["standard"],
                Specializations = { "standard", "sabre" },
                // Standard-Bearer [MUSTER]: whoever he is TOUCHING when the horns sound is sworn
                // into the Company for the fight — they swing faster, and every rally he calls
                // reaches them, wherever they drift.
                Passives = { AtStart(Status(StatusKind.Haste, 200, Allies(1)),
                                     Status(StatusKind.Mustered, 1, Allies(1))) },
                Signature = { Mana(Company(), 10) },                 // Rally (the Company — ADR 0014)
            };
            ForkRanks["banneret"] = Rank.B;
            Offer("banneret", Rank.B, null, "banneret.herald", "banneret.warcaller");

            Node("banneret.herald", new SpecNode // the shieldward
            {
                SignaturePatch = Patch(add: Shield(Company(), 10)),
            });
            Node("banneret.warcaller", new SpecNode // the dread captain: one cast, both tempos
            {
                SignaturePatch = Patch(add: Status(StatusKind.Slow, 250, Enemies(2), ticks: 25)),
            });

            Offer("banneret", Rank.A, "banneret.herald", "banneret.herald.steady", "banneret.herald.secondwind");
            Node("banneret.herald.steady", new SpecNode // the Company also holds (muster DR)
            {
                Triggers = { AtStart(Status(StatusKind.DamageTakenDown, 15, Allies(1))) },
            });
            Node("banneret.herald.secondwind", new SpecNode // allies below half receive double Rally
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner),
                    Mana(Company(below: 50), 10), Shield(Company(below: 50), 10)) },
            });
            Offer("banneret", Rank.S, "banneret.herald", "banneret.herald.quickening", "banneret.herald.widebanner");
            Node("banneret.herald.quickening", new SpecNode // intensity: the few, faster (doubles the innate)
            {
                Triggers = { AtStart(Status(StatusKind.Haste, 200, Allies(1))) },
            });
            Node("banneret.herald.widebanner", new SpecNode // breadth: muster reach 2 (r1 companions get both)
            {                                               // — a WIDER Company, sworn at placement
                Triggers = { AtStart(Status(StatusKind.Haste, 200, Allies(2)),
                                     Status(StatusKind.Mustered, 1, Allies(2))) },
            });

            Offer("banneret", Rank.A, "banneret.warcaller", "banneret.warcaller.drumbeat", "banneret.warcaller.dreadpresence");
            Node("banneret.warcaller.drumbeat", new SpecNode // Rallied allies' next 3 swings, faster
            {
                Triggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Haste, 300, Company(), swings: 3)) },
            });
            Node("banneret.warcaller.dreadpresence", new SpecNode // enemies beside him swing slower (live aura — clause 3)
            {
                Triggers = { AtStart(Glyph(Self, new FieldDef
                {
                    AttachToOwner = true, Radius = 1, Ticks = -1,
                    PresenceAffects = Affects.Enemies, Presence = { (StatusKind.Slow, 200) },
                })) },
            });
            Offer("banneret", Rank.S, "banneret.warcaller", "banneret.warcaller.bearer", "banneret.warcaller.lastmarch");
            Node("banneret.warcaller.bearer", new SpecNode // Living Inscription (ADR 0026): the
            {                                              // Hourstone pays its bearer — scales with
                                                           // ACTIVATION, never collection size, so
                                                           // Banneret can't become compulsory.
                Triggers = { On(EventKind.TriggerFired, W(FiredTeamRule), Mana(Self, 10)).Guarded() },
            });
            Node("banneret.warcaller.lastmarch", new SpecNode // the whole warband is his Company
            {                                                // — literally: placement stops mattering
                Triggers = { AtStart(Status(StatusKind.Haste, 200, Allies(99)),
                                     Status(StatusKind.Mustered, 1, Allies(99))) },
            });
        }
    }
}
