using System.Collections.Generic;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>The 11-category catalog (ADR 0015). One weapon per category — the
    /// categories ARE the item list. Every rider is an engine amplifier (the rider law);
    /// bow and pike ride physics because reach IS their engine. All numbers placeholder.</summary>
    public static class Weapons
    {
        /// <summary>The mace's mastery is "double mana per swing", so it has to name the same
        /// number the weapon earns — a literal 10 (the old global rate) would silently stop being
        /// a doubling the moment ManaPerSwing was authored. One const, two uses, no drift.</summary>
        private const int MaceMana = 14;

        public static readonly Dictionary<string, WeaponDef> All = new Dictionary<string, WeaponDef>
        {
            // ManaPerSwing is the cadence axis (2026-07-25 systems review §2). Read it against
            // Interval: mana-per-tick now runs 0.83 (daggers) → 1.40 (mace), where it used to be a
            // flat 10/Interval — i.e. purely "fast weapons cast more". Light blades trade cast
            // cadence for swing events; heavy weapons swing rarely and bank most of a cast each time.
            ["daggers"] = new WeaponDef
            {
                Name = "Twin Daggers", Category = "daggers", Damage = 6, Interval = 6, Range = 1, CritChance = 10,
                ManaPerSwing = 5,     // 0.83/tick — the swing-spam blade starves its own signature
                MasteryTriggers = { AtStart(Status(StatusKind.CritUp, 15, Self)) },
            },
            ["sabre"] = new WeaponDef
            {
                Name = "Officer's Sabre", Category = "sabre", Damage = 7, Interval = 7, Range = 1, CritChance = 5,
                ManaPerSwing = 7,     // 1.00/tick
                // The finisher's blade: the first swing after each cast is an automatic crit.
                MasteryTriggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.NextSwingCrit, 0, Self, swings: 1)) },
            },
            ["mace"] = new WeaponDef
            {
                Name = "Temple Mace", Category = "mace", Damage = 9, Interval = 10, Range = 1,
                ManaPerSwing = MaceMana,   // 1.40/tick — 2.80 mastered: THE cast engine
                // Double mana per swing — the cast engine roars.
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, RootEv), Mana(Self, MaceMana)) },
            },
            ["greataxe"] = new WeaponDef
            {
                Name = "Greataxe", Category = "greataxe", Damage = 14, Interval = 14, Range = 1, CleavePct = 25,
                ManaPerSwing = 16,    // 1.14/tick — few swings, each most of a cast
                // Overkill carries to the enemy nearest the CORPSE (Death.Amount = overkill).
                MasteryTriggers = { On(EventKind.Death, W(SrcOwner), Dmg(Nearest(atCorpse: true, exAnchor: true), 0, pctOfEvent: 100)) },
            },
            ["towershield"] = new WeaponDef
            {
                Name = "Tower Shield", Category = "towershield", Damage = 5, Interval = 14, Range = 1,
                ManaPerSwing = 16,    // 1.14/tick — the wall barely hits, but the Slam keeps coming
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack), Shield(Self, 3)) },
            },
            ["pike"] = new WeaponDef
            {
                Name = "Pike", Category = "pike", Damage = 9, Interval = 11, Range = 2,
                ManaPerSwing = 11,    // 1.00/tick
                // The braced spear: +30% against enemies engaged with your line.
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, TgtEngaged, RootEv), Dmg(EvTgt, 0, pctOfEvent: 30)) },
            },
            ["censer"] = new WeaponDef
            {
                Name = "Censer", Category = "censer", Damage = 7, Interval = 10, Range = 3, HealAutos = true,
                ManaPerSwing = 10,    // 1.00/tick
                MasteryTriggers = { AtStart(Status(StatusKind.OverhealToShield, 0, Self)) },
            },
            ["staff"] = new WeaponDef
            {
                Name = "Ashwood Staff", Category = "staff", Damage = 8, Interval = 10, Range = 3,
                ManaPerSwing = 13,    // 1.30/tick — the caster's stick earns its name
                MasteryTriggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Haste, 300, Self, ticks: 20)) },
            },
            ["bow"] = new WeaponDef
            {
                Name = "Longbow", Category = "bow", Damage = 8, Interval = 10, Range = 4,
                ManaPerSwing = 9,     // 0.90/tick — steady fire, cast-light
                MasteryRangeBonus = 1, // the only range rider in the game — queen of distance
            },
            ["musket"] = new WeaponDef
            {
                Name = "Matchlock Musket", Category = "musket", Damage = 16, Interval = 16, Range = 4,
                ManaPerSwing = 20,    // 1.25/tick — artillery: rare shots, real casts
                // The opening shot: the first swing each fight deals double.
                MasteryTriggers = { AtStart(Status(StatusKind.SwingAmpPct, 100, Self, swings: 1)) },
            },
            ["standard"] = new WeaponDef
            {
                Name = "Company Standard", Category = "standard", Damage = 5, Interval = 9, Range = 1,
                ManaPerSwing = 9,     // 1.00/tick
                // Company potency: the muster runs deeper (stacks with Standard-Bearer).
                MasteryTriggers = { AtStart(Status(StatusKind.Haste, 100, Allies(1))) },
            },
        };
    }
}
