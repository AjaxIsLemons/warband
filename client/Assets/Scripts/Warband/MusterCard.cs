using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal enum MusterLensTarget
{
    None,
    Health,
    Basic,
    Reach,
    Signature,
    Passive,
    Combined,
}

/// <summary>
/// Purpose-built opening-draft card: portrait identity, three fixed comparison facts, and two
/// named rules. Exact mechanics are disclosed inside the portrait instead of growing the card.
/// </summary>
internal sealed class MusterCard
{
    private sealed class FactView
    {
        public readonly MusterFactKind Kind;
        public readonly VisualElement Root = new VisualElement();
        public readonly WarbandGlyph Icon = new WarbandGlyph();
        public readonly Label Value = new Label();
        public readonly WarbandGlyph SecondaryIcon = new WarbandGlyph();
        public readonly Label SecondaryValue = new Label();

        public FactView(MusterFactKind kind)
        {
            Kind = kind;
            Root.AddToClassList("muster-fact");
            Root.AddToClassList("muster-fact--" + kind.ToString().ToLowerInvariant());
            Root.pickingMode = PickingMode.Position;
            Icon.AddToClassList("muster-fact__icon");
            Value.AddToClassList("muster-fact__value");
            SecondaryIcon.AddToClassList("muster-fact__secondary-icon");
            SecondaryValue.AddToClassList("muster-fact__secondary-value");
            Root.Add(Icon);
            Root.Add(Value);
            Root.Add(SecondaryIcon);
            Root.Add(SecondaryValue);
        }

        public void Bind(MusterFactModel model)
        {
            Icon.Set(model.Icon);
            Value.text = model.Value;
            SecondaryIcon.Set(model.SecondaryIcon);
            SecondaryValue.text = model.SecondaryValue;
            Root.tooltip = model.AccessibleLabel + " · " + model.TooltipBody;
            SetDisplayed(SecondaryIcon, model.SecondaryIcon != UiGlyphId.Unknown);
            SetDisplayed(SecondaryValue, !string.IsNullOrEmpty(model.SecondaryValue));
        }
    }

    private sealed class RuleView
    {
        public readonly MusterRuleKind Kind;
        public readonly VisualElement Root = new VisualElement();
        public readonly WarbandGlyph Icon = new WarbandGlyph();
        public readonly Label Name = new Label();
        public readonly VisualElement Keyword = new VisualElement();
        public readonly WarbandGlyph KeywordIcon = new WarbandGlyph();
        public readonly Label KeywordText = new Label();
        public readonly Label Context = new Label();

        public RuleView(MusterRuleKind kind)
        {
            Kind = kind;
            Root.AddToClassList("muster-rule");
            Root.AddToClassList("muster-rule--" + kind.ToString().ToLowerInvariant());
            Root.pickingMode = PickingMode.Position;

            Icon.AddToClassList("muster-rule__icon");
            Root.Add(Icon);

            var copy = new VisualElement();
            copy.AddToClassList("muster-rule__copy");
            Name.AddToClassList("muster-rule__name");
            copy.Add(Name);

            Keyword.AddToClassList("muster-rule__keyword");
            KeywordIcon.AddToClassList("muster-rule__keyword-icon");
            KeywordText.AddToClassList("muster-rule__keyword-text");
            Keyword.Add(KeywordIcon);
            Keyword.Add(KeywordText);
            copy.Add(Keyword);
            Root.Add(copy);

            Context.AddToClassList("muster-rule__context");
            Root.Add(Context);
        }

        public void Bind(MusterRuleModel model)
        {
            Icon.Set(model.Icon);
            Name.text = model.Name;
            KeywordIcon.Set(model.KeywordIcon);
            KeywordText.text = model.Keyword.ToUpperInvariant();
            Context.text = model.Context;
            Root.tooltip = model.Name + " · " + model.ExactRule;
        }
    }

