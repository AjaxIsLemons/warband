using System;
using System.Collections.Generic;

/// <summary>
/// The four-fact scan budget is ordered here, not in card rendering. A future fact becomes one
/// typed row plus a profile change instead of a new card layout.
/// </summary>
internal static class OfferFactProfiles
{
    private static readonly IReadOnlyDictionary<MarketOfferKind, PresentationFactId[]> Profiles =
        new Dictionary<MarketOfferKind, PresentationFactId[]>
        {
            [MarketOfferKind.Recruit] = new[]
            {
                PresentationFactId.Hp,
                PresentationFactId.BasicPower,
                PresentationFactId.Reach,
                PresentationFactId.Cadence,
            },
            [MarketOfferKind.RankUp] = new[]
            {
                PresentationFactId.Rank,
                PresentationFactId.Hp,
                PresentationFactId.BasicPower,
                PresentationFactId.ChoiceCount,
            },
            [MarketOfferKind.Weapon] = new[]
            {
                PresentationFactId.BasicPower,
                PresentationFactId.Reach,
                PresentationFactId.Cadence,
                PresentationFactId.ManaPerSwing,
            },
            [MarketOfferKind.Trinket] = new[]
            {
                PresentationFactId.Hp,
                PresentationFactId.ManaThreshold,
                PresentationFactId.BasicPower,
                PresentationFactId.Duration,
            },
            [MarketOfferKind.Inscription] = new[]
            {
                PresentationFactId.Scope,
                PresentationFactId.Duration,
                PresentationFactId.ChoiceCount,
            },
            [MarketOfferKind.Capacity] = new[]
            {
                PresentationFactId.FieldCapacity,
                PresentationFactId.RankDelta,
            },
        };

    public static List<StatChipModel> Select(MarketOfferKind kind,
                                             IReadOnlyList<StatChipModel> facts)
    {
        var result = new List<StatChipModel>(4);
        if (facts == null) return result;
        if (Profiles.TryGetValue(kind, out PresentationFactId[] profile))
            foreach (PresentationFactId id in profile)
            {
                for (int i = 0; i < facts.Count; i++)
                {
                    StatChipModel fact = facts[i];
                    if (fact.Id != id || result.Contains(fact)) continue;
                    result.Add(fact);
                    break;
                }
                if (result.Count == 4) return result;
            }

        for (int i = 0; i < facts.Count && result.Count < 4; i++)
            if (!result.Contains(facts[i])) result.Add(facts[i]);
        return result;
    }

    public static void Validate()
    {
        foreach (MarketOfferKind kind in Enum.GetValues(typeof(MarketOfferKind)))
        {
            if (kind == MarketOfferKind.Sold) continue;
            if (!Profiles.ContainsKey(kind))
                throw new InvalidOperationException(
                    $"[Offer Facts] {kind} has no four-fact display profile.");
        }
    }
}
