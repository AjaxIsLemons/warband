using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// First-playable run and economy shape. These are deliberately explicit initial tuning
    /// values, not balance claims: friends playtest #1 is expected to move them.
    /// </summary>
    public sealed class RunConfig
    {
        public int Acts = 3;
        public int NodesPerAct = 4;              // Fight, Fight, Interlude, Fight, then boss
        public int EventsPerAct = 1;
        public int InterludeNodeIndex = 2;

        public int StartingSand = 4;
        public int StartingFieldSlots = 3;
        public int MaxFieldSlots = 6;
        public int BenchSlots = 2;
        public int[] SlotCosts = { 8, 12, 16 };

        // [act-1][Stable/Fraying/Collapsing]. A loss ends the run and pays nothing, so the
        // old kill-share/pot split is intentionally gone.
        public int[][] FightRewardsByAct =
        {
            new[] { 4, 5, 7 },
            new[] { 5, 6, 8 },
            new[] { 6, 7, 9 },
        };
        public int InterludeTreasurySand = 5;
        public int[] BossSandByAct = { 6, 8, 0 };

        public int HeroSlots = 3;
        public int ItemSlots = 2;
        public int HeroPrice = 5;
        public int WeaponPrice = 4;
        public int TrinketPrice = 3;
        public int InscriptionPrice = 7;
        public int RerollCost = 1;
        public int WeaponChancePct = 45;
        public int TrinketChancePct = 35;
        public int InscriptionChancePct = 20;
        public int SellPct = 50;
        public int[] ReforgeCosts = { 4, 8 };
        public int RewardChoices = 3;

        /// <summary>
        /// ADR 0030: endless reuses Act 3's economy instead of inventing a fourth reward table.
        /// Content difficulty may keep reading the virtual act; only the economy clamps.
        /// </summary>
        public int FightReward(int act, FightTier tier) =>
            FightRewardsByAct[System.Math.Min(System.Math.Max(act, 1), Acts) - 1][(int)tier];
        public int BossReward(int act) =>
            BossSandByAct[System.Math.Min(System.Math.Max(act, 1), Acts) - 1];
        public int SlotCost(int slotsBought) => SlotCosts[slotsBought];
        public int EndlessFightsPerCycle => NodesPerAct - 1;

        // Compatibility shims for the shell/tests landing in parallel with this economy pass.
        // New code uses Sand/Inscription/FightReward terminology.
        public int BannerPrice { get => InscriptionPrice; set => InscriptionPrice = value; }
        public int BannerChancePct
        {
            get => InscriptionChancePct;
            set
            {
                InscriptionChancePct = value;
                int remainder = 100 - value;
                WeaponChancePct = remainder * 56 / 100;
                TrinketChancePct = remainder - WeaponChancePct;
            }
        }
        public int BaseIncome(int act) => FightRewardsByAct[act - 1][(int)FightTier.Fraying];
        public int Pot(int act, FightTier tier) => FightReward(act, tier);
        public int[] TierKillSharePct = { 100, 100, 100 };

        /// <summary>ADR 0015: the forge follows the front — stock and reforge are both
        /// capped by act (never by record). Placeholder curve.</summary>
        public WeaponTier TierCeiling(int act) =>
            act <= 1 ? WeaponTier.Worn : act == 2 ? WeaponTier.Honed : WeaponTier.Relic;
    }
}