    private readonly Action<string> _onToggle;
    private readonly UiMusterTuning _tuning;
    private readonly VisualElement _portrait;
    private readonly Label _portraitFallback;
    private readonly WarbandGlyph _roleIcon;
    private readonly Label _roleBadge;
    private readonly Label _name;
    private readonly Label _role;
    private readonly VisualElement _facts;
    private readonly Dictionary<MusterFactKind, FactView> _factViews =
        new Dictionary<MusterFactKind, FactView>();
    private readonly RuleView _signature;
    private readonly RuleView _passive;
    private readonly VisualElement _lens;
    private readonly Label _lensEyebrow;
    private readonly Label _lensTitle;
    private readonly Label _lensContext;
    private readonly Label _lensBody;
    private readonly VisualElement _lensKeywords;
    private readonly VisualElement _selectionMark;
    private readonly WarbandGlyph _selectionGlyph;
    private readonly Label _selectionLabel;
    private readonly VisualElement _info;

    private MusterCardModel _model = new MusterCardModel();
    private string _key = "";
    private bool _pointerDown;
    private bool _focused;
    private bool _lensHasShown;
    private int _lensGeneration;
    private int _motionGeneration;
    private MusterLensTarget _lensTarget;
    private IVisualElementScheduledItem _lensTask;
    private IVisualElementScheduledItem _motionTask;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private string _lastLayoutWarning = "";
#endif

    public VisualElement Root { get; }
    public VisualElement Portrait => _portrait;

    public MusterCard(Action<string> onToggle, UiMusterTuning tuning)
    {
        _onToggle = onToggle;
        _tuning = tuning ?? new UiMusterTuning();

        Root = new VisualElement
        {
            focusable = true,
            tabIndex = 0,
            pickingMode = PickingMode.Position,
        };
        Root.AddToClassList("muster-card");
        Root.usageHints |= UsageHints.DynamicTransform;

        _portrait = new VisualElement();
        _portrait.AddToClassList("muster-card__portrait");
        _portraitFallback = new Label();
        _portraitFallback.AddToClassList("muster-card__portrait-fallback");
        _portrait.Add(_portraitFallback);

        var role = new VisualElement();
        role.AddToClassList("muster-card__role-badge");
        _roleIcon = new WarbandGlyph();
        _roleIcon.AddToClassList("muster-card__role-icon");
        _roleBadge = new Label();
        _roleBadge.AddToClassList("muster-card__role-badge-text");
        role.Add(_roleIcon);
        role.Add(_roleBadge);
        _portrait.Add(role);

        _lens = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
        };
        _lens.AddToClassList("muster-lens");
        _lensEyebrow = new Label();
        _lensEyebrow.AddToClassList("muster-lens__eyebrow");
        _lensTitle = new Label();
        _lensTitle.AddToClassList("muster-lens__title");
        _lensContext = new Label();
        _lensContext.AddToClassList("muster-lens__context");
        _lensBody = new Label();
        _lensBody.AddToClassList("muster-lens__body");
        _lensKeywords = new VisualElement();
        _lensKeywords.AddToClassList("muster-lens__keywords");
        _lens.Add(_lensEyebrow);
        _lens.Add(_lensTitle);
        _lens.Add(_lensContext);
        _lens.Add(_lensBody);
        _lens.Add(_lensKeywords);
        _portrait.Add(_lens);
        Root.Add(_portrait);

        _info = new VisualElement();
        _info.AddToClassList("muster-card__info");
        var identity = new VisualElement();
        identity.AddToClassList("muster-card__identity");
        _name = new Label();
        _name.AddToClassList("muster-card__name");
        _role = new Label();
        _role.AddToClassList("muster-card__role");
        identity.Add(_name);
        identity.Add(_role);
        _info.Add(identity);

        _facts = new VisualElement();
        _facts.AddToClassList("muster-card__facts");
        AddFact(MusterFactKind.Health);
        AddFact(MusterFactKind.Basic);
        AddFact(MusterFactKind.Reach);
        _info.Add(_facts);

        _signature = AddRule(MusterRuleKind.Signature);
        _passive = AddRule(MusterRuleKind.Passive);
        Root.Add(_info);

        _selectionMark = new VisualElement();
        _selectionMark.AddToClassList("muster-card__selection");
        _selectionGlyph = new WarbandGlyph(UiGlyphId.Check);
        _selectionGlyph.AddToClassList("muster-card__selection-icon");
        _selectionLabel = new Label();
        _selectionLabel.AddToClassList("muster-card__selection-label");
        _selectionMark.Add(_selectionGlyph);
        _selectionMark.Add(_selectionLabel);
        Root.Add(_selectionMark);

