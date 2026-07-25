using System.Collections.Generic;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// One authored PvE setup plus the exact information the preview owes the player.
    /// Calling an encounter factory returns fresh mutable defs; callers may safely tune or
    /// compose a fight without mutating the catalog for later attempts.
    /// </summary>
    public sealed class EncounterDef
    {
        public string Id = "";
        public string Name = "";
        public string Pressure = "";
        public string RuleName = "";
        public string RuleText = "";
        public List<EncounterUnit> Enemies = new List<EncounterUnit>();
    }

    public sealed class EncounterUnit
    {
        public UnitDef Def = null!;
        public Hex Pos;
        public string Role = "";
    }

    /// <summary>
    /// What the Bond actually did in one resolved fight. The point of the encounter is the
    /// question "which threat are you willing to leave enraged" — so the Bond is only real if
    /// it FIRES before the fight ends and the survivor then lives long enough to spend the
    /// speed. A readout where Enrage never fires, or fires with two ticks left, means the
    /// relationship is decoration and the encounter poses no decision.
    /// </summary>
    public readonly struct BondReadout
    {
        public readonly bool EnrageFired;
        public readonly int EnrageTick;          // -1 when it never fired
        public readonly int SurvivorId;          // -1 when it never fired
        public readonly int TicksAfterEnrage;    // fight length remaining once the survivor turned
        public readonly int SurvivorSwingsAfterEnrage; // what the speed was actually converted into
        public readonly int EnemyDeaths;

        public BondReadout(bool fired, int tick, int survivorId, int ticksAfter, int swingsAfter, int enemyDeaths)
        {
            EnrageFired = fired; EnrageTick = tick; SurvivorId = survivorId;
            TicksAfterEnrage = ticksAfter; SurvivorSwingsAfterEnrage = swingsAfter;
            EnemyDeaths = enemyDeaths;
        }
    }

    /// <summary>Small authored proofs. Grow this only after each relationship earns its keep in play.</summary>
    public static class Encounters
    {
        public const int BondHaste = 1000;

        /// <summary>
        /// Read the Bond out of a resolved fight. Keyed on the published BondHaste magnitude so
        /// it can never confuse the Enrage with an incidental Haste from a player's own kit.
        /// </summary>
        public static BondReadout ReadBond(BattleResult result, ICollection<int> enemyIds)
        {
            int enrageTick = -1, survivor = -1;
            foreach (var e in result.Events)
            {
                if (e.Kind != EventKind.StatusApplied) continue;
                if (e.Aux != (int)StatusKind.Haste || e.Amount != BondHaste) continue;
                if (!enemyIds.Contains(e.Target)) continue;
                enrageTick = e.Tick; survivor = e.Target;
                break;
            }

            int enemyDeaths = 0;
            foreach (var e in result.Events)
                if (e.Kind == EventKind.Death && enemyIds.Contains(e.Target))
                    enemyDeaths++;

            if (enrageTick < 0)
                return new BondReadout(false, -1, -1, 0, 0, enemyDeaths);

            int swings = 0;
            foreach (var e in result.Events)
                if (e.Kind == EventKind.Attack && e.Source == survivor && e.Tick >= enrageTick)
                    swings++;

            return new BondReadout(true, enrageTick, survivor,
                result.EndTick - enrageTick, swings, enemyDeaths);
        }

        /// <summary>
        /// First PvE proof from Design/pve-encounters.md: the player sees both enemies,
        /// their formation, and the complete Bond rule before deployment.
        /// </summary>
        public static EncounterDef BondedPair()
        {
            var bulwark = Loadout.Compose(Kits.Chassis["bulwark"]).Def;
            bulwark.Name = "Oathbound Bulwark";
            bulwark.MaxHp = 230;
            bulwark.Attack = 8;
            bulwark.Triggers.Add(BondEnrage());

            var sharpshot = Loadout.Compose(Kits.Chassis["sharpshot"]).Def;
            sharpshot.Name = "Oathbound Sharpshot";
            sharpshot.MaxHp = 135;
            sharpshot.Attack = 12;
            sharpshot.Triggers.Add(BondEnrage());

            return new EncounterDef
            {
                Id = "bonded-pair",
                Name = "The Last Oath",
                Pressure = "Choose which threat you are willing to leave enraged.",
                RuleName = "BOND",
                RuleText = "When either Oathbound dies, the survivor Enrages (+100% Attack Speed).",
                Enemies =
                {
                    new EncounterUnit
                    {
                        Def = bulwark,
                        Pos = Hex.FromRowCol(5, 2),
                        Role = "Frontline control",
                    },
                    new EncounterUnit
                    {
                        Def = sharpshot,
                        Pos = Hex.FromRowCol(6, 4),
                        Role = "Backline damage",
                    },
                },
            };
        }

        private static Trigger BondEnrage() =>
            On(EventKind.Death, W(TgtAlly()),
                Status(StatusKind.Haste, BondHaste, Self));
    }
}
