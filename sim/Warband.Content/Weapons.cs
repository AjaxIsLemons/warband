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
        public static readonly Dictionary<string, WeaponDef> All = new Dictionary<string, WeaponDef>
        {
            ["daggers"] = new WeaponDef
            {
                Name = "Twin Daggers", Category = "daggers", Damage = 6, Interval = 6, Range = 1, CritChance = 10,
                MasteryTriggers = { AtStart(Status(StatusKind.CritUp, 15, Self)) },
            },
            ["sabre"] = new WeaponDef
            {
                Name = "Officer's Sabre", Category = "sabre", Damage = 7, Interval = 7, Range = 1, CritChance = 5,
                // The finisher's blade: the first swing after each cast is an automatic crit.
                MasteryTriggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.NextSwingCrit, 0, Self, swings: 1)) },
            },
            ["mace"] = new WeaponDef
            {
                Name = "Temple Mace", Category = "mace", Damage = 9, Interval = 10, Range = 1,
                // Double mana per swing — the cast engine roars.
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, RootEv), Mana(Self, Battle.ManaPerAttack)) },
            },
            ["greataxe"] = new WeaponDef
            {
                Name = "Greataxe", Category = "greataxe", Damage = 14, Interval = 14, Range = 1, CleavePct = 25,
                // Overkill carries to the nearest enemy (Death.Amount = overkill).
                MasteryTriggers = { On(EventKind.Death, W(SrcOwner), Dmg(Nearest(atEvent: true), 0, pctOfEvent: 100)) },
            },
            ["towershield"] = new WeaponDef
            {
                Name = "Tower Shield", Category = "towershield", Damage = 5, Interval = 14, Range = 1,
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack), Shield(Self, 3)) },
            },
            ["pike"] = new WeaponDef
            {
                Name = "Pike", Category = "pike", Damage = 9, Interval = 11, Range = 2,
                // The braced spear: +30% against enemies engaged with your line.
                MasteryTriggers = { On(EventKind.DamageDealt, W(SrcOwner, ByAttack, TgtEngaged, RootEv), Dmg(EvTgt, 0, pctOfEvent: 30)) },
            },
            ["censer"] = new WeaponDef
            {
                Name = "Censer", Category = "censer", Damage = 7, Interval = 10, Range = 3, HealAutos = true,
                MasteryTriggers = { AtStart(Status(StatusKind.OverhealToShield, 0, Self)) },
            },
            ["staff"] = new WeaponDef
            {
                Name = "Ashwood Staff", Category = "staff", Damage = 8, Interval = 10, Range = 3,
                MasteryTriggers = { On(EventKind.Cast, W(SrcOwner), Status(StatusKind.Haste, 300, Self, ticks: 20)) },
            },
            ["bow"] = new WeaponDef
            {
                Name = "Longbow", Category = "bow", Damage = 8, Interval = 10, Range = 4,
                MasteryRangeBonus = 1, // the only range rider in the game — queen of distance
            },
            ["musket"] = new WeaponDef
            {
                Name = "Matchlock Musket", Category = "musket", Damage = 16, Interval = 16, Range = 4,
                // The opening shot: the first swing each fight deals double.
                MasteryTriggers = { AtStart(Status(StatusKind.SwingAmpPct, 100, Self, swings: 1)) },
            },
            ["standard"] = new WeaponDef
            {
                Name = "Company Standard", Category = "standard", Damage = 5, Interval = 9, Range = 1,
                // Company potency: the muster runs deeper (stacks with Standard-Bearer).
                MasteryTriggers = { AtStart(Status(StatusKind.Haste, 100, Allies(1))) },
            },
        };
    }
}
