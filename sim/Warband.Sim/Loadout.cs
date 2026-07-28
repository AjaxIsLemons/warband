using System.Collections.Generic;

namespace Warband.Sim
{
    // ADR 0005: the battle sim never knows items/ranks/trees exist. The run layer
    // composes chassis + weapon + trinkets + spec nodes into ONE resolved UnitDef via
    // this deterministic composer; run-scoped bonuses ride in as spawn statuses.

    /// <summary>
    /// Which enemy a unit walks at and swings at. Combat-grammar.md promised "nearest default;
    /// kits override" from day one and only the default was ever built, so every unit in the game
    /// shared one brain. This is that override hook: pure data, read in <see cref="Battle"/>'s
    /// acquire pass, deterministic tiebreak by ascending id in every case.
    /// The rest of ADR 0013 is unchanged — a preference only decides who you ACQUIRE; stickiness,
    /// Phase and Taunt still own when you re-acquire, and Taunt still overrides everything.
    /// </summary>
    public enum TargetPref
    {
        Nearest,     // ADR 0013 default — the wall, the brawler, the line
        Farthest,    // artillery and divers: reach past the front for what matters
        LowestHp,    // the finisher: the knife picks its moment (Shade)
        HighestHp,   // the breaker: go at the biggest thing on the board
    }

    public sealed class ChassisDef
    {
        public string Id = "";       // stable content key ("bulwark"); Name is the display label
        public string Name = "hero";
        public int MaxHp;
        public int MoveInterval = 5;
        public TargetPref TargetPref = TargetPref.Nearest;
        /// <summary>Preferred fighting distance. >0: while its target is closer than this AND still
        /// inside weapon range, the unit gives ground one hex at a time — and keeps attacking while
        /// it does (ADR 0018 clause 6: nothing is gated on walking). 0 = stands its ground, which
        /// is every unit's behavior today.</summary>
        public int Standoff;
        public int ManaMax;
        public WeaponDef StarterWeapon = null!;   // weapon-required (round 10)
        // The rank stat law (Jake, 2026-07-23): every rank-up carries a flat stat bump
        // alongside its 1-of-2 node offer. Per-chassis, pure data, trivially retunable.
        public int RankHp;                        // +MaxHp per rank step (C→B→A→S)
        public int RankAttack;                    // +Attack per rank step
        public List<string> Specializations = new List<string>(); // ADR 0012 category tags
        public List<EffectDef> Signature = new List<EffectDef>();
        public List<Trigger> Passives = new List<Trigger>();
        public List<StatRule> StatRules = new List<StatRule>();
    }

    /// <summary>Temper tiers (ADR 0015): rarity is a tier on the same weapon, never a
    /// new item. Stat scale is placeholder (+25/+50%).</summary>
    public enum WeaponTier { Worn, Honed, Relic }

    /// <summary>The attack profile lives here (round 10): swap the weapon, change the hero.
    /// ADR 0015: the weapon also carries its category's latent mastery rider — active for
    /// specialists, live for EVERYONE at Relic tier (doubled for specialists).</summary>
    public sealed class WeaponDef
    {
        public string Name = "weapon";
        public string Category = "";  // ADR 0012 tag: matched against the chassis' specializations
        public int Damage;
        public int Interval;
        public int Range;
        /// <summary>Mana earned per completed swing — the weapon's second axis (2026-07-25 systems
        /// review). With a flat rate, cast cadence was purely 1/Interval, so fast weapons won swing
        /// events AND casts AND had equal damage-per-tick. Authored per weapon, the wardrobe becomes
        /// a real trade: daggers spam swings but starve the signature; the musket swings rarely and
        /// each shot is most of a cast.</summary>
        public int ManaPerSwing = Battle.ManaPerAttack;
        public int CritChance;
        public int CritMultFp = 1500;
        public bool HealAutos;        // censer law: swings heal the lowest-HP ally
        public int CleavePct;         // greataxe shape: swing also hits enemies adjacent to target
        public List<Trigger> Triggers = new List<Trigger>();      // on-hit riders etc. (always on)
        public List<StatRule> StatRules = new List<StatRule>();

