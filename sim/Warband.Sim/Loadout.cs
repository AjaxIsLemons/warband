using System.Collections.Generic;

namespace Warband.Sim
{
    // ADR 0005: the battle sim never knows items/ranks/trees exist. The run layer
    // composes chassis + weapon + trinkets + spec nodes into ONE resolved UnitDef via
    // this deterministic composer; run-scoped bonuses ride in as spawn statuses.

    public sealed class ChassisDef
    {
        public string Name = "hero";
        public int MaxHp;
        public int MoveInterval = 5;
        public int ManaMax;
        public WeaponDef StarterWeapon = null!;   // weapon-required (round 10)
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

    /// <summary>A spec-tree choice: same primitive bundle as a trinket + optional
    /// signature override (the fork "transform" is content discipline — ADR 0005).</summary>
    public sealed class SpecNode
    {
        public string Name = "node";
        public int HpBonus;
        public List<Trigger> Triggers = new List<Trigger>();
        public List<StatRule> StatRules = new List<StatRule>();
        public List<EffectDef>? SignatureOverride;
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
            bool mastered = false)
        {
            var w = weapon ?? chassis.StarterWeapon;
            int scale = TierScale(tier);
            var def = new UnitDef
            {
                Name = chassis.Name,
                MaxHp = chassis.MaxHp,
                MoveInterval = chassis.MoveInterval,
                ManaMax = chassis.ManaMax,
                Attack = w.Damage * scale / 100,
                AttackInterval = w.Interval,
                Range = w.Range,
                CritChance = w.CritChance,
                CritMultFp = w.CritMultFp,
                HealAutos = w.HealAutos,
                CleavePct = w.CleavePct,
            };
            def.Signature.AddRange(chassis.Signature);
            def.Triggers.AddRange(chassis.Passives);
            def.Triggers.AddRange(w.Triggers);
            def.StatRules.AddRange(chassis.StatRules);
            def.StatRules.AddRange(w.StatRules);

            bool riderLive = mastered || tier == WeaponTier.Relic;
            int riderCopies = (mastered && tier == WeaponTier.Relic) ? 2 : riderLive ? 1 : 0;
            for (int i = 0; i < riderCopies; i++)
            {
                def.Triggers.AddRange(w.MasteryTriggers);
                def.StatRules.AddRange(w.MasteryStatRules);
                def.Range += w.MasteryRangeBonus;
            }

            var result = new ComposedLoadout { Def = def };

            if (trinkets != null)
                foreach (var t in trinkets)
                {
                    def.MaxHp += t.HpBonus;
                    def.ManaMax += t.ManaMaxDelta;
                    def.Triggers.AddRange(t.Triggers);
                    def.StatRules.AddRange(t.StatRules);
                    foreach (var (kind, mag) in t.SpawnStatuses)
                        result.SpawnStatuses.Add(new Status { Kind = kind, Mag = mag, TicksLeft = -1 });
                }

            if (nodes != null)
                foreach (var n in nodes)
                {
                    def.MaxHp += n.HpBonus;
                    def.Triggers.AddRange(n.Triggers);
                    def.StatRules.AddRange(n.StatRules);
                    if (n.SignatureOverride != null)
                    {
                        def.Signature.Clear();
                        def.Signature.AddRange(n.SignatureOverride);
                    }
                }

            if (def.ManaMax < 0) def.ManaMax = 0;
            return result;
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
