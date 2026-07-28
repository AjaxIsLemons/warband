using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Reusable progressive-disclosure panel for whichever card currently owns focus.</summary>
internal sealed class InspectorPanel
{
    private static readonly DecisionDetailKind[] DetailKinds =
    {
        DecisionDetailKind.Champion,
        DecisionDetailKind.Recruit,
        DecisionDetailKind.RankUp,
        DecisionDetailKind.Weapon,
        DecisionDetailKind.Trinket,
        DecisionDetailKind.Inscription,
        DecisionDetailKind.Capacity,
        DecisionDetailKind.Combatant,
    };

    private static VisualTreeAsset s_template;

    private readonly Action<HallActionId> _onAction;
    private readonly Action<string> _onRecipient;
    private readonly RuntimeTooltipService _tooltips;
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
    private readonly VisualElement _stats;
    private readonly VisualElement _rankUpBody;
    private readonly VisualElement _rankUpGains;
    private readonly VisualElement _rankUpLadder;
    private readonly VisualElement _rankUpOptions;
    private readonly VisualElement _decisionBody;
    private readonly VisualElement _sections;
    private readonly VisualElement _deferred;
    private readonly VisualElement _equipmentPreview;
    private readonly VisualElement _recipients;
    private readonly Label _comparisonTitle;
    private readonly VisualElement _comparisonTable;
    private readonly VisualElement _ruleDeltas;
    private readonly VisualElement _tags;
    private readonly VisualElement _actions;

    public VisualElement Root { get; }
    public VisualElement ActionsRoot => _actions;

    public InspectorPanel(Action<HallActionId> onAction, Action<string> onRecipient = null,
                          RuntimeTooltipService tooltips = null)
    {
        _onAction = onAction;
        _onRecipient = onRecipient;
        _tooltips = tooltips;
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
        _economy.Add(_currencyPrice);
        _stats = Required<VisualElement>(Root, "stats");
        _rankUpBody = Required<VisualElement>(Root, "rank-up-body");
        _rankUpGains = Required<VisualElement>(Root, "rank-up-gains");
        _rankUpLadder = Required<VisualElement>(Root, "rank-up-ladder");
        _rankUpOptions = Required<VisualElement>(Root, "rank-up-options");
        _decisionBody = Required<VisualElement>(Root, "decision-body");
        _sections = Required<VisualElement>(Root, "sections");
        _deferred = Required<VisualElement>(Root, "deferred");
        _equipmentPreview = Required<VisualElement>(Root, "equipment-preview");
        _recipients = Required<VisualElement>(Root, "recipients");
        _comparisonTitle = Required<Label>(Root, "comparison-title");
        _comparisonTable = Required<VisualElement>(Root, "comparison-table");
        _ruleDeltas = Required<VisualElement>(Root, "rule-deltas");
        _tags = Required<VisualElement>(Root, "tags");
        _actions = Required<VisualElement>(Root, "actions");
    }