        // The latent mastery rider (one per category, engine-amplifier law):
        public List<Trigger> MasteryTriggers = new List<Trigger>();
        public List<StatRule> MasteryStatRules = new List<StatRule>();
        public int MasteryRangeBonus; // bow's physics rider (+1 range) — reach IS its engine
    }

    public sealed class TrinketDef
    {
        public string Name = "trinket";
        public int HpBonus;
        public int ManaMaxDelta;
        public List<Trigger> Triggers = new List<Trigger>();
        public List<StatRule> StatRules = new List<StatRule>();
        public List<(StatusKind Kind, int Mag)> SpawnStatuses = new List<(StatusKind, int)>();
    }

    /// <summary>
    /// A node's modification of the signature it INHERITS, rather than a replacement for it
    /// (2026-07-25 systems review §4). Overrides are last-wins, so an S crown silently erased an
    /// A amplifier's texture — Sarissa kept Deep Thrust's length and dropped its escalation — and
    /// "the same thing, bigger" could only be authored by copy-pasting the whole effect list with
    /// one number changed (faultline, challenge, everburn, greatchorus all did exactly that).
    ///
    /// Law: a node that changes the VERB still uses <see cref="SpecNode.SignatureOverride"/>; a node
    /// that changes the DEGREE declares a patch. Patches apply in node order on top of whatever
    /// override is current, so B-fork → A-amplifier → S-crown composes instead of colliding.
    /// </summary>
    public sealed class SignaturePatch
    {
        public int RadiusDelta;        // += Selector.Range on radius selectors (AlliesWithin/EnemiesWithin)
        public int? LineRange;         // = Selector.Range on line selectors (0 = board length)
        public int AmountPct = 100;    // scales every effect's Amount
        public int? Escalate;          // = EscalatePctPerIndex on every effect
        public int? FieldRadius;       // = Field.Radius on every glyph the signature creates
        public int? FieldTicks;        // = Field.Ticks (<0 = rest of the fight)
        public int Repeat = 1;         // resolve the whole list N times (Great Chorus)
        public List<EffectDef> Add = new List<EffectDef>();   // appended after the patched list
    }

    /// <summary>A spec-tree choice: same primitive bundle as a trinket + an optional signature
    /// override or patch (the fork "transform" is content discipline — ADR 0005).</summary>
    public sealed class SpecNode
    {
        public string Name = "node";
        public int HpBonus;
        public List<Trigger> Triggers = new List<Trigger>();
        public List<StatRule> StatRules = new List<StatRule>();
        public List<(StatusKind Kind, int Mag)> SpawnStatuses = new List<(StatusKind, int)>(); // trinket-shaped (ADR 0005)
        public List<EffectDef>? SignatureOverride;
        public SignaturePatch? SignaturePatch;
        /// <summary>A node may re-aim the unit, not just re-weight it. This is what lets a fork
        /// change the HAT at the behavior layer instead of only the payload layer — a backline
        /// SWAP can actually make the hero stand back.</summary>
        public TargetPref? TargetPref;
        public int? Standoff;
        public int CleaveBonusPct;  // added to the weapon's cleave (No Quarter: width becomes weight)
        public bool DoublesBanners; // Bearer of the Mark (ADR: first cross-layer node) — read by the run layer
    }

    public sealed class ComposedLoadout
    {
        public UnitDef Def = null!;
        public List<Status> SpawnStatuses = new List<Status>();
    }

    public static class Loadout
    {
        /// <summary>Tier stat multipliers, fixed-point /100 (placeholder magnitudes).</summary>
        private static int TierScale(WeaponTier tier) =>
            tier == WeaponTier.Relic ? 150 : tier == WeaponTier.Honed ? 125 : 100;

