using System;
using System.Collections.Generic;
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
    private readonly VisualElement _economy;
    private readonly Label _price;
    private readonly HourstoneAmount _currencyPrice;
    private readonly HourstoneBalanceComparison _balance;
    private readonly VisualElement _stats;
    private readonly VisualElement _sections;
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
        _economy = Required<VisualElement>(Root, "economy");
        _price = Required<Label>(Root, "price");
        _currencyPrice = new HourstoneAmount(0, "wb-inspector__currency-price");
        _balance = new HourstoneBalanceComparison();
        _economy.Add(_currencyPrice);
        _economy.Add(_balance);
        _stats = Required<VisualElement>(Root, "stats");
        _sections = Required<VisualElement>(Root, "sections");
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
        _currencyPrice.Bind(model.CurrencyCost,
            model.CurrencyBalance < 0 || model.CurrencyBalance >= model.CurrencyCost);
        if (model.CurrencyCost >= 0)
            _balance.Bind(model.CurrencyBalance, model.CurrencyCost);
        _portraitFallback.text = model.PortraitFallback;
        WarbandCard.SetAccent(Root, model.Accent);

        var texture = string.IsNullOrEmpty(model.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(model.PortraitResource);
        _portrait.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        SetDisplayed(_portraitFallback, texture == null);
        SetDisplayed(_price, model.CurrencyCost < 0 && !string.IsNullOrEmpty(model.Price));
        SetDisplayed(_currencyPrice, model.CurrencyCost >= 0);
        SetDisplayed(_balance, model.CurrencyCost >= 0 && model.CurrencyBalance >= 0);
        SetDisplayed(_economy, model.CurrencyCost >= 0 || !string.IsNullOrEmpty(model.Price));
        BindSections(model);

        _stats.Clear();
        foreach (var stat in model.Stats)
        {
            var chip = new MechanicStatTile(
                "wb-inspector-stat", "wb-inspector-stat");
            chip.Bind(stat);
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
            var label = new Label();
            label.AddToClassList("wb-keyword-note");
            MechanicPresentation.BindInline(label, note);
            _tags.Add(label);
        }
        SetDisplayed(_tags, model.Tags.Count > 0 || model.KeywordNotes.Count > 0);

        _actions.Clear();
        foreach (var action in model.Actions)
        {
            HallActionId id = action.Id;
            var button = new Button(() => _onAction?.Invoke(id));
            button.AddToClassList("btn");
            button.AddToClassList(action.Primary ? "btn--primary" : "btn--ghost");
            if (action.CurrencyCost >= 0)
                MechanicPresentation.BindCurrencyButton(
                    button, action.Label, action.CurrencyCost, action.CurrencyGain);
            else
                button.text = action.Label;
            button.SetEnabled(action.Enabled);
            if (!action.Enabled) button.tooltip = action.DisabledReason;
            _actions.Add(button);
        }
    }

    private void BindSections(InspectorModel model)
    {
        _sections.Clear();
        var sections = model.Sections.Count > 0
            ? model.Sections
            : LegacySections(model);
        foreach (var section in sections)
        {
            var root = new VisualElement();
            root.AddToClassList("wb-inspector__section");
            root.AddToClassList("wb-inspector__section--" +
                                section.Kind.ToString().ToLowerInvariant());
            var label = new Label(section.Label);
            label.AddToClassList("wb-inspector__section-label");
            root.Add(label);

            if (section.Kind == InspectorSectionKind.Rule)
                root.Add(RuleLine(section));
            else if (section.Kind == InspectorSectionKind.Comparison)
            {
                foreach (var comparison in section.Comparisons)
                    root.Add(ComparisonRow(comparison));
            }
            else if (section.Kind == InspectorSectionKind.Choices)
            {
                var choices = new VisualElement();
                choices.AddToClassList("wb-inspector__choice-preview-options");
                foreach (var choice in section.Choices)
                    choices.Add(ChoicePreview(choice));
                root.Add(choices);
            }
            else if (section.Kind == InspectorSectionKind.Capacity)
                root.Add(CapacityDiagram(section));
            _sections.Add(root);
        }
    }

    private static VisualElement RuleLine(InspectorSectionModel section)
    {
        var line = new VisualElement();
        line.AddToClassList("wb-inspector__line");
        var icon = new Label(section.Icon);
        icon.AddToClassList("wb-inspector__line-icon");
        var copy = new VisualElement();
        copy.AddToClassList("wb-inspector__line-body");
        var title = new Label(section.Name);
        title.AddToClassList("wb-inspector__line-title");
        var summary = new Label();
        summary.AddToClassList("wb-inspector__line-copy");
        MechanicPresentation.BindInline(summary, section.Summary);
        copy.Add(title);
        copy.Add(summary);
        line.Add(icon);
        line.Add(copy);
        return line;
    }

    private static VisualElement ChoicePreview(ChoicePreviewModel choice)
    {
        var option = new VisualElement();
        option.AddToClassList("wb-choice-preview");
        var change = new Label(choice.Change);
        change.AddToClassList("wb-choice-preview__change");
        var name = new Label(choice.Name);
        name.AddToClassList("wb-choice-preview__name");
        var rule = new Label();
        rule.AddToClassList("wb-choice-preview__rule");
        MechanicPresentation.BindInline(rule, choice.Rule);
        option.Add(change);
        option.Add(name);
        option.Add(rule);
        foreach (var comparison in choice.Comparisons)
            option.Add(ComparisonRow(comparison));
        return option;
    }

    private static VisualElement CapacityDiagram(InspectorSectionModel section)
    {
        var root = new VisualElement();
        root.AddToClassList("wb-capacity-detail");
        var sockets = new VisualElement();
        sockets.AddToClassList("wb-capacity-detail__sockets");
        for (int i = 0; i < section.CapacityMax; i++)
        {
            var socket = new VisualElement();
            socket.AddToClassList("wb-capacity-detail__socket");
            socket.EnableInClassList("wb-capacity-detail__socket--active",
                i < section.CapacityBefore);
            socket.EnableInClassList("wb-capacity-detail__socket--new",
                i >= section.CapacityBefore && i < section.CapacityAfter);
            var number = new Label((i + 1).ToString());
            socket.Add(number);
            sockets.Add(socket);
        }
        var copy = new VisualElement();
        var title = new Label(section.Name);
        title.AddToClassList("wb-inspector__line-title");
        var summary = new Label();
        summary.AddToClassList("wb-inspector__line-copy");
        MechanicPresentation.BindInline(summary, section.Summary);
        copy.Add(title);
        copy.Add(summary);
        root.Add(sockets);
        root.Add(copy);
        return root;
    }

    private static List<InspectorSectionModel> LegacySections(InspectorModel model)
    {
        var result = new List<InspectorSectionModel>();
        void Add(string label, string icon, string name, string summary)
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(summary)) return;
            result.Add(new InspectorSectionModel
            {
                Label = label,
                Icon = icon,
                Name = name,
                Summary = summary,
            });
        }
        Add("BASIC ATTACK", model.WeaponIcon, model.WeaponName, model.WeaponSummary);
        Add(model.AbilityTrigger, model.AbilityIcon, model.AbilityName, model.AbilitySummary);
        Add(model.PassiveTrigger, model.PassiveIcon, model.PassiveName, model.PassiveSummary);
        if (model.Comparisons.Count > 0)
            result.Add(new InspectorSectionModel
            {
                Kind = InspectorSectionKind.Comparison,
                Label = model.ComparisonTitle,
                Comparisons = new List<StatComparisonModel>(model.Comparisons),
            });
        if (model.ChoicePreviews.Count > 0)
            result.Add(new InspectorSectionModel
            {
                Kind = InspectorSectionKind.Choices,
                Label = "SPECIALIZATION PREVIEW",
                Choices = new List<ChoicePreviewModel>(model.ChoicePreviews),
            });
        return result;
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
        PresentationFactId id = DecisionCardPresentation.FactId(comparison.Label);
        DecisionCardPresentation.ApplyFact(row, id);
        DecisionFactDefinition definition = DecisionCardPresentation.Fact(id);
        row.EnableInClassList("wb-comparison--good", comparison.Tone == "good");
        row.EnableInClassList("wb-comparison--bad", comparison.Tone == "bad");
        var icon = new WarbandGlyph(definition.Glyph);
        icon.SetColor(definition.Color);
        icon.AddToClassList("wb-comparison__icon");
        var label = new Label(definition.Label.Length > 0
            ? definition.Label
            : comparison.Label);
        label.AddToClassList("wb-comparison__label");
        var before = new Label(comparison.Before);
        before.AddToClassList("wb-comparison__before");
        var arrow = new Label("→");
        arrow.AddToClassList("wb-comparison__arrow");
        var after = new Label(comparison.After);
        after.AddToClassList("wb-comparison__after");
        row.Add(icon);
        row.Add(label);
        row.Add(before);
        row.Add(arrow);
        row.Add(after);
        return row;
    }
}