    public void Bind(InspectorModel model)
    {
        SetDisplayed(_empty, model.Empty);
        SetDisplayed(_content, !model.Empty);
        SetDisplayed(_actions, !model.Empty);
        foreach (DecisionDetailKind kind in DetailKinds)
            Root.EnableInClassList("wb-inspector--" +
                kind.ToString().ToLowerInvariant(), !model.Empty && model.Kind == kind);
        Root.EnableInClassList(
            "wb-inspector--equipment-preview",
            !model.Empty && model.EquipmentPreview != null);
        if (model.Empty)
        {
            // The Hall can present actions in a pinned dock outside this panel's content tree.
            // Clear stale commits before returning so an empty dossier can never retain BUY/EQUIP.
            _actions.Clear();
            SetDisplayed(_rankUpBody, false);
            return;
        }

        _eyebrow.text = model.Eyebrow;
        _title.text = model.Title;
        _subtitle.text = model.Subtitle;
        _price.text = model.Price;
        _currencyPrice.Bind(model.CurrencyCost,
            model.CurrencyBalance < 0 || model.CurrencyBalance >= model.CurrencyCost);
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
        SetDisplayed(_economy, model.CurrencyCost >= 0 || !string.IsNullOrEmpty(model.Price));
        BindSections(model);
        BindEquipmentPreview(model.EquipmentPreview);
        BindRankUp(model);

        bool typedRankUp = model.RankUpDetail != null;
        _stats.Clear();
        // Rank-up shows its gains as before → after rows inside the rank-up body; repeating
        // them as +N chips here would encode one fact in two channels (workbench-dossier.md).
        if (!typedRankUp)
            foreach (var stat in model.Stats)
            {
                var chip = new MechanicStatTile(
                    "wb-inspector-stat", "wb-inspector-stat");
                chip.Bind(stat);
                _tooltips?.Attach(chip, () => StatTooltip(stat, model.Title));
                _stats.Add(chip);
            }
        SetDisplayed(_stats, _stats.childCount > 0);

        _tags.Clear();
        foreach (var tag in model.Tags)
        {
            var label = new Label(tag);
            label.AddToClassList("wb-tag");
            _tags.Add(label);
        }
        SetDisplayed(_decisionBody, !typedRankUp);
        SetDisplayed(_tags, !typedRankUp && _tags.childCount > 0 &&
            model.EquipmentPreview == null);
        SetDisplayed(_rankUpBody, typedRankUp);

        _actions.Clear();
        foreach (var action in model.Actions)
        {
            HallActionId id = action.Id;
            var button = new Button(() => _onAction?.Invoke(id));
            button.AddToClassList("btn");
            button.AddToClassList(action.Primary ? "btn--primary" : "btn--ghost");
            if (action.CurrencyCost >= 0)
                MechanicPresentation.BindCurrencyButton(
                    button, action.Label, action.CurrencyCost, action.CurrencyGain,
                    action.CurrencySuffix);
            else
                button.text = action.Label;
            button.SetEnabled(action.Enabled);
            if (!action.Enabled)
            {
                if (_tooltips == null)
                    button.tooltip = action.DisabledReason;
                else
                    _tooltips.Attach(button, () => new RuntimeTooltipModel
                    {
                        Kind = RuntimeTooltipKind.DisabledReason,
                        Family = MechanicFamily.Neutral,
                        Eyebrow = "ACTION UNAVAILABLE",
                        Title = action.Label,
                        Domain = "REQUIREMENT",
                        Body = action.DisabledReason,
                    });
            }
            _actions.Add(button);
        }
    }

