using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The seven mechanic families players learn once and then read everywhere. These are
/// presentation semantics only; no simulation rule depends on a family, color, or glyph.
/// Currency is adjacent to the mechanic grammar because it shares the same reusable visual
/// treatment, but it is not a combat mechanic.
/// </summary>
internal enum MechanicFamily
{
    Neutral,
    Durability,
    Offense,
    Restoration,
    Space,
    Time,
    Mana,
    Protection,
    Currency,
}

internal sealed class MechanicDefinition
{
    public readonly MechanicFamily Family;
    public readonly string Semantic;
    public readonly UiGlyphId Glyph;
    public readonly Color Color;
    public readonly Color Surface;

    public MechanicDefinition(MechanicFamily family, string semantic, UiGlyphId glyph,
                              Color color, Color surface)
    {
        Family = family;
        Semantic = semantic;
        Glyph = glyph;
        Color = color;
        Surface = surface;
    }
}

/// <summary>
/// One glossary concept embedded in ordinary rule prose. The rule text remains the visual source
/// of truth; this only associates an authored phrase with the definition already carried by the
/// card model.
/// </summary>
internal sealed class SemanticTextToken
{
    private readonly Regex _pattern;

    public string LinkId { get; }
    public string Label { get; }
    public string Note { get; }
    public MechanicFamily Family { get; }

