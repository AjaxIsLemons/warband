using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A decision card keeps one visual and mechanical language while changing density for its job.
/// Profiles are presentation-only: selecting a Stock or Target card still has exactly the same
/// run-domain meaning as selecting its full Detail projection.
/// </summary>
internal enum DecisionCardProfile
{
    Feature,
    Stock,
    Detail,
    Target,
}

internal sealed class DecisionFactDefinition
{
    public readonly string Label;
    public readonly string Semantic;
    public readonly UiGlyphId Glyph;
    public readonly string Tooltip;
    public readonly Color Color;
    public readonly MechanicFamily Family;

    public DecisionFactDefinition(string label, string semantic, UiGlyphId glyph,
                                  string tooltip, MechanicFamily family)
    {
        Label = label;
        Semantic = semantic;
        Glyph = glyph;
        Tooltip = tooltip;
        Family = family;
        Color = MechanicPresentation.Definition(family).Color;
    }
}

/// <summary>
/// Source of truth for the meaning, glyph, color, and plain-language help of every typed fact
/// rendered by Muster, stock/detail cards, and the selected-card dossier.
/// </summary>
internal static class DecisionCardPresentation
{
    private static readonly string[] ProfileClasses =
    {
        "feature", "stock", "detail", "target",
    };

    private static readonly string[] SemanticClasses =
    {
        "neutral", "health", "power", "healing", "reach", "cadence", "mana",
        "precision", "area", "rank", "field", "choice", "scope", "duration",
        "protection",
    };

    private static readonly string[] AccentClasses =
    {
        "ward", "mending", "precision", "power", "affliction", "tempo", "reaction",
        "utility",
    };

    private static readonly DecisionFactDefinition Unknown = new DecisionFactDefinition(
        "", "neutral", UiGlyphId.Unknown, "Additional decision information.",
        MechanicFamily.Neutral);
    private static readonly DecisionFactDefinition Healing = Fact(
        "HEAL", "healing", UiGlyphId.Heal, "Healing restored by each basic attack.",
        MechanicFamily.Restoration);

    private static readonly IReadOnlyDictionary<PresentationFactId, DecisionFactDefinition>
        Facts = new Dictionary<PresentationFactId, DecisionFactDefinition>
        {
            [PresentationFactId.Hp] = Fact("HEALTH", "health", UiGlyphId.Health,
                "Maximum combat health.", MechanicFamily.Durability),
            [PresentationFactId.BasicPower] = Fact("POWER", "power", UiGlyphId.Damage,
                "Damage resolved by each basic attack.", MechanicFamily.Offense),
            [PresentationFactId.Restoration] = Healing,
            [PresentationFactId.Reach] = Fact("REACH", "reach", UiGlyphId.Reach,
                "Maximum basic-attack reach in hexes.", MechanicFamily.Space),
            [PresentationFactId.Cadence] = Fact("CADENCE", "cadence", UiGlyphId.Cadence,
                "Time between completed basic attacks.", MechanicFamily.Time),
            [PresentationFactId.ManaThreshold] = Fact("MANA", "mana", UiGlyphId.Mana,
                "Mana required to cast the Signature.", MechanicFamily.Mana),
            [PresentationFactId.ManaPerSwing] = Fact("MANA / HIT", "mana", UiGlyphId.Mana,
                "Mana gained when a basic attack resolves.", MechanicFamily.Mana),
            // SHIELD, not PROTECTION: Protection is the FAMILY name (the colour/glyph bucket).
            // Leaking it into a stat label meant the same number read as PROTECTION on one
            // surface and SHIELD on another.
            [PresentationFactId.Protection] = Fact("SHIELD", "protection",
                UiGlyphId.Shield, "Shield or other damage prevention currently active.",
                MechanicFamily.Protection),
            [PresentationFactId.CritChance] = Fact("CRIT", "precision", UiGlyphId.Sniper,
                "Chance for a basic attack to critically hit.", MechanicFamily.Offense),
            [PresentationFactId.Cleave] = Fact("CLEAVE", "area", UiGlyphId.Area,
                "Basic-attack damage also dealt beside the target.", MechanicFamily.Space),
            [PresentationFactId.Rank] = Fact("RANK", "rank", UiGlyphId.Haste,
                "Current and resulting champion rank.", MechanicFamily.Neutral),
            [PresentationFactId.RankDelta] = Fact("GAIN", "rank", UiGlyphId.Haste,
                "Guaranteed progression gained by this choice.", MechanicFamily.Neutral),
            [PresentationFactId.FieldCapacity] = Fact("FIELD", "field", UiGlyphId.Glyph,
                "Champions that can fight at the same time.", MechanicFamily.Space),
            [PresentationFactId.ChoiceCount] = Fact("CHOICE", "choice", UiGlyphId.Check,
                "A follow-up choice resolved after this decision.", MechanicFamily.Neutral),
            [PresentationFactId.Scope] = Fact("SCOPE", "scope", UiGlyphId.Area,
                "Who or what this rule affects.", MechanicFamily.Space),
            [PresentationFactId.Duration] = Fact("DURATION", "duration", UiGlyphId.Cadence,
                "How long this rule remains active.", MechanicFamily.Time),
        };