    private static RuntimeTooltipModel StatTooltip(StatChipModel stat, string context)
    {
        MechanicFamily family = MechanicPresentation.Family(stat?.Id ??
            PresentationFactId.Unknown);
        return new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = family,
            Eyebrow = "UNIT FACT",
            Title = DecisionCardPresentation.DisplayLabel(stat),
            Domain = MechanicPresentation.Definition(family).Semantic.ToUpperInvariant(),
            Body = DecisionCardPresentation.Tooltip(stat),
            Context = string.IsNullOrWhiteSpace(context)
                ? ""
                : "SHOWN ON  " + context.ToUpperInvariant(),
        };
    }

    private void BindSections(InspectorModel model)
    {
        _sections.Clear();
        _deferred.Clear();
        var sections = model.Sections.Count > 0
            ? model.Sections
            : LegacySections(model);
        // During an equip projection the preview owns the whole detail column — the unit's
        // rules stay one click away on its own dossier, so even deferred rows yield.
        bool equipProjection = model.EquipmentPreview != null;
        foreach (InspectorSectionModel section in sections)
        {
            // Role, not geometry: Deferred renders as one compact line whose full rule lives
            // on hover; Primary renders in full. Width never decides what exists
            // (Design/workbench-dossier.md).
            if (section.Role == InspectorSectionRole.Deferred)
            {
                if (!equipProjection)
                    _deferred.Add(DeferredRow(section, model.Title));
                continue;
            }
            var root = new VisualElement();
            root.AddToClassList("wb-inspector__section");
            root.AddToClassList("wb-inspector__section--" +
                                section.Kind.ToString().ToLowerInvariant());
            root.Add(SectionHeading(section));

            if (section.Kind == InspectorSectionKind.Rule)
                root.Add(RuleLine(
                    section, model.Traits, model.KeywordNotes, model.Title));
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
        SetDisplayed(_deferred, _deferred.childCount > 0);
        // An empty rule column yields its width to the equip preview instead of holding a
        // blank flex share.
        SetDisplayed(_sections.parent,
            _sections.childCount > 0 || _deferred.childCount > 0);
    }

    /// <summary>
    /// One compact line for a Deferred section: icon · trigger label · name, full rule on
    /// hover. The name is always visible — deferred is never hidden (workbench-dossier.md).
    /// </summary>
    private VisualElement DeferredRow(InspectorSectionModel section, string context)
    {
        var row = new VisualElement();
        row.AddToClassList("wb-deferred-row");
        row.focusable = true;
        row.tabIndex = 0;

        var icon = new Label(string.IsNullOrWhiteSpace(section.Icon) ? "◇" : section.Icon);
        icon.AddToClassList("wb-deferred-row__icon");
        var label = new Label(section.Label);
        label.AddToClassList("wb-deferred-row__label");
        var name = new Label(DeferredName(section));
        name.AddToClassList("wb-deferred-row__name");
        var hint = new Label("HOVER");
        hint.AddToClassList("wb-deferred-row__hint");
        row.Add(icon);
        row.Add(label);
        row.Add(name);
        row.Add(hint);

        RuntimeTooltipModel Tooltip() => new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(section.Name),
            Eyebrow = section.Label,
            Title = DeferredName(section),
            Domain = "FULL RULE",
            Body = DeferredBody(section),
            Context = context,
        };
        if (_tooltips == null) row.tooltip = DeferredBody(section);
        else _tooltips.Attach(row, Tooltip);
        return row;
    }

    private static string DeferredName(InspectorSectionModel section)
    {
        if (section.Kind == InspectorSectionKind.Choices)
        {
            var names = new List<string>();
            foreach (ChoicePreviewModel choice in section.Choices) names.Add(choice.Name);
            return string.Join("  OR  ", names);
        }
        return section.Name;
    }

    private static string DeferredBody(InspectorSectionModel section)
    {
        if (section.Kind != InspectorSectionKind.Choices) return section.Summary;
        var lines = new List<string>();
        foreach (ChoicePreviewModel choice in section.Choices)
            lines.Add($"{choice.Name} — {choice.Rule}");
        return string.Join("\n\n", lines);
    }

    private void BindRankUp(InspectorModel model)
    {
        RankUpDetailModel detail = model.RankUpDetail;
        _rankUpGains.Clear();
        _rankUpLadder.Clear();
        _rankUpOptions.Clear();
        SetDisplayed(_rankUpBody, detail != null);
        if (detail == null) return;

        // The guaranteed gains as before → after rows — delta and absolute in one view
        // (workbench-dossier.md, law 4). These replace the +N stat chips.
        foreach (StatComparisonModel comparison in model.Comparisons)
            _rankUpGains.Add(ComparisonRow(comparison));
        SetDisplayed(_rankUpGains, _rankUpGains.childCount > 0);

        foreach (RankTierSlotModel tier in detail.Tiers)
            _rankUpLadder.Add(RankTier(tier, model.Title));
        foreach (ChoicePreviewModel option in detail.Options)
            _rankUpOptions.Add(RankUpOption(option, model.Title));
    }

    private VisualElement RankTier(RankTierSlotModel model, string context)
    {
        var slot = new VisualElement();
        slot.AddToClassList("wb-rank-tier");
        slot.AddToClassList("wb-rank-tier--" +
            model.State.ToString().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(model.Accent))
            slot.AddToClassList("accent--" + model.Accent);
        slot.focusable = true;
        slot.tabIndex = 0;
        slot.userData = model;

        var rank = new Label(model.Rank);
        rank.AddToClassList("wb-rank-tier__rank");
        var icon = new Label(model.Icon);
        icon.AddToClassList("wb-rank-tier__icon");
        var name = new Label(model.Name);
        name.AddToClassList("wb-rank-tier__name");
        slot.Add(rank);
        slot.Add(icon);
        slot.Add(name);

        RuntimeTooltipModel Tooltip() => new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(model.Name),
            Eyebrow = model.State switch
            {
                RankTierSlotState.Selected => $"RANK {model.Rank} SELECTED",
                RankTierSlotState.Pending => $"RANK {model.Rank} PENDING",
                _ => $"RANK {model.Rank} LOCKED",
            },
            Title = model.Name,
            Domain = "SPECIALIZATION TIER",
            Body = model.Rule,
            Context = context,
        };
        if (_tooltips == null) slot.tooltip = model.Rule;
        else _tooltips.Attach(slot, Tooltip);
        return slot;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool EditorShowFirstRankTierTooltip()
    {
        VisualElement slot =
            Root.Q<VisualElement>(className: "wb-rank-tier--selected") ??
            Root.Q<VisualElement>(className: "wb-rank-tier--pending");
        if (slot?.userData is not RankTierSlotModel model) return false;
        _tooltips?.EditorShow(slot, new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(model.Name),
            Eyebrow = $"RANK {model.Rank} " + model.State.ToString().ToUpperInvariant(),
            Title = model.Name,
            Domain = "SPECIALIZATION TIER",
            Body = model.Rule,
            Context = _title.text,
        });
        return _tooltips != null;
    }