    public SemanticTextToken(
        string linkId, string label, string note, MechanicFamily family)
    {
        LinkId = linkId ?? "";
        Label = label ?? "";
        Note = note ?? "";
        Family = family;
        _pattern = new Regex(
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(Label)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public Match Match(string text, int startAt) =>
        _pattern.Match(text ?? "", Mathf.Max(0, startAt));

    public RuntimeTooltipModel Tooltip(string context) =>
        RuntimeTooltipModel.Keyword(Note, context);
}

/// <summary>
/// Resolved semantic runs for one Label. Link ids stay local to the label, which keeps rebuilds
/// deterministic and lets the tooltip layer respond to the exact word under the pointer.
/// </summary>
internal sealed class SemanticTextBinding
{
    private readonly Dictionary<string, SemanticTextToken> _byLinkId =
        new Dictionary<string, SemanticTextToken>(StringComparer.Ordinal);

    public string Text { get; }
    public string Context { get; }
    public IReadOnlyList<SemanticTextToken> Tokens { get; }
    public bool HasTokens => Tokens.Count > 0;

    public SemanticTextBinding(
        string text, string context, IReadOnlyList<SemanticTextToken> tokens)
    {
        Text = text ?? "";
        Context = context ?? "";
        Tokens = tokens ?? Array.Empty<SemanticTextToken>();
        foreach (SemanticTextToken token in Tokens)
            if (token != null && !string.IsNullOrWhiteSpace(token.LinkId))
                _byLinkId[token.LinkId] = token;
    }

    public bool TryToken(string linkId, out SemanticTextToken token) =>
        _byLinkId.TryGetValue(linkId ?? "", out token);

    public SemanticTextToken Find(string label)
    {
        foreach (SemanticTextToken token in Tokens)
            if (string.Equals(token.Label, label, StringComparison.OrdinalIgnoreCase))
                return token;
        return null;
    }
}

/// <summary>
/// One source of truth for mechanic icon, color, tinted surface class, and inline-text color.
/// Views ask for meaning; they never choose their own red/blue/purple interpretation.
/// </summary>
internal static class MechanicPresentation
{
    private static readonly MechanicFamily[] Families =
    {
        MechanicFamily.Neutral,
        MechanicFamily.Durability,
        MechanicFamily.Offense,
        MechanicFamily.Restoration,
        MechanicFamily.Space,
        MechanicFamily.Time,
        MechanicFamily.Mana,
        MechanicFamily.Protection,
        MechanicFamily.Currency,
    };

    private static readonly IReadOnlyDictionary<MechanicFamily, MechanicDefinition> Definitions =
        new Dictionary<MechanicFamily, MechanicDefinition>
        {
            [MechanicFamily.Neutral] = Define(MechanicFamily.Neutral, "neutral",
                UiGlyphId.Unknown, 205, 216, 232, 22, 31, 44),
            [MechanicFamily.Durability] = Define(MechanicFamily.Durability, "durability",
                UiGlyphId.Health, 232, 111, 116, 55, 25, 30),
            [MechanicFamily.Offense] = Define(MechanicFamily.Offense, "offense",
                UiGlyphId.Damage, 239, 151, 78, 56, 34, 22),
            [MechanicFamily.Restoration] = Define(MechanicFamily.Restoration, "restoration",
                UiGlyphId.Heal, 101, 211, 154, 20, 49, 39),
            [MechanicFamily.Space] = Define(MechanicFamily.Space, "space",
                UiGlyphId.Reach, 104, 174, 238, 20, 40, 59),
            [MechanicFamily.Time] = Define(MechanicFamily.Time, "time",
                UiGlyphId.Cadence, 181, 139, 235, 39, 29, 58),
            [MechanicFamily.Mana] = Define(MechanicFamily.Mana, "mana",
                UiGlyphId.Mana, 83, 211, 213, 16, 48, 53),
            [MechanicFamily.Protection] = Define(MechanicFamily.Protection, "protection",
                UiGlyphId.Shield, 91, 190, 203, 16, 43, 52),
            [MechanicFamily.Currency] = Define(MechanicFamily.Currency, "currency",
                UiGlyphId.Hourstone, 228, 177, 64, 55, 40, 16),
        };

    private static readonly Regex InlineTerms = new Regex(
        @"\b(health|hp|damage|power|attack|attacks|heal|heals|healing|restore|restores|" +
        @"restoration|regeneration|regen|reach|range|hex|hexes|area|line|distance|" +
        @"cadence|second|seconds|duration|haste|slow|slowed|mana|shield|shields|" +
        @"armor|armour|ward|barrier|protection)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static MechanicDefinition Definition(MechanicFamily family) =>
        Definitions.TryGetValue(family, out MechanicDefinition definition)
            ? definition
            : Definitions[MechanicFamily.Neutral];

    public static MechanicFamily Family(PresentationFactId id) =>
        id switch
        {
            PresentationFactId.Hp => MechanicFamily.Durability,
            PresentationFactId.BasicPower => MechanicFamily.Offense,
            PresentationFactId.Restoration => MechanicFamily.Restoration,
            PresentationFactId.Reach or PresentationFactId.Cleave or
                PresentationFactId.FieldCapacity or PresentationFactId.Scope =>
                MechanicFamily.Space,
            PresentationFactId.Cadence or PresentationFactId.Duration =>
                MechanicFamily.Time,
            PresentationFactId.ManaThreshold or PresentationFactId.ManaPerSwing =>
                MechanicFamily.Mana,
            PresentationFactId.Protection => MechanicFamily.Protection,
            _ => MechanicFamily.Neutral,
        };

    public static MechanicFamily Family(string label)
    {
        string value = (label ?? "").Trim().ToUpperInvariant();
        if (value is "HP" or "HEALTH" or "MAX HP") return MechanicFamily.Durability;
        if (value is "HEAL" or "HEALING" or "RESTORE" or "RESTORATION" or "REGEN")
            return MechanicFamily.Restoration;
        if (value is "SHIELD" or "ARMOR" or "ARMOUR" or "WARD" or "BARRIER" or
            "PROTECTION")
            return MechanicFamily.Protection;
        if (value is "REACH" or "RANGE" or "DISTANCE" or "AREA" or "CLEAVE" or
            "FIELD" or "SCOPE")
            return MechanicFamily.Space;
        if (value is "CADENCE" or "SPEED" or "DURATION" or "TERM" or "HASTE" or "SLOW")
            return MechanicFamily.Time;
        if (value.StartsWith("MANA", StringComparison.Ordinal) || value == "SIGNATURE")
            return MechanicFamily.Mana;
        if (value is "ATK" or "ATTACK" or "DAMAGE" or "DMG" or "POWER" or "CRIT")
            return MechanicFamily.Offense;
        return MechanicFamily.Neutral;
    }

    public static void Apply(VisualElement element, MechanicFamily family)
    {
        if (element == null) return;
        element.AddToClassList("mechanic-surface");
        foreach (MechanicFamily candidate in Families)
            element.EnableInClassList("mechanic--" +
                Definition(candidate).Semantic, candidate == family);
    }

    public static string FormatInline(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        return InlineTerms.Replace(text, match =>
        {
            MechanicDefinition definition = Definition(Family(match.Value));
            string hex = ColorUtility.ToHtmlStringRGB(definition.Color);
            return $"<color=#{hex}><b>{match.Value}</b></color>";
        });
    }

    public static void BindInline(Label label, string text)
    {
        if (label == null) return;
        label.enableRichText = true;
        label.text = FormatInline(text);
    }

    /// <summary>
    /// Binds ordinary wrapped prose while promoting every glossary word actually present in that
    /// prose into a rich-text link. Unused notes do not create detached chips elsewhere.
    /// </summary>
    public static SemanticTextBinding BindSemantic(
        Label label, string text, IReadOnlyList<string> notes, string context = "")
    {
        var tokens = new List<SemanticTextToken>();
        if (!string.IsNullOrWhiteSpace(text) && notes != null)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                string note = notes[i];
                RuntimeTooltipModel tooltip = RuntimeTooltipModel.Keyword(note, context);
                if (string.IsNullOrWhiteSpace(tooltip.Title)) continue;
                var candidate = new SemanticTextToken(
                    $"keyword:{i}", tooltip.Title, note, tooltip.Family);
                if (!candidate.Match(text, 0).Success) continue;
                tokens.Add(candidate);
            }
        }

        var binding = new SemanticTextBinding(text, context, tokens);
        if (label == null) return binding;
        label.enableRichText = true;
        label.text = FormatSemantic(binding);
        label.userData = binding;
        label.EnableInClassList("semantic-text--interactive", binding.HasTokens);
        label.focusable = binding.HasTokens;
        label.tabIndex = binding.HasTokens ? 0 : -1;
        if (binding.HasTokens)
            label.AddToClassList("semantic-text");
        return binding;
    }

    private static string FormatSemantic(SemanticTextBinding binding)
    {
        if (binding == null || !binding.HasTokens)
            return FormatInline(binding?.Text ?? "");

        string text = binding.Text;
        var output = new System.Text.StringBuilder(text.Length + 96);
        int cursor = 0;
        while (cursor < text.Length)
        {
            SemanticTextToken nextToken = null;
            Match nextMatch = null;
            foreach (SemanticTextToken token in binding.Tokens)
            {
                Match match = token.Match(text, cursor);
                if (!match.Success) continue;
                if (nextMatch == null ||
                    match.Index < nextMatch.Index ||
                    match.Index == nextMatch.Index && match.Length > nextMatch.Length)
                {
                    nextToken = token;
                    nextMatch = match;
                }
            }

            if (nextMatch == null)
            {
                output.Append(FormatInline(text.Substring(cursor)));
                break;
            }

            if (nextMatch.Index > cursor)
                output.Append(FormatInline(
                    text.Substring(cursor, nextMatch.Index - cursor)));
            string hex = ColorUtility.ToHtmlStringRGB(
                Definition(nextToken.Family).Color);
            output.Append("<link=\"")
                .Append(nextToken.LinkId)
                .Append("\"><color=#")
                .Append(hex)
                .Append("><b><u>")
                .Append(nextMatch.Value)
                .Append("</u></b></color></link>");
            cursor = nextMatch.Index + nextMatch.Length;
        }
        return output.ToString();
    }

    public static void BindCurrencyButton(Button button, string action, int amount,
                                          bool gain = false, string suffix = "")
    {
        if (button == null) return;
        button.text = "";
        button.Clear();
        var content = new VisualElement();
        content.AddToClassList("currency-action");
        content.pickingMode = PickingMode.Ignore;
        var verb = new Label(action ?? "");
        verb.AddToClassList("currency-action__label");
        verb.pickingMode = PickingMode.Ignore;
        content.Add(verb);
        content.Add(new HourstoneAmount(amount, "currency-action__amount"));
        if (!string.IsNullOrEmpty(suffix))
        {
            var tail = new Label(suffix);
            tail.AddToClassList("currency-action__suffix");
            tail.pickingMode = PickingMode.Ignore;
            content.Add(tail);
        }
        button.Add(content);
        button.tooltip = string.IsNullOrEmpty(suffix)
            ? $"{action}. {(gain ? "Returns" : "Costs")} {amount} Hourstone."
            : $"{action} {amount} Hourstone {suffix}.";
    }

    public static void Validate()
    {
        foreach (MechanicFamily family in Families)
        {
            MechanicDefinition definition = Definition(family);
            if (definition.Family != family || string.IsNullOrWhiteSpace(definition.Semantic))
                throw new InvalidOperationException(
                    $"[Mechanics UI] {family} has no complete presentation token.");
        }
    }

    private static MechanicDefinition Define(MechanicFamily family, string semantic,
                                               UiGlyphId glyph, byte r, byte g, byte b,
                                               byte sr, byte sg, byte sb) =>
        new MechanicDefinition(family, semantic, glyph,
            new Color32(r, g, b, 255), new Color32(sr, sg, sb, 255));
}

/// <summary>A reusable icon + label + value stat tile with semantic surface treatment.</summary>
internal sealed class MechanicStatTile : VisualElement
{
    private readonly string _legacyRootClass;
    private readonly WarbandGlyph _icon;
    private readonly Label _label;
    private readonly Label _value;

