using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable, pooled card renderer. A card is selection-only: buying, selling, equipping, and
/// committing always happen through explicit inspector/workspace actions.
/// </summary>
internal sealed class WarbandCard
{
    private static VisualTreeAsset s_template;

    private readonly Action<string> _onSelected;
    private readonly VisualElement _portrait;
    private readonly Label _portraitFallback;
    private readonly Label _roleIcon;
    private readonly Label _rank;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly VisualElement _stats;
    private readonly VisualElement _ability;
    private readonly Label _abilityIcon;
    private readonly Label _abilityTrigger;
    private readonly Label _abilityName;
    private readonly Label _abilitySummary;
    private readonly VisualElement _passive;
    private readonly Label _passiveIcon;
    private readonly Label _passiveTrigger;
    private readonly Label _passiveName;
    private readonly Label _passiveSummary;
    private readonly VisualElement _tags;
    private readonly Label _weapon;
    private readonly Label _frozen;
    private readonly Label _price;
    private string _key = "";
    private bool _disabled;
    private CardModel _model = new CardModel();
    private readonly Action<CardModel, VisualElement> _onHover;
    private readonly Action _onLeave;

    public VisualElement Root { get; }

    public WarbandCard(Action<string> onSelected,
                       Action<CardModel, VisualElement> onHover = null,
                       Action onLeave = null)
    {
        _onSelected = onSelected;
        _onHover = onHover;
        _onLeave = onLeave;
        if (s_template == null)
            s_template = Resources.Load<VisualTreeAsset>("UI/WarbandCard");
        if (s_template == null)
            throw new InvalidOperationException("[UI] Resources/UI/WarbandCard.uxml is required.");

        var host = new VisualElement();
        s_template.CloneTree(host);
        Root = Required<VisualElement>(host, "card");
        Root.RemoveFromHierarchy();
        DecisionCardPresentation.ApplyProfile(Root, DecisionCardProfile.Target);

        _portrait = Required<VisualElement>(Root, "portrait");
        _portraitFallback = Required<Label>(Root, "portrait-fallback");
        _roleIcon = Required<Label>(Root, "role-icon");
        _rank = Required<Label>(Root, "rank");
        _eyebrow = Required<Label>(Root, "eyebrow");
        _title = Required<Label>(Root, "title");
        _subtitle = Required<Label>(Root, "subtitle");
        _stats = Required<VisualElement>(Root, "stats");
        _ability = Required<VisualElement>(Root, "ability");
        _abilityIcon = Required<Label>(Root, "ability-icon");
        _abilityTrigger = Required<Label>(Root, "ability-trigger");
        _abilityName = Required<Label>(Root, "ability-name");
        _abilitySummary = Required<Label>(Root, "ability-summary");
        _passive = Required<VisualElement>(Root, "passive");
        _passiveIcon = Required<Label>(Root, "passive-icon");
        _passiveTrigger = Required<Label>(Root, "passive-trigger");
        _passiveName = Required<Label>(Root, "passive-name");
        _passiveSummary = Required<Label>(Root, "passive-summary");
        _tags = Required<VisualElement>(Root, "tags");
        _weapon = Required<Label>(Root, "weapon");
        _frozen = Required<Label>(Root, "frozen");
        _price = Required<Label>(Root, "price");

        Root.RegisterCallback<ClickEvent>(OnClick);
        Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        Root.RegisterCallback<PointerEnterEvent>(_ => _onHover?.Invoke(_model, Root));
        Root.RegisterCallback<PointerLeaveEvent>(_ => _onLeave?.Invoke());
        Root.RegisterCallback<FocusInEvent>(_ => _onHover?.Invoke(_model, Root));
        Root.RegisterCallback<FocusOutEvent>(_ => _onLeave?.Invoke());
    }