#endif

    private VisualElement RankUpOption(ChoicePreviewModel model, string context)
    {
        var option = new VisualElement();
        option.AddToClassList("wb-rank-option");
        if (!string.IsNullOrWhiteSpace(model.Accent))
            option.AddToClassList("accent--" + model.Accent);
        option.focusable = true;
        option.tabIndex = 0;

        var change = new Label(model.Change);
        change.AddToClassList("wb-rank-option__change");
        var name = new Label(model.Name);
        name.AddToClassList("wb-rank-option__name");
        var rule = new Label();
        rule.AddToClassList("wb-rank-option__rule");
        // Scent, not the contract: the option preview carries the authored one-liner (or the
        // first sentence when none is authored); the full rule lives on hover and again in
        // the blocking choice after purchase. Machine trigger-prose runs a whole rule
        // without a sentence break, so it can never be the glance text.
        MechanicPresentation.BindInline(rule, string.IsNullOrWhiteSpace(model.Summary)
            ? FirstSentence(model.Rule)
            : model.Summary);
        option.Add(change);
        option.Add(name);
        option.Add(rule);

        RuntimeTooltipModel Tooltip() => new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(model.Name),
            Eyebrow = "RANK-UP OPTION",
            Title = model.Name,
            Domain = string.IsNullOrWhiteSpace(model.Change)
                ? "SPECIALIZATION"
                : model.Change,
            Body = model.Rule,
            Context = context,
        };
        if (_tooltips == null) option.tooltip = model.Rule;
        else _tooltips.Attach(option, Tooltip);
        return option;
    }

    private VisualElement TraitChip(
        WarbandSpecBadgeModel trait, string context, bool inline = false)
    {
        var chip = new VisualElement();
        chip.AddToClassList("wb-trait-chip");
        if (inline) chip.AddToClassList("wb-trait-chip--inline");
        if (!string.IsNullOrWhiteSpace(trait.Accent))
            chip.AddToClassList("accent--" + trait.Accent);
        chip.focusable = true;
        chip.tabIndex = 0;

        if (!inline)
        {
            var icon = new Label(trait.Icon);
            icon.AddToClassList("wb-trait-chip__icon");
            chip.Add(icon);
        }
        var rank = new Label(trait.Rank);
        rank.AddToClassList("wb-trait-chip__rank");
        var name = new Label(trait.Name);
        name.AddToClassList("wb-trait-chip__name");
        chip.Add(rank);
        chip.Add(name);

        RuntimeTooltipModel Tooltip() => new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(trait.Name),
            Eyebrow = $"RANK {trait.Rank} SELECTED TRAIT",
            Title = trait.Name,
            Domain = "SPECIALIZATION",
            Body = trait.Rule,
            Context = context,
        };
        if (_tooltips == null) chip.tooltip = trait.Rule;
        else _tooltips.Attach(chip, Tooltip);
        return chip;
    }

    private static VisualElement SectionHeading(InspectorSectionModel section)
    {
        var heading = new VisualElement();
        heading.AddToClassList("wb-inspector__section-heading");
        var label = new Label(section.Label);
        label.AddToClassList("wb-inspector__section-label");
        heading.Add(label);
        if (section.LabelGlyph == UiGlyphId.Unknown ||
            string.IsNullOrWhiteSpace(section.LabelValue))
            return heading;

        var context = new VisualElement();
        context.AddToClassList("wb-inspector__section-context");
        MechanicDefinition definition =
            MechanicPresentation.Definition(MechanicFamily.Mana);
        var glyph = new WarbandGlyph(section.LabelGlyph);
        glyph.SetColor(definition.Color);
        glyph.AddToClassList("wb-inspector__section-context-glyph");
        var value = new Label(section.LabelValue);
        value.AddToClassList("wb-inspector__section-context-value");
        context.Add(glyph);
        context.Add(value);
        heading.Add(context);
        return heading;
    }

    private void BindEquipmentPreview(EquipmentPreviewModel model)
    {
        bool shown = model != null;
        SetDisplayed(_equipmentPreview, shown);
        _recipients.Clear();
        _comparisonTable.Clear();
        _ruleDeltas.Clear();
        if (!shown) return;

        foreach (RecipientPreviewModel recipient in model.Recipients)
            _recipients.Add(RecipientChip(recipient));

        _comparisonTitle.text =
            $"{model.CurrentItemName.ToUpperInvariant()}  →  " +
            model.OfferedItemName.ToUpperInvariant();
        foreach (StatComparisonModel comparison in model.StatDeltas)
            _comparisonTable.Add(ComparisonRow(comparison));

        if (model.LostRule != null)
            _ruleDeltas.Add(RuleDelta("LOSE", model.LostRule, "loss"));
        if (model.GainedRule != null)
            _ruleDeltas.Add(RuleDelta("GAIN", model.GainedRule, "gain"));
    }

    private Button RecipientChip(RecipientPreviewModel model)
    {
        var chip = new Button(() =>
        {
            if (model.IsEligible) _onRecipient?.Invoke(model.HeroKey);
        });
        chip.AddToClassList("wb-recipient");
        chip.EnableInClassList("wb-recipient--selected", model.IsSelected);
        chip.EnableInClassList("wb-recipient--invalid", !model.IsEligible);
        chip.SetEnabled(model.IsEligible);

        var portrait = new VisualElement();
        portrait.AddToClassList("wb-recipient__portrait");
        Texture2D texture = string.IsNullOrEmpty(model.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(model.PortraitResource);
        portrait.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        if (texture == null)
        {
            var fallback = new Label(model.PortraitFallback);
            fallback.AddToClassList("wb-recipient__fallback");
            portrait.Add(fallback);
        }

        var rank = new Label(model.RankText);
        rank.AddToClassList("wb-recipient__rank");
        var name = new Label(model.DisplayName);
        name.AddToClassList("wb-recipient__name");
        chip.Add(portrait);
        chip.Add(rank);
        chip.Add(name);
        chip.tooltip = model.IsEligible
            ? $"{model.DisplayName} · {model.RankText}\nEquipped: {model.CurrentItemName}"
            : $"{model.DisplayName} · {model.IneligibleReason}";
        return chip;
    }

    private static VisualElement RuleDelta(string verb, RuleDeltaModel model, string tone)
    {
        var root = new VisualElement();
        root.AddToClassList("wb-rule-delta");
        root.AddToClassList("wb-rule-delta--" + tone);
        root.EnableInClassList("wb-rule-delta--inactive", !model.Applies);
        var label = new Label(verb + (model.Applies ? "" : " · INACTIVE"));
        label.AddToClassList("wb-rule-delta__label");
        var copy = new Label();
        copy.AddToClassList("wb-rule-delta__copy");
        MechanicPresentation.BindInline(copy,
            $"{model.RuleName} · {model.ShortSummary}");
        root.Add(label);
        root.Add(copy);
        root.tooltip = model.FullDescription;
        return root;
    }

    private VisualElement RuleLine(
        InspectorSectionModel section,
        IReadOnlyList<WarbandSpecBadgeModel> traits,
        IReadOnlyList<string> keywordNotes,
        string context)
    {
        var line = new VisualElement();
        line.AddToClassList("wb-inspector__line");
        var icon = new Label(section.Icon);
        icon.AddToClassList("wb-inspector__line-icon");
        var copy = new VisualElement();
        copy.AddToClassList("wb-inspector__line-body");
        var titleRow = new VisualElement();
        titleRow.AddToClassList("wb-inspector__line-title-row");
        var title = new Label(section.Name);
        title.AddToClassList("wb-inspector__line-title");
        var summary = new Label();
        summary.AddToClassList("wb-inspector__line-copy");
        summary.AddToClassList("wb-inspector__semantic-copy");
        SemanticTextBinding semantic = MechanicPresentation.BindSemantic(
            summary, section.Summary, keywordNotes, context);
        _tooltips?.AttachSemantic(summary, semantic);
        SpecRuleContext ruleContext = SectionContext(section.Label);
        bool hasContextTraits = false;
        if (traits != null)
            foreach (WarbandSpecBadgeModel trait in traits)
                if (trait.Context == ruleContext)
                {
                    hasContextTraits = true;
                    titleRow.Add(TraitChip(trait, context, inline: true));
                }
        bool titleLivesInSentence =
            semantic.Find(section.Name) != null;
        if (!hasContextTraits && !titleLivesInSentence) titleRow.Add(title);
        if (titleRow.childCount > 0) copy.Add(titleRow);
        copy.Add(summary);
        line.Add(icon);
        line.Add(copy);
        return line;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool EditorShowSemanticKeywordTooltip(string preferredKeyword = "")
    {
        List<Label> labels =
            Root.Query<Label>(className: "semantic-text--interactive").ToList();
        foreach (Label label in labels)
        {
            if (label.userData is not SemanticTextBinding binding) continue;
            SemanticTextToken token = string.IsNullOrWhiteSpace(preferredKeyword)
                ? binding.Tokens.Count > 0 ? binding.Tokens[0] : null
                : binding.Find(preferredKeyword);
            if (token == null) continue;
            _tooltips?.EditorShow(label, token.Tooltip(binding.Context));
            return _tooltips != null;
        }
        return false;
    }
#endif

    private static SpecRuleContext SectionContext(string label)
    {
        string value = (label ?? "").ToUpperInvariant();
        if (value.Contains("BASIC")) return SpecRuleContext.BasicAttack;
        if (value.Contains("SIGNATURE")) return SpecRuleContext.Signature;
        return SpecRuleContext.Passive;
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
        if (!string.IsNullOrEmpty(model.AbilityName) ||
            !string.IsNullOrEmpty(model.AbilitySummary))
            result.Add(new InspectorSectionModel
            {
                Label = "SIGNATURE",
                Icon = model.AbilityIcon,
                Name = model.AbilityName,
                Summary = model.AbilitySummary,
                LabelGlyph = model.AbilityManaCost >= 0
                    ? UiGlyphId.Mana
                    : UiGlyphId.Unknown,
                LabelValue = model.AbilityManaCost >= 0
                    ? model.AbilityManaCost.ToString()
                    : "",
            });
        Add(PassiveSectionLabel(model.PassiveTrigger), model.PassiveIcon,
            model.PassiveName, model.PassiveSummary);
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

    private static string PassiveSectionLabel(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return "PASSIVE";
        const string always = " · ALWAYS";
        return trigger.EndsWith(always, StringComparison.OrdinalIgnoreCase)
            ? trigger.Substring(0, trigger.Length - always.Length)
            : trigger;
    }

    /// <summary>First sentence of a rule, with a continuation mark when more follows.
    /// Generated trigger-prose can run 170+ characters without a sentence break, so a
    /// word-boundary budget backstops the split — the option card clips overflow, and the
    /// full text always survives on hover.</summary>
    private static string FirstSentence(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule)) return rule;
        int end = rule.IndexOf(". ", StringComparison.Ordinal);
        string sentence = end < 0 ? rule : rule.Substring(0, end + 1) + " …";
        const int budget = 120;
        if (sentence.Length <= budget) return sentence;
        int cut = sentence.LastIndexOf(' ', budget);
        if (cut <= 0) cut = budget;
        return sentence.Substring(0, cut) + " …";
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
        row.EnableInClassList("wb-comparison--good",
            comparison.Direction == DeltaDirection.Positive ||
            comparison.Tone == "good");
        row.EnableInClassList("wb-comparison--bad",
            comparison.Direction == DeltaDirection.Negative ||
            comparison.Tone == "bad");
        row.EnableInClassList("wb-comparison--contextual",
            comparison.Direction == DeltaDirection.Contextual);
        row.tooltip = string.IsNullOrWhiteSpace(comparison.Explanation)
            ? definition.Tooltip
            : comparison.Explanation;
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
        var direction = new Label(comparison.Direction == DeltaDirection.Positive ? "+" :
            comparison.Direction == DeltaDirection.Negative ? "−" :
            comparison.Direction == DeltaDirection.Contextual ? "◆" : "=");
        direction.AddToClassList("wb-comparison__direction");
        row.Add(icon);
        row.Add(label);
        row.Add(before);
        row.Add(arrow);
        row.Add(after);
        row.Add(direction);
        return row;
    }
}