        Root.RegisterCallback<ClickEvent>(OnClick);
        Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        Root.RegisterCallback<PointerDownEvent>(_ => _pointerDown = true);
        Root.RegisterCallback<PointerUpEvent>(_ =>
        {
            _pointerDown = false;
        });
        Root.RegisterCallback<PointerCancelEvent>(_ => _pointerDown = false);
        Root.RegisterCallback<FocusInEvent>(OnFocusIn);
        Root.RegisterCallback<FocusOutEvent>(OnFocusOut);
        Root.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (!_focused) ScheduleLens(MusterLensTarget.None, _tuning.lensCloseMs);
        });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Root.RegisterCallback<GeometryChangedEvent>(_ => ValidateResolvedLayout());
#endif
        HideLensImmediately();
    }

    public void Bind(MusterCardModel model)
    {
        _model = model ?? new MusterCardModel();
        _key = _model.Key;
        Root.userData = "muster:" + _key;
        Root.tooltip = _model.CanToggle
            ? $"{_model.Name}, {_model.Role}. Select or remove this champion."
            : _model.DisabledReason;

        _name.text = _model.Name;
        _role.text = _model.Role;
        _roleBadge.text = _model.Role.ToUpperInvariant();
        _roleIcon.Set(_model.RoleIcon);
        _portraitFallback.text = _model.PortraitFallback;
        _selectionLabel.text = _model.SelectedSlot >= 0
            ? $"PICK {_model.SelectedSlot + 1}"
            : "PICKED";

        Texture2D texture = string.IsNullOrEmpty(_model.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(_model.PortraitResource);
        _portrait.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        SetDisplayed(_portraitFallback, texture == null);

        foreach (MusterFactModel fact in _model.Facts)
            if (_factViews.TryGetValue(fact.Kind, out FactView view)) view.Bind(fact);
        _signature.Bind(_model.Signature);
        _passive.Bind(_model.Passive);

        Root.EnableInClassList("muster-card--selected", _model.Selected);
        Root.EnableInClassList("muster-card--blocked", !_model.CanToggle && !_model.Selected);
        WarbandCard.SetAccent(Root, _model.Accent);
        SetDisplayed(_selectionMark, _model.Selected);
        MusterPresentationContract.ValidateOrWarn(_model);

        if (_lensTarget != MusterLensTarget.None)
            PopulateLens(_lensTarget);
    }

    public void PlayReveal(int index, bool reducedMotion)
    {
        int generation = ++_motionGeneration;
        _motionTask?.Pause();
        UiMotionRecipe recipe = _tuning.reveal;
        int stagger = reducedMotion ? 0 :
            Mathf.Min(index * recipe.staggerMs, recipe.staggerCapMs);
        int duration = reducedMotion ? _tuning.reducedFadeMs : recipe.durationMs;

        SetMotionImmediately(_portrait, recipe.startOpacity,
            reducedMotion ? 0f : recipe.distancePx);
        SetMotionImmediately(_info, recipe.startOpacity,
            reducedMotion ? 0f : recipe.distancePx);
        _motionTask = Root.schedule.Execute(() =>
        {
            if (generation != _motionGeneration) return;
            AnimateToRest(_portrait, duration);
            Root.schedule.Execute(() =>
            {
                if (generation == _motionGeneration) AnimateToRest(_info, duration);
            }).ExecuteLater(reducedMotion ? 0 : _tuning.revealInfoDelayMs);
        });
        _motionTask.ExecuteLater(stagger + 16);
    }

    public void CancelPresentation()
    {
        _lensGeneration++;
        _motionGeneration++;
        _lensTask?.Pause();
        _motionTask?.Pause();
        _lensTask = null;
        _motionTask = null;
        _pointerDown = false;
        _focused = false;
        HideLensImmediately();
        ClearMotion(_portrait);
        ClearMotion(_info);
        ClearMotion(Root);
    }

    public void PreviewLens(MusterLensTarget target)
    {
        _lensGeneration++;
        _lensTask?.Pause();
        _lensTask = null;
        if (target == MusterLensTarget.None) HideLensImmediately();
        else ShowLens(target);
    }

    private void AddFact(MusterFactKind kind)
    {
        var view = new FactView(kind);
        _factViews.Add(kind, view);
        _facts.Add(view.Root);
        view.Root.RegisterCallback<PointerEnterEvent>(evt =>
        {
            if (!IsPointerDisclosure(evt.pointerType)) return;
            ScheduleLens(ToLens(kind), _lensHasShown
                ? _tuning.lensReshowMs
                : _tuning.lensInitialDelayMs);
        });
        view.Root.RegisterCallback<PointerLeaveEvent>(_ =>
            ScheduleLens(MusterLensTarget.None, _tuning.lensCloseMs));
    }

    private RuleView AddRule(MusterRuleKind kind)
    {
        var view = new RuleView(kind);
        _info.Add(view.Root);
        view.Root.RegisterCallback<PointerEnterEvent>(evt =>
        {
            if (!IsPointerDisclosure(evt.pointerType)) return;
            ScheduleLens(kind == MusterRuleKind.Signature
                    ? MusterLensTarget.Signature
                    : MusterLensTarget.Passive,
                _lensHasShown ? _tuning.lensReshowMs : _tuning.lensInitialDelayMs);
        });
        view.Root.RegisterCallback<PointerLeaveEvent>(_ =>
            ScheduleLens(MusterLensTarget.None, _tuning.lensCloseMs));
        return view;
    }

    private void OnClick(ClickEvent evt)
    {
        if (!string.IsNullOrEmpty(_key)) _onToggle?.Invoke(_key);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
        OnClick(null);
        evt.StopPropagation();
    }

    private void OnFocusIn(FocusInEvent evt)
    {
        _focused = true;
        if (_pointerDown) return;
        ScheduleLens(MusterLensTarget.Combined,
            _lensHasShown ? _tuning.lensReshowMs : _tuning.lensInitialDelayMs);
    }

    private void OnFocusOut(FocusOutEvent evt)
    {
        _focused = false;
        ScheduleLens(MusterLensTarget.None, _tuning.lensCloseMs);
    }

    private void ScheduleLens(MusterLensTarget target, int delayMs)
    {
        int generation = ++_lensGeneration;
        _lensTask?.Pause();
        _lensTask = Root.schedule.Execute(() =>
        {
            if (generation != _lensGeneration) return;
            if (target == MusterLensTarget.None) HideLensImmediately();
            else ShowLens(target);
        });
        _lensTask.ExecuteLater(Mathf.Max(0, delayMs));
    }

    private void ShowLens(MusterLensTarget target)
    {
        _lensTarget = target;
        _lensHasShown = true;
        PopulateLens(target);
        _portrait.AddToClassList("muster-card__portrait--lens");
        _lens.AddToClassList("muster-lens--visible");
    }

    private void HideLensImmediately()
    {
        _lensTarget = MusterLensTarget.None;
        _portrait?.RemoveFromClassList("muster-card__portrait--lens");
        _lens?.RemoveFromClassList("muster-lens--visible");
    }

    private void PopulateLens(MusterLensTarget target)
    {
        _lensKeywords.Clear();
        if (target == MusterLensTarget.Signature)
        {
            PopulateRuleLens(_model.Signature);
            return;
        }
        if (target == MusterLensTarget.Passive)
        {
            PopulateRuleLens(_model.Passive);
            return;
        }
        if (target == MusterLensTarget.Combined)
        {
            _lensEyebrow.text = "STARTING KIT";
            _lensTitle.text = _model.Name;
            _lensContext.text = _model.Role.ToUpperInvariant() + " · EXACT RULES";
            _lensBody.text =
                $"{BasicScanLine()}\n\n" +
                $"{_model.Signature.Name.ToUpperInvariant()} · {_model.Signature.ExactRule}\n\n" +
                $"{_model.Passive.Name.ToUpperInvariant()} · {_model.Passive.ExactRule}";
            AddKeyword(_model.Signature.Keyword, _model.Signature.KeywordIcon);
            AddKeyword(_model.Passive.Keyword, _model.Passive.KeywordIcon);
            return;
        }

        MusterFactKind kind = target == MusterLensTarget.Health
            ? MusterFactKind.Health
            : target == MusterLensTarget.Basic
                ? MusterFactKind.Basic
                : MusterFactKind.Reach;
        MusterFactModel fact = _model.Facts.Find(value => value.Kind == kind);
        if (fact == null) return;
        _lensEyebrow.text = "COMBAT STAT";
        _lensTitle.text = fact.TooltipTitle;
        _lensContext.text = fact.AccessibleLabel.ToUpperInvariant();
        _lensBody.text = fact.TooltipBody;
    }

    private void PopulateRuleLens(MusterRuleModel rule)
    {
        _lensEyebrow.text = rule.Kind == MusterRuleKind.Signature ? "SIGNATURE" : "PASSIVE";
        _lensTitle.text = rule.Name;
        _lensContext.text = string.IsNullOrEmpty(rule.Context)
            ? rule.Keyword.ToUpperInvariant()
            : rule.Keyword.ToUpperInvariant() + " · " + rule.Context;
        _lensBody.text = rule.ExactRule;
        AddKeyword(rule.Keyword, rule.KeywordIcon);
        foreach (string note in rule.KeywordNotes) AddKeyword(note, UiGlyphId.Unknown);
    }

    private string BasicScanLine()
    {
        MusterFactModel basic = _model.Facts.Find(value => value.Kind == MusterFactKind.Basic);
        MusterFactModel reach = _model.Facts.Find(value => value.Kind == MusterFactKind.Reach);
        if (basic == null || reach == null) return "";
        return $"{basic.AccessibleLabel} · REACH {reach.Value}";
    }

    private void AddKeyword(string text, UiGlyphId glyph)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var row = new VisualElement();
        row.AddToClassList("muster-lens__keyword");
        if (glyph != UiGlyphId.Unknown)
        {
            var icon = new WarbandGlyph(glyph);
            icon.AddToClassList("muster-lens__keyword-icon");
            row.Add(icon);
        }
        var label = new Label(text);
        label.AddToClassList("muster-lens__keyword-copy");
        row.Add(label);
        _lensKeywords.Add(row);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool ValidateResolvedLayout()
    {
        if (Root.panel == null || Root.resolvedStyle.display == DisplayStyle.None) return true;
        // Reveal motion deliberately begins below the card. Do not report that authored
        // transition pose as content overflow; validate once the information has settled.
        if (_info.resolvedStyle.opacity < 0.999f) return true;
        const float epsilon = 0.75f;
        bool infoEscapes = _info.worldBound.yMax > Root.worldBound.yMax + epsilon ||
                           _info.worldBound.xMin < Root.worldBound.xMin - epsilon ||
                           _info.worldBound.xMax > Root.worldBound.xMax + epsilon;
        bool rulesOverlap = _signature.Root.worldBound.yMax >
                            _passive.Root.worldBound.yMin + epsilon;
        bool nameEscapes = _name.worldBound.xMax > Root.worldBound.xMax + epsilon;
        bool valid = !infoEscapes && !rulesOverlap && !nameEscapes;
        if (valid)
        {
            _lastLayoutWarning = "";
            return true;
        }

        string signature =
            $"{_key}|{Mathf.RoundToInt(Root.resolvedStyle.width)}x" +
            $"{Mathf.RoundToInt(Root.resolvedStyle.height)}|{infoEscapes}|" +
            $"{rulesOverlap}|{nameEscapes}";
        if (!string.Equals(signature, _lastLayoutWarning, StringComparison.Ordinal))
        {
            _lastLayoutWarning = signature;
            Debug.LogError(
                $"[Muster Card Layout] '{_model.Name}' violated its layout contract at " +
                $"{Root.resolvedStyle.width:0}×{Root.resolvedStyle.height:0}. " +
                $"info escape={infoEscapes}, rule overlap={rulesOverlap}, " +
                $"name escape={nameEscapes}.");
        }
        return false;
    }
#endif

    private static MusterLensTarget ToLens(MusterFactKind kind) =>
        kind == MusterFactKind.Health ? MusterLensTarget.Health :
        kind == MusterFactKind.Basic ? MusterLensTarget.Basic :
        MusterLensTarget.Reach;

    private static bool IsPointerDisclosure(string pointerType) =>
        pointerType == UnityEngine.UIElements.PointerType.mouse ||
        pointerType == UnityEngine.UIElements.PointerType.pen;

    private static void SetMotionImmediately(VisualElement element, float opacity, float y)
    {
        element.style.transitionDuration =
            new List<TimeValue> { new TimeValue(0f, TimeUnit.Millisecond) };
        element.style.opacity = opacity;
        element.style.translate = new Translate(0f, y);
    }

    private static void AnimateToRest(VisualElement element, int durationMs)
    {
        element.style.transitionProperty = new List<StylePropertyName>
        {
            new StylePropertyName("opacity"),
            new StylePropertyName("translate"),
        };
        var duration = new TimeValue(Mathf.Max(0, durationMs), TimeUnit.Millisecond);
        element.style.transitionDuration = new List<TimeValue> { duration, duration };
        element.style.transitionTimingFunction = new List<EasingFunction>
        {
            new EasingFunction(EasingMode.EaseOut),
            new EasingFunction(EasingMode.EaseOut),
        };
        element.style.opacity = 1f;
        element.style.translate = new Translate(0f, 0f);
    }

    private static void ClearMotion(VisualElement element)
    {
        if (element == null) return;
        element.style.opacity = StyleKeyword.Null;
        element.style.translate = StyleKeyword.Null;
        element.style.scale = StyleKeyword.Null;
        element.style.transitionProperty = StyleKeyword.Null;
        element.style.transitionDuration = StyleKeyword.Null;
        element.style.transitionTimingFunction = StyleKeyword.Null;
    }

    private static void SetDisplayed(VisualElement element, bool displayed) =>
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
}

