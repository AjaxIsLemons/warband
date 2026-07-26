using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Reusable progressive-disclosure panel for whichever card currently owns focus.</summary>
internal sealed class InspectorPanel
{
    private static VisualTreeAsset s_template;

    private readonly Action<HallActionId> _onAction;
    private readonly Label _empty;
    private readonly VisualElement _content;
    private readonly VisualElement _portrait;
    private readonly Label _portraitFallback;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _price;
    private readonly VisualElement _stats;
    private readonly VisualElement _comparison;
    private readonly Label _comparisonTitle;
    private readonly VisualElement _comparisonRows;
    private readonly VisualElement _choicePreview;
    private readonly VisualElement _choicePreviewOptions;
    private readonly Label _weaponIcon;
    private readonly Label _weaponName;
    private readonly Label _weaponSummary;
    private readonly Label _abilityIcon;
    private readonly Label _abilityTrigger;
    private readonly Label _abilityName;
    private readonly Label _abilitySummary;
    private readonly Label _passiveIcon;
    private readonly Label _passiveTrigger;
    private readonly Label _passiveName;
    private readonly Label _passiveSummary;
    private readonly VisualElement _tags;
    private readonly VisualElement _actions;

    public VisualElement Root { get; }
    public VisualElement ActionsRoot => _actions;

    public InspectorPanel(Action<HallActionId> onAction)
    {
        _onAction = onAction;
        if (s_template == null)
            s_template = Resources.Load<VisualTreeAsset>("UI/InspectorPanel");
        if (s_template == null)
            throw new InvalidOperationException("[UI] Resources/UI/InspectorPanel.uxml is required.");

        var host = new VisualElement();
        s_template.CloneTree(host);
        Root = Required<VisualElement>(host, "inspector");
        Root.RemoveFromHierarchy();
        DecisionCardPresentation.ApplyProfile(Root, DecisionCardProfile.Detail);

        _empty = Required<Label>(Root, "empty");
        _content = Required<VisualElement>(Root, "content");
        _portrait = Required<VisualElement>(Root, "portrait");
        _portraitFallback = Required<Label>(Root, "portrait-fallback");
        _eyebrow = Required<Label>(Root, "eyebrow");
        _title = Required<Label>(Root, "title");
        _subtitle = Required<Label>(Root, "subtitle");
        _price = Required<Label>(Root, "price");
        _stats = Required<VisualElement>(Root, "stats");
        _comparison = Required<VisualElement>(Root, "comparison");
        _comparisonTitle = Required<Label>(Root, "comparison-title");
        _comparisonRows = Required<VisualElement>(Root, "comparison-rows");
        _choicePreview = Required<VisualElement>(Root, "choice-preview");
        _choicePreviewOptions = Required<VisualElement>(Root, "choice-preview-options");
        _weaponIcon = Required<Label>(Root, "weapon-icon");
        _weaponName = Required<Label>(Root, "weapon-name");
        _weaponSummary = Required<Label>(Root, "weapon-summary");
        _abilityIcon = Required<Label>(Root, "ability-icon");
        _abilityTrigger = Required<Label>(Root, "ability-trigger");
        _abilityName = Required<Label>(Root, "ability-name");
        _abilitySummary = Required<Label>(Root, "ability-summary");
        _passiveIcon = Required<Label>(Root, "passive-icon");
        _passiveTrigger = Required<Label>(Root, "passive-trigger");
        _passiveName = Required<Label>(Root, "passive-name");
        _passiveSummary = Required<Label>(Root, "passive-summary");
        _tags = Required<VisualElement>(Root, "tags");
        _actions = Required<VisualElement>(Root, "actions");
    }

