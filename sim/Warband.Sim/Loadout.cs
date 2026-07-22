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

    /// <summary>The attack profile lives here (round 10): swap the weapon, change the hero.</summary>
    public sealed class WeaponDef
    {
        public string Name = "weapon";
        public int Damage;
        public int Interval;
        public int Range;
        public int CritChance;
        public int CritMultFp = 1500;
        public List<Trigger> Triggers = new List<Trigger>();      // on-hit riders etc.
        public List<StatRule> StatRules = new List<StatRule>();
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
        /// <summary>Merge order (fixed, documented): chassis → weapon → trinkets → nodes.
        /// Last node with a SignatureOverride wins. Null weapon = the starter.</summary>
        public static ComposedLoadout Compose(
            ChassisDef chassis,
            WeaponDef? weapon = null,
            IEnumerable<TrinketDef>? trinkets = null,
            IEnumerable<SpecNode>? nodes = null)
        {
            var w = weapon ?? chassis.StarterWeapon;
            var def = new UnitDef
            {
                Name = chassis.Name,
                MaxHp = chassis.MaxHp,
                MoveInterval = chassis.MoveInterval,
                ManaMax = chassis.ManaMax,
                Attack = w.Damage,
                AttackInterval = w.Interval,
                Range = w.Range,
                CritChance = w.CritChance,
                CritMultFp = w.CritMultFp,
            };
            def.Signature.AddRange(chassis.Signature);
            def.Triggers.AddRange(chassis.Passives);
            def.Triggers.AddRange(w.Triggers);
            def.StatRules.AddRange(chassis.StatRules);
            def.StatRules.AddRange(w.StatRules);

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