    public MechanicStatTile(string legacyRootClass = "", string legacyChildPrefix = "")
    {
        _legacyRootClass = legacyRootClass ?? "";
        AddToClassList("mechanic-stat-tile");
        if (!string.IsNullOrEmpty(_legacyRootClass)) AddToClassList(_legacyRootClass);

        string prefix = string.IsNullOrEmpty(legacyChildPrefix)
            ? _legacyRootClass
            : legacyChildPrefix;
        _icon = new WarbandGlyph();
        _icon.AddToClassList("mechanic-stat-tile__icon");
        _label = new Label();
        _label.AddToClassList("mechanic-stat-tile__label");
        _value = new Label();
        _value.AddToClassList("mechanic-stat-tile__value");
        if (!string.IsNullOrEmpty(prefix))
        {
            _icon.AddToClassList(prefix + "__icon");
            _label.AddToClassList(prefix + "__label");
            _value.AddToClassList(prefix + "__value");
        }
        Add(_icon);
        Add(_label);
        Add(_value);
    }

    public void Bind(StatChipModel model)
    {
        model ??= new StatChipModel();
        DecisionFactDefinition fact = DecisionCardPresentation.Fact(model);
        _icon.Set(fact.Glyph);
        _icon.SetColor(fact.Color);
        _label.text = DecisionCardPresentation.DisplayLabel(model);
        _value.text = model.Value;
        tooltip = DecisionCardPresentation.Tooltip(model);
        DecisionCardPresentation.ApplyFact(this, model);
        foreach (string tone in new[] { "good", "warn", "bad" })
        {
            bool enabled = string.Equals(model.Tone, tone, StringComparison.Ordinal);
            EnableInClassList("mechanic-stat-tile--" + tone, enabled);
            if (!string.IsNullOrEmpty(_legacyRootClass))
                EnableInClassList(_legacyRootClass + "--" + tone, enabled);
        }
    }
}

/// <summary>The one visual spelling of an Hourstone currency amount.</summary>
internal sealed class HourstoneAmount : VisualElement
{
    private readonly Label _value;