        /// <summary>Merge order (fixed, documented): chassis → weapon → trinkets → nodes.
        /// Last node with a SignatureOverride wins. Null weapon = the starter.
        /// ADR 0015 rider gate: mastered → rider on · Relic → rider on for anyone ·
        /// Relic AND mastered → rider doubled (included twice — additive rider law).</summary>
        public static ComposedLoadout Compose(
            ChassisDef chassis,
            WeaponDef? weapon = null,
            IEnumerable<TrinketDef>? trinkets = null,
            IEnumerable<SpecNode>? nodes = null,
            WeaponTier tier = WeaponTier.Worn,
            bool mastered = false,
            int rankSteps = 0)
        {
            // See AddRules — every rule that reaches a UnitDef goes through it, so identity is a
            // property of composition rather than of authoring.
            var w = weapon ?? chassis.StarterWeapon;
            int scale = TierScale(tier);
            var def = new UnitDef
            {
                Name = chassis.Name,
                MaxHp = chassis.MaxHp + chassis.RankHp * rankSteps,
                MoveInterval = chassis.MoveInterval,
                TargetPref = chassis.TargetPref,
                Standoff = chassis.Standoff,
                ManaMax = chassis.ManaMax,
                Attack = w.Damage * scale / 100 + chassis.RankAttack * rankSteps,
                AttackInterval = w.Interval,
                ManaPerSwing = w.ManaPerSwing,
                Range = w.Range,
                CritChance = w.CritChance,
                CritMultFp = w.CritMultFp,
                HealAutos = w.HealAutos,
                CleavePct = w.CleavePct,
                ChassisId = chassis.Id,
                WeaponName = w.Name,
                WeaponTier = tier,
            };
            // Cloned, not shared: node patches mutate this list in place, and the chassis'
            // own Signature is a static catalog instance reused by every later composition.
            foreach (var e in chassis.Signature) def.Signature.Add(e.Clone());
            AddRules(def, chassis.Passives, chassis.StatRules, chassis.Id);
            AddRules(def, w.Triggers, w.StatRules, w.Name);

            bool riderLive = mastered || tier == WeaponTier.Relic;
            int riderCopies = (mastered && tier == WeaponTier.Relic) ? 2 : riderLive ? 1 : 0;
            for (int i = 0; i < riderCopies; i++)
            {
                AddRules(def, w.MasteryTriggers, w.MasteryStatRules, w.Name + "/mastery");
                def.Range += w.MasteryRangeBonus;
            }

            var result = new ComposedLoadout { Def = def };

            if (trinkets != null)
                foreach (var t in trinkets)
                {
                    def.MaxHp += t.HpBonus;
                    def.ManaMax += t.ManaMaxDelta;
                    AddRules(def, t.Triggers, t.StatRules, t.Name);
                    def.Traits.Add(t.Name);
                    foreach (var (kind, mag) in t.SpawnStatuses)
                        result.SpawnStatuses.Add(new Status { Kind = kind, Mag = mag, TicksLeft = -1 });
                }

            if (nodes != null)
                foreach (var n in nodes)
                {
                    def.MaxHp += n.HpBonus;
                    def.CleavePct += n.CleaveBonusPct;
                    AddRules(def, n.Triggers, n.StatRules, n.Name);
                    def.Traits.Add(n.Name);
                    if (n.TargetPref.HasValue) def.TargetPref = n.TargetPref.Value;
                    if (n.Standoff.HasValue) def.Standoff = n.Standoff.Value;
                    foreach (var (kind, mag) in n.SpawnStatuses)
                        result.SpawnStatuses.Add(new Status { Kind = kind, Mag = mag, TicksLeft = -1 });
                    // Override REPLACES the verb, patch MODIFIES the degree. A node may carry both:
                    // the override lands first, so a fork can restate its signature and shape it in
                    // the same node. Applying in node order is what makes B → A → S compose.
                    if (n.SignatureOverride != null)
                    {
                        def.Signature.Clear();
                        foreach (var e in n.SignatureOverride) def.Signature.Add(e.Clone());
                    }
                    if (n.SignaturePatch != null)
                        ApplyPatch(def.Signature, n.SignaturePatch);
                }

            if (def.ManaMax < 0) def.ManaMax = 0;
            return result;
        }