    public static DecisionFactDefinition Fact(PresentationFactId id) =>
        Facts.TryGetValue(id, out DecisionFactDefinition definition)
            ? definition
            : Unknown;

    public static DecisionFactDefinition Fact(StatChipModel fact)
    {
        if (fact != null &&
            string.Equals(fact.Label?.Trim(), "HEAL", StringComparison.OrdinalIgnoreCase))
            return Healing;
        return Fact(fact?.Id ?? PresentationFactId.Unknown);
    }

    public static PresentationFactId FactId(string label) =>
        (label ?? "").Trim().ToUpperInvariant() switch
        {
            "HP" or "HEALTH" => PresentationFactId.Hp,
            "HEAL" or "HEALING" or "RESTORE" or "RESTORATION" or "REGEN" =>
                PresentationFactId.Restoration,
            "ATK" or "ATTACK" or "DAMAGE" or "POWER" or "BASIC POWER" =>
                PresentationFactId.BasicPower,
            "SHIELD" or "ARMOR" or "ARMOUR" or "WARD" or "BARRIER" or "PROTECTION" =>
                PresentationFactId.Protection,
            "REACH" or "RANGE" => PresentationFactId.Reach,
            "SPEED" or "CADENCE" => PresentationFactId.Cadence,
            "MANA" or "SIGNATURE MANA" => PresentationFactId.ManaThreshold,
            "MANA/HIT" or "MANA / HIT" or "MANA/SWING" or "MANA / SWING" =>
                PresentationFactId.ManaPerSwing,
            "CRIT" => PresentationFactId.CritChance,
            "CLEAVE" => PresentationFactId.Cleave,
            "RANK" => PresentationFactId.Rank,
            "GAIN" => PresentationFactId.RankDelta,
            "FIELD" or "CAPACITY" => PresentationFactId.FieldCapacity,
            "CHOICE" => PresentationFactId.ChoiceCount,
            "SCOPE" => PresentationFactId.Scope,
            "DURATION" or "TERM" => PresentationFactId.Duration,
            _ => PresentationFactId.Unknown,
        };

    public static void ApplyProfile(VisualElement root, DecisionCardProfile profile)
    {
        if (root == null) return;
        root.AddToClassList("decision-card");
        string selected = profile.ToString().ToLowerInvariant();
        foreach (string value in ProfileClasses)
            root.EnableInClassList("decision-card--" + value, value == selected);
    }

    public static void ApplyAccent(VisualElement root, string accent)
    {
        if (root == null) return;
        foreach (string value in AccentClasses)
            root.EnableInClassList("accent--" + value, value == accent);
    }

    public static void ApplyFact(VisualElement root, PresentationFactId id)
    {
        ApplyFact(root, Fact(id));
    }

    public static void ApplyFact(VisualElement root, StatChipModel fact)
    {
        ApplyFact(root, Fact(fact));
    }

    public static void ApplyFact(VisualElement root, DecisionFactDefinition fact)
    {
        if (root == null) return;
        string selected = (fact ?? Unknown).Semantic;
        root.AddToClassList("decision-fact");
        foreach (string value in SemanticClasses)
            root.EnableInClassList("decision-fact--" + value, value == selected);
        MechanicPresentation.Apply(root, (fact ?? Unknown).Family);
    }

    public static string DisplayLabel(StatChipModel fact)
    {
        if (fact == null) return "";
        DecisionFactDefinition definition = Fact(fact);
        return fact.Id == PresentationFactId.Unknown || string.IsNullOrEmpty(definition.Label)
            ? (fact.Label ?? "").ToUpperInvariant()
            : definition.Label;
    }

    public static string Tooltip(StatChipModel fact)
    {
        if (fact == null) return "";
        string basic = string.IsNullOrWhiteSpace(fact.Tooltip)
            ? Fact(fact).Tooltip
            : fact.Tooltip;
        if (string.IsNullOrWhiteSpace(fact.AdvancedTooltip)) return basic;
        return basic + "\n\nADVANCED · " + fact.AdvancedTooltip;
    }

    public static void Validate()
    {
        MechanicPresentation.Validate();
        foreach (PresentationFactId id in Enum.GetValues(typeof(PresentationFactId)))
        {
            if (id == PresentationFactId.Unknown) continue;
            if (!Facts.ContainsKey(id))
                throw new InvalidOperationException(
                    $"[Decision Cards] {id} has no semantic presentation definition.");
        }
    }

    private static DecisionFactDefinition Fact(string label, string semantic, UiGlyphId glyph,
                                               string tooltip, MechanicFamily family) =>
        new DecisionFactDefinition(label, semantic, glyph, tooltip, family);
}