    public HourstoneAmount(int amount = 0, string legacyRootClass = "")
    {
        AddToClassList("currency-amount");
        if (!string.IsNullOrEmpty(legacyRootClass)) AddToClassList(legacyRootClass);
        MechanicPresentation.Apply(this, MechanicFamily.Currency);
        var icon = new WarbandGlyph(UiGlyphId.Hourstone);
        icon.SetColor(MechanicPresentation.Definition(MechanicFamily.Currency).Color);
        icon.AddToClassList("currency-amount__icon");
        _value = new Label();
        _value.AddToClassList("currency-amount__value");
        Add(icon);
        Add(_value);
        Bind(amount);
    }

    public void Bind(int amount, bool affordable = true)
    {
        _value.text = amount.ToString();
        EnableInClassList("currency-amount--unaffordable", !affordable);
        tooltip = $"{amount} Hourstone";
    }
}

/// <summary>
/// Selected-offer arithmetic. A disabled action answers "can I buy this?"; this answers why.
/// </summary>
internal sealed class HourstoneBalanceComparison : VisualElement
{
    private readonly HourstoneAmount _balance;
    private readonly HourstoneAmount _cost;
    private readonly HourstoneAmount _after;

    public HourstoneBalanceComparison()
    {
        AddToClassList("currency-balance");
        Add(Label("BALANCE", "currency-balance__label"));
        _balance = new HourstoneAmount(0, "currency-balance__amount");
        Add(_balance);
        Add(Label("−", "currency-balance__operator"));
        _cost = new HourstoneAmount(0, "currency-balance__amount");
        Add(_cost);
        Add(Label("=", "currency-balance__operator"));
        _after = new HourstoneAmount(0, "currency-balance__amount");
        Add(_after);
        Add(Label("AFTER", "currency-balance__after-label"));
    }

    public void Bind(int balance, int cost)
    {
        bool affordable = balance >= cost;
        _balance.Bind(balance);
        _cost.Bind(cost);
        _after.Bind(balance - cost, affordable);
        EnableInClassList("currency-balance--unaffordable", !affordable);
        tooltip = $"Balance {balance}; cost {cost}; after purchase {balance - cost}.";
    }

    private static Label Label(string text, string className)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }
}