    public void Bind(InspectorModel model)
    {
        SetDisplayed(_empty, model.Empty);
        SetDisplayed(_content, !model.Empty);
        SetDisplayed(_actions, !model.Empty);
        if (model.Empty)
        {
            // The Hall can present actions in a pinned dock outside this panel's content tree.
            // Clear stale commits before returning so an empty dossier can never retain BUY/EQUIP.
            _actions.Clear();
            return;
        }

        _eyebrow.text = model.Eyebrow;
        _title.text = model.Title;
        _subtitle.text = model.Subtitle;
        _price.text = model.Price;
        _portraitFallback.text = model.PortraitFallback;
        _weaponIcon.text = model.WeaponIcon;
        _weaponName.text = model.WeaponName;
        _weaponSummary.text = model.WeaponSummary;
        _abilityIcon.text = model.AbilityIcon;
        _abilityTrigger.text = model.AbilityTrigger;
        _abilityName.text = model.AbilityName;
        _abilitySummary.text = model.AbilitySummary;
        _passiveIcon.text = model.PassiveIcon;
        _passiveTrigger.text = model.PassiveTrigger;
        _passiveName.text = model.PassiveName;
        _passiveSummary.text = model.PassiveSummary;
        WarbandCard.SetAccent(Root, model.Accent);

        var texture = string.IsNullOrEmpty(model.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(model.PortraitResource);
        _portrait.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        SetDisplayed(_portraitFallback, texture == null);
        SetDisplayed(_price, !string.IsNullOrEmpty(model.Price));
        _comparisonTitle.text = model.ComparisonTitle;
        _comparisonRows.Clear();
        foreach (var comparison in model.Comparisons)
            _comparisonRows.Add(ComparisonRow(comparison));
        SetDisplayed(_comparison, model.Comparisons.Count > 0);

        _choicePreviewOptions.Clear();
        foreach (var choice in model.ChoicePreviews)
        {
            var option = new VisualElement();
            option.AddToClassList("wb-choice-preview");
            var change = new Label(choice.Change);
            change.AddToClassList("wb-choice-preview__change");
            var name = new Label(choice.Name);
            name.AddToClassList("wb-choice-preview__name");
            var rule = new Label(choice.Rule);
            rule.AddToClassList("wb-choice-preview__rule");
            option.Add(change);
            option.Add(name);
            option.Add(rule);
            foreach (var comparison in choice.Comparisons)
                option.Add(ComparisonRow(comparison));
            _choicePreviewOptions.Add(option);
        }
        SetDisplayed(_choicePreview, model.ChoicePreviews.Count > 0);

        _stats.Clear();
        foreach (var stat in model.Stats)
        {
            var chip = new VisualElement();
            chip.AddToClassList("wb-inspector-stat");
            DecisionFactDefinition definition = DecisionCardPresentation.Fact(stat);
            DecisionCardPresentation.ApplyFact(chip, stat);
            chip.tooltip = DecisionCardPresentation.Tooltip(stat);
            var icon = new WarbandGlyph(definition.Glyph);
            icon.SetColor(definition.Color);
            icon.AddToClassList("wb-inspector-stat__icon");
            var label = new Label(DecisionCardPresentation.DisplayLabel(stat));
            label.AddToClassList("wb-inspector-stat__label");
            var value = new Label(stat.Value);
            value.AddToClassList("wb-inspector-stat__value");
            chip.EnableInClassList("wb-inspector-stat--good", stat.Tone == "good");
            chip.EnableInClassList("wb-inspector-stat--warn", stat.Tone == "warn");
            chip.EnableInClassList("wb-inspector-stat--bad", stat.Tone == "bad");
            chip.Add(icon);
            chip.Add(label);
            chip.Add(value);
            _stats.Add(chip);
        }

        _tags.Clear();
        foreach (var tag in model.Tags)
        {
            var label = new Label(tag);
            label.AddToClassList("wb-tag");
            _tags.Add(label);
        }
        foreach (var note in model.KeywordNotes)
        {
            var label = new Label(note);
            label.AddToClassList("wb-keyword-note");
            _tags.Add(label);
        }
        SetDisplayed(_tags, model.Tags.Count > 0 || model.KeywordNotes.Count > 0);

        _actions.Clear();
        foreach (var action in model.Actions)
        {
            HallActionId id = action.Id;
            var button = new Button(() => _onAction?.Invoke(id)) { text = action.Label };
            button.AddToClassList("btn");
            button.AddToClassList(action.Primary ? "btn--primary" : "btn--ghost");
            button.SetEnabled(action.Enabled);
            button.tooltip = action.Enabled ? "" : action.DisabledReason;
            _actions.Add(button);
        }
    }

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null) throw new InvalidOperationException($"[UI] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool value) =>
        element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

    private static VisualElement ComparisonRow(StatComparisonModel comparison)
    {
        var row = new VisualElement();
        row.AddToClassList("wb-comparison");
        DecisionCardPresentation.ApplyFact(row,
            DecisionCardPresentation.FactId(comparison.Label));
        row.EnableInClassList("wb-comparison--good", comparison.Tone == "good");
        row.EnableInClassList("wb-comparison--bad", comparison.Tone == "bad");
        var label = new Label(comparison.Label);
        label.AddToClassList("wb-comparison__label");
        var before = new Label(comparison.Before);
        before.AddToClassList("wb-comparison__before");
        var arrow = new Label("→");
        arrow.AddToClassList("wb-comparison__arrow");
        var after = new Label(comparison.After);
        after.AddToClassList("wb-comparison__after");
        row.Add(label);
        row.Add(before);
        row.Add(arrow);
        row.Add(after);
        return row;
    }
}
