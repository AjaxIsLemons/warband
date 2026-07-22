namespace Warband.Run
{
    /// <summary>
    /// Economy shape per ADR 0006/0007. EVERY number here is a placeholder (content
    /// doctrine) — the shapes are the decisions, the values are for the sweep harness
    /// and playtests to move.
    /// </summary>
    public sealed class RunConfig
    {
        public int Acts = 5;
        public int NodesPerAct = 4;              // fight/event nodes before the act boss
        public int EventsPerAct = 1;             // rest are wager fights

        public int StartingFieldSlots = 3;       // ADR 0006: start 3
        public int MaxFieldSlots = 6;            //           cap 6
        public int BenchSlots = 2;               //           bench of 2
        public int[] SlotCosts = { 6, 10, 14 };  // escalating, indexed by slots already bought

        public int[] BaseIncomeByAct = { 3, 4, 5, 6, 7 };    // per node, act-anchored (ADR 0006)
        public int[] PotBaseByAct = { 10, 14, 18, 22, 26 };  // wager pot base (ADR 0007)
        public int[] TierPotPct = { 100, 150, 225 };         // Safe / Even / Greedy pot multiplier
        public int[] TierKillSharePct = { 70, 50, 30 };      // pot % paid per-kill; rest is the win bonus
                                                             // (greed shifts weight to on-win — ADR 0007 §5)

        public int BaseIncome(int act) => BaseIncomeByAct[act - 1];
        public int Pot(int act, FightTier tier) => PotBaseByAct[act - 1] * TierPotPct[(int)tier] / 100;
        public int SlotCost(int slotsBought) => SlotCosts[slotsBought];
    }
}