        /// <summary>
        /// Apply one node's patch to the signature it inherited. Every knob here modifies what is
        /// already present — a patch can never introduce a verb the inherited signature lacked
        /// (that is exactly what <see cref="SpecNode.SignatureOverride"/> is for). That split is
        /// what keeps "the same thing, bigger" a one-liner and "a different thing" explicit.
        /// </summary>
        /// <summary>
        /// The ONE door every rule walks through on its way into a <see cref="UnitDef"/>, so that
        /// render identity is a property of composition and never of authoring
        /// (Design/passive-legibility.md, law L1). A new spec node or trinket is identified the day
        /// it is written, with no extra field to remember and nothing to keep in sync.
        ///
        /// <para>CLONES, always. The catalog hands out the SAME Trigger/StatRule instance to every
        /// composition — stamping in place would rewrite the kit for every later one in the process,
        /// which is exactly the bug the Signature clone above exists to prevent.</para>
        ///
        /// <para>Ids: the first rule a source contributes takes the bare source id, later ones get
        /// <c>#2</c>, <c>#3</c>. That ordering matters — appending a SECOND passive to a chassis must
        /// not rename the first and silently orphan its authored tell row. Triggers and StatRules
        /// count separately and may share an id: they arrive as different EventKinds, so a tell row
        /// is already unambiguous, and "this node's passive" reads better as one name than two.</para>
        /// </summary>
        private static void AddRules(UnitDef def, List<Trigger> triggers, List<StatRule> rules, string source)
        {
            for (int i = 0; i < triggers.Count; i++)
            {
                var t = triggers[i].CloneForComposition();
                t.RuleId = i == 0 ? source : source + "#" + (i + 1);
                def.Triggers.Add(t);
            }
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i].CloneForComposition();
                r.RuleId = i == 0 ? source : source + "#" + (i + 1);
                def.StatRules.Add(r);
            }
        }

        private static void ApplyPatch(List<EffectDef> sig, SignaturePatch p)
        {
            foreach (var e in sig)
            {
                // Radius and line length are both Selector.Range but mean different things, so
                // they never share a knob: a radius grows by a delta, a line length is set
                // outright (0 = board-length). Bumping a board-length line by +1 would silently
                // truncate it to a single hex — the exact class of bug this model exists to kill.
                if (p.RadiusDelta != 0 &&
                    (e.Select.Kind == SelKind.AlliesWithin || e.Select.Kind == SelKind.EnemiesWithin))
                {
                    e.Select.Range += p.RadiusDelta;
                    if (e.Select.Range < 0) e.Select.Range = 0;
                }
                if (p.LineRange.HasValue &&
                    (e.Select.Kind == SelKind.EnemiesOnLineThroughTarget ||
                     e.Select.Kind == SelKind.EnemiesOnLineThroughFarthest))
                    e.Select.Range = p.LineRange.Value;

                if (p.AmountPct != 100) e.Amount = e.Amount * p.AmountPct / 100;
                if (p.Escalate.HasValue) e.EscalatePctPerIndex = p.Escalate.Value;
                if (e.Field != null)
                {
                    if (p.FieldRadius.HasValue) e.Field.Radius = p.FieldRadius.Value;
                    if (p.FieldTicks.HasValue) e.Field.Ticks = p.FieldTicks.Value;
                }
            }

            if (p.Repeat > 1)
            {
                int once = sig.Count;
                for (int r = 1; r < p.Repeat; r++)
                    for (int i = 0; i < once; i++)
                        sig.Add(sig[i].Clone());
            }

            // Added effects are the node's own, authored at final magnitude — they are appended
            // AFTER the patch pass so the node's numbers mean what they say.
            foreach (var e in p.Add) sig.Add(e.Clone());
        }

        /// <summary>Spawn a battle-ready unit from a composed loadout + run-earned bonuses.</summary>
        public static UnitState Spawn(int id, int team, ComposedLoadout loadout, Hex pos,
                                      IEnumerable<Status>? runBonuses = null)
        {
            var u = UnitState.Spawn(id, team, loadout.Def, pos);
            foreach (var s in loadout.SpawnStatuses)
                u.Statuses.Add(new Status { Kind = s.Kind, Mag = s.Mag, TicksLeft = s.TicksLeft, SourceId = id });
            if (runBonuses != null)
                foreach (var s in runBonuses)
                    u.Statuses.Add(new Status { Kind = s.Kind, Mag = s.Mag, TicksLeft = -1, SourceId = id });
            return u;
        }
    }
}