internal static class MusterPresentationContract
{
    private static readonly MusterFactKind[] ExpectedFacts =
    {
        MusterFactKind.Health,
        MusterFactKind.Basic,
        MusterFactKind.Reach,
    };

    public static void Validate(IReadOnlyList<MusterCardModel> cards)
    {
        if (cards == null || cards.Count == 0)
            throw new InvalidOperationException("[Muster Contract] Opening offer cannot be empty.");
        foreach (MusterCardModel card in cards) Validate(card);
    }

    public static void ValidateOrWarn(MusterCardModel card)
    {
        try
        {
            Validate(card);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
        }
    }

    private static void Validate(MusterCardModel card)
    {
        Require(card != null, "card cannot be null");
        Require(!string.IsNullOrWhiteSpace(card.Key), "card key is required");
        Require(!string.IsNullOrWhiteSpace(card.Name), card.Key + " needs a name");
        Require(!string.IsNullOrWhiteSpace(card.Role), card.Key + " needs a plain-language role");
        Require(card.RoleIcon != UiGlyphId.Unknown, card.Key + " needs a role glyph");
        Require(card.Facts != null && card.Facts.Count == 3,
            card.Key + " must expose exactly Health, Basic, and Reach");
        for (int i = 0; i < ExpectedFacts.Length; i++)
        {
            MusterFactModel fact = card.Facts[i];
            Require(fact.Kind == ExpectedFacts[i],
                card.Key + " fact order must be Health, Basic, Reach");
            Require(fact.Icon != UiGlyphId.Unknown, card.Key + " facts need glyphs");
            Require(!string.IsNullOrWhiteSpace(fact.Value), card.Key + " facts need values");
            Require(!string.IsNullOrWhiteSpace(fact.TooltipTitle),
                card.Key + " facts need tooltip titles");
            Require(!string.IsNullOrWhiteSpace(fact.TooltipBody),
                card.Key + " facts need exact disclosure");
        }
        ValidateRule(card.Key, card.Signature, MusterRuleKind.Signature);
        ValidateRule(card.Key, card.Passive, MusterRuleKind.Passive);
    }

    private static void ValidateRule(string key, MusterRuleModel rule, MusterRuleKind expected)
    {
        Require(rule != null && rule.Kind == expected, key + " rule kind mismatch");
        Require(rule.Icon != UiGlyphId.Unknown, key + " rules need glyphs");
        Require(rule.KeywordIcon != UiGlyphId.Unknown, key + " keywords need glyphs");
        Require(!string.IsNullOrWhiteSpace(rule.Name), key + " rules need names");
        Require(!string.IsNullOrWhiteSpace(rule.Keyword), key + " rules need scan keywords");
        Require(!string.IsNullOrWhiteSpace(rule.ExactRule), key + " rules need exact language");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Muster Contract] " + message);
    }
}