    public void Bind(CardModel model)
    {
        _model = model;
        _key = model.Key;
        _disabled = model.Disabled || model.Sold;
        Root.userData = model.Key;
        Root.tooltip = model.Sold ? "Already purchased" :
            model.Disabled ? "This card is not currently available" :
            "Select to inspect";

        _eyebrow.text = model.Eyebrow;
        _title.text = model.Sold ? "SOLD" : model.Title;
        _subtitle.text = model.Subtitle;
        _portraitFallback.text = model.PortraitFallback;
        _roleIcon.text = model.RoleIcon;
        _rank.text = model.Rank;
        _abilityIcon.text = model.AbilityIcon;
        _abilityTrigger.text = model.AbilityTrigger;
        _abilityName.text = model.AbilityName;
        _abilitySummary.text = model.AbilitySummary;
        _ability.tooltip = string.IsNullOrEmpty(model.AbilitySummary)
            ? ""
            : $"{model.AbilityTrigger}: {model.AbilitySummary}";
        _passiveIcon.text = model.PassiveIcon;
        _passiveTrigger.text = model.PassiveTrigger;
        _passiveName.text = model.PassiveName;
        _passiveSummary.text = model.PassiveSummary;
        _passive.tooltip = string.IsNullOrEmpty(model.PassiveSummary)
            ? ""
            : $"{model.PassiveTrigger}: {model.PassiveSummary}";
        _weapon.text = model.Weapon;
        _price.text = model.Price;

        Root.EnableInClassList("wb-card--selected", model.Selected);
        Root.EnableInClassList("wb-card--pinned", model.Pinned);
        Root.EnableInClassList("wb-card--disabled", model.Disabled);
        Root.EnableInClassList("wb-card--sold", model.Sold);
        Root.EnableInClassList("wb-card--frozen", model.Frozen);
        SetAccent(Root, model.Accent);

        var texture = string.IsNullOrEmpty(model.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(model.PortraitResource);
        _portrait.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        SetDisplayed(_portraitFallback, texture == null);
        SetDisplayed(_rank, !string.IsNullOrEmpty(model.Rank));
        SetDisplayed(_subtitle, !string.IsNullOrEmpty(model.Subtitle));
        SetDisplayed(_ability, !string.IsNullOrEmpty(model.AbilityName));
        SetDisplayed(_passive, !string.IsNullOrEmpty(model.PassiveName));
        SetDisplayed(_weapon, !string.IsNullOrEmpty(model.Weapon));
        SetDisplayed(_frozen, model.Frozen);
        SetDisplayed(_price, !string.IsNullOrEmpty(model.Price));

        SyncStats(model.Stats);
        SyncTags(model.Tags);
    }

    /// <summary>
    /// Hall cards are physical selectors, not miniature dossiers. Keep one exact rule on the
    /// object and move the complete kit into the selection tray / optional dossier.
    /// Called after Bind so it intentionally owns the final inline visibility state.
    /// </summary>
    public void SetHallVariant(string variant)
    {
        // Every Hall object is a compact Stock or Target selector now. Inline ability text is
        // intentionally owned by the selected Detail stage; forcing it visible here overrides
        // the profile stylesheet and can push copy into the protected footer.
        SetDisplayed(_subtitle, false);
        SetDisplayed(_ability, false);
        SetDisplayed(_passive, false);
        SetDisplayed(_tags, false);
        DecisionCardPresentation.ApplyProfile(Root,
            variant == "armory" || variant == "hourstone" || variant == "market"
                ? DecisionCardProfile.Stock
                : DecisionCardProfile.Target);
    }

    private void OnClick(ClickEvent evt)
    {
        if (_disabled || string.IsNullOrEmpty(_key)) return;
        _onSelected?.Invoke(_key);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
        OnClick(null);
        evt.StopPropagation();
    }

    private void SyncStats(List<StatChipModel> models)
    {
        while (_stats.childCount > models.Count) _stats.RemoveAt(_stats.childCount - 1);
        while (_stats.childCount < models.Count)
        {
            var chip = new VisualElement();
            chip.AddToClassList("wb-mini-stat");
            var icon = new WarbandGlyph { name = "icon" };
            icon.AddToClassList("wb-mini-stat__icon");
            var label = new Label { name = "label" };
            label.AddToClassList("wb-mini-stat__label");
            var value = new Label { name = "value" };
            value.AddToClassList("wb-mini-stat__value");
            chip.Add(icon);
            chip.Add(label);
            chip.Add(value);
            _stats.Add(chip);
        }
        for (int i = 0; i < models.Count; i++)
        {
            var chip = _stats.ElementAt(i);
            StatChipModel model = models[i];
            DecisionFactDefinition definition = DecisionCardPresentation.Fact(model);
            WarbandGlyph icon =
                chip.Q<WarbandGlyph>(className: "wb-mini-stat__icon");
            icon.Set(definition.Glyph);
            icon.SetColor(definition.Color);
            chip.Q<Label>("label").text = DecisionCardPresentation.DisplayLabel(model);
            chip.Q<Label>("value").text = model.Value;
            chip.tooltip = DecisionCardPresentation.Tooltip(model);
            DecisionCardPresentation.ApplyFact(chip, model);
            chip.EnableInClassList("wb-mini-stat--good", models[i].Tone == "good");
            chip.EnableInClassList("wb-mini-stat--warn", models[i].Tone == "warn");
            chip.EnableInClassList("wb-mini-stat--bad", models[i].Tone == "bad");
        }
    }

    private static string StatSemantic(string label)
    {
        switch ((label ?? "").ToUpperInvariant())
        {
            case "HP": return "hp";
            case "ATK": return "attack";
            case "HEAL": return "heal";
            case "REACH": return "reach";
            case "SPEED": return "speed";
            case "CRIT": return "crit";
            default: return "";
        }
    }

    private static string StatDisplayLabel(string label)
    {
        switch ((label ?? "").ToUpperInvariant())
        {
            case "HP": return "♥  HP";
            case "ATK": return "⚔  DAMAGE";
            case "HEAL": return "✚  HEAL";
            case "REACH": return "⌖  REACH";
            case "SPEED": return "◷  CADENCE";
            case "CRIT": return "✦  CRIT";
            default: return label;
        }
    }

    private void SyncTags(List<string> models)
    {
        while (_tags.childCount > models.Count) _tags.RemoveAt(_tags.childCount - 1);
        while (_tags.childCount < models.Count)
        {
            var label = new Label();
            label.AddToClassList("wb-tag");
            _tags.Add(label);
        }
        for (int i = 0; i < models.Count; i++)
            ((Label)_tags.ElementAt(i)).text = models[i];
        SetDisplayed(_tags, models.Count > 0);
    }

    internal static void SetAccent(VisualElement element, string accent)
    {
        foreach (var value in new[]
                 { "ward", "mending", "precision", "power", "affliction", "tempo", "reaction", "utility" })
            element.EnableInClassList("accent--" + value, value == accent);
    }

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null) throw new InvalidOperationException($"[UI] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool value) =>
        element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
}
