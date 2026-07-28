using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

internal enum RuntimeTooltipKind
{
    General,
    Keyword,
    Equipment,
    DisabledReason,
}

internal sealed class RuntimeTooltipModel
{
    public RuntimeTooltipKind Kind;
    public MechanicFamily Family = MechanicFamily.Neutral;
    public UiGlyphId Glyph = UiGlyphId.Unknown;
    public string Eyebrow = "";
    public string Title = "";
    public string Domain = "";
    public string Body = "";
    public string Context = "";
    public string Footer = "";
    public List<StatChipModel> Facts = new List<StatChipModel>();

    public static RuntimeTooltipModel Keyword(string note, string context = "")
    {
        SplitKeyword(note, out string title, out string body);
        MechanicFamily family = KeywordFamily(title);
        return new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.Keyword,
            Family = family,
            Glyph = UiGlyphCatalog.Keyword(title == "REGENERATION" ? "REGEN" : title),
            Eyebrow = "KEYWORD",
            Title = title,
            Domain = MechanicPresentation.Definition(family).Semantic.ToUpperInvariant() +
                     " · COMBAT KEYWORD",
            Body = body,
            Context = string.IsNullOrWhiteSpace(context)
                ? ""
                : "SEEN ON  " + context.ToUpperInvariant(),
            Footer = "Select the source to pin its full dossier.",
        };
    }

    public static RuntimeTooltipModel Equipment(
        string eyebrow, string title, string rule, IReadOnlyList<StatChipModel> facts = null,
        string context = "")
    {
        MechanicFamily family = SemanticFamily(rule);
        if (family == MechanicFamily.Neutral && facts != null)
            foreach (StatChipModel fact in facts)
            {
                family = MechanicPresentation.Family(fact?.Id ??
                    PresentationFactId.Unknown);
                if (family != MechanicFamily.Neutral) break;
            }
        var model = new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.Equipment,
            Family = family,
            Eyebrow = eyebrow,
            Title = title,
            Domain = "EQUIPMENT",
            Body = rule,
            Context = context,
            Footer = "Select the source for its full dossier.",
        };
        if (facts != null)
            foreach (StatChipModel fact in facts)
                if (fact != null) model.Facts.Add(fact);
        return model;
    }

    private static MechanicFamily KeywordFamily(string keyword)
    {
        string value = (keyword ?? "").Trim().ToUpperInvariant();
        if (value is "REGEN" or "REGENERATION")
            return MechanicFamily.Restoration;
        if (value is "SHIELD" or "WARD" or "BARRIER")
            return MechanicFamily.Protection;
        if (value is "MANA")
            return MechanicFamily.Mana;
        if (value is "HASTE" or "FRENZY" or "STUN" or "SLOW")
            return MechanicFamily.Time;
        if (value is "LEAP" or "LINE" or "GLYPH" or "AREA" or "DISTANCE")
            return MechanicFamily.Space;
        if (value is "BURN" or "RIPOSTE" or "COUNTER")
            return MechanicFamily.Offense;
        return MechanicPresentation.Family(value);
    }

    private static MechanicFamily SemanticFamily(string text)
    {
        string value = (text ?? "").ToUpperInvariant();
        foreach (string term in new[] { "HEAL", "RESTORE", "REGEN" })
            if (value.Contains(term)) return MechanicFamily.Restoration;
        foreach (string term in new[] { "SHIELD", "WARD", "ARMOR", "BARRIER" })
            if (value.Contains(term)) return MechanicFamily.Protection;
        if (value.Contains("MANA")) return MechanicFamily.Mana;
        foreach (string term in new[] { "CADENCE", "HASTE", "SLOW", "SECOND", "DURATION" })
            if (value.Contains(term)) return MechanicFamily.Time;
        foreach (string term in new[] { "REACH", "RANGE", "HEX", "AREA", "LINE" })
            if (value.Contains(term)) return MechanicFamily.Space;
        foreach (string term in new[] { "DAMAGE", "ATTACK", "POWER", "CRIT", "CLEAVE" })
            if (value.Contains(term)) return MechanicFamily.Offense;
        return MechanicFamily.Neutral;
    }

    private static void SplitKeyword(string note, out string title, out string body)
    {
        string value = (note ?? "").Trim();
        foreach (string separator in new[] { " — ", " – ", " · ", ": " })
        {
            int split = value.IndexOf(separator, StringComparison.Ordinal);
            if (split <= 0) continue;
            title = value.Substring(0, split).Trim().ToUpperInvariant();
            body = value.Substring(split + separator.Length).Trim();
            return;
        }
        title = value.ToUpperInvariant();
        body = "A named combat rule. Its highlighted color and icon stay consistent wherever " +
               "this rule appears.";
    }
}

/// <summary>
/// One shell-owned, delayed, focus-accessible tooltip layer. Views provide semantic models; this
/// service owns timing, stale-request cancellation, theming, and safe-bound placement.
/// </summary>
internal sealed class RuntimeTooltipService : IDisposable
{
    private sealed class AnchorManipulator : Manipulator
    {
        private readonly RuntimeTooltipService _service;
        private readonly Func<RuntimeTooltipModel> _model;

        public AnchorManipulator(RuntimeTooltipService service,
                                 Func<RuntimeTooltipModel> model)
        {
            _service = service;
            _model = model;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<FocusInEvent>(OnFocusIn);
            target.RegisterCallback<FocusOutEvent>(OnFocusOut);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.UnregisterCallback<FocusInEvent>(OnFocusIn);
            target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnPointerEnter(PointerEnterEvent evt) =>
            _service.RequestShow(target, _model, false);

        private void OnPointerLeave(PointerLeaveEvent evt) =>
            _service.RequestHide(target);

        private void OnFocusIn(FocusInEvent evt) =>
            _service.RequestShow(target, _model, true);

        private void OnFocusOut(FocusOutEvent evt) =>
            _service.RequestHide(target);

        private void OnDetach(DetachFromPanelEvent evt) =>
            _service.HideIfAnchored(target);
    }

    private sealed class SemanticLinkManipulator : Manipulator
    {
        private readonly RuntimeTooltipService _service;
        private readonly SemanticTextBinding _binding;

        public SemanticLinkManipulator(
            RuntimeTooltipService service, SemanticTextBinding binding)
        {
            _service = service;
            _binding = binding;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerOverLinkTagEvent>(OnPointerOver);
            target.RegisterCallback<PointerOutLinkTagEvent>(OnPointerOut);
            target.RegisterCallback<PointerUpLinkTagEvent>(OnPointerUp);
            target.RegisterCallback<FocusInEvent>(OnFocusIn);
            target.RegisterCallback<FocusOutEvent>(OnFocusOut);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerOverLinkTagEvent>(OnPointerOver);
            target.UnregisterCallback<PointerOutLinkTagEvent>(OnPointerOut);
            target.UnregisterCallback<PointerUpLinkTagEvent>(OnPointerUp);
            target.UnregisterCallback<FocusInEvent>(OnFocusIn);
            target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnPointerOver(PointerOverLinkTagEvent evt)
        {
            if (!_binding.TryToken(evt.linkID, out SemanticTextToken token)) return;
            _service.RequestShow(
                target, () => token.Tooltip(_binding.Context), false);
        }

        private void OnPointerOut(PointerOutLinkTagEvent evt) =>
            _service.RequestHide(target);

        private void OnPointerUp(PointerUpLinkTagEvent evt)
        {
            if (!_binding.TryToken(evt.linkID, out SemanticTextToken token)) return;
            _service.RequestShow(
                target, () => token.Tooltip(_binding.Context), true);
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (_binding.Tokens.Count == 0) return;
            SemanticTextToken token = _binding.Tokens[0];
            _service.RequestShow(
                target, () => token.Tooltip(_binding.Context), true);
        }

        private void OnFocusOut(FocusOutEvent evt) =>
            _service.RequestHide(target);

        private void OnDetach(DetachFromPanelEvent evt) =>
            _service.HideIfAnchored(target);
    }

    private static readonly string[] KindClasses =
    {
        "runtime-tooltip--general",
        "runtime-tooltip--keyword",
        "runtime-tooltip--equipment",
        "runtime-tooltip--disabledreason",
    };

    private readonly VisualElement _host;
    private readonly HubPresentationConfig _config;
    private readonly VisualElement _root;
    private readonly WarbandGlyph _glyph;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _domain;
    private readonly Label _body;
    private readonly VisualElement _facts;
    private readonly Label _context;
    private readonly Label _footer;

    private VisualElement _anchor;
    private int _generation;
    private float _lastHideTime = -100f;
    private bool _reducedMotion;
    private bool _disposed;

    public RuntimeTooltipService(VisualElement host, HubPresentationConfig config)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _config = config ?? HubPresentationConfig.Load();
        _root = new VisualElement { name = "runtime-tooltip-layer" };
        _root.AddToClassList("runtime-tooltip");
        _root.pickingMode = PickingMode.Ignore;

        var heading = new VisualElement();
        heading.AddToClassList("runtime-tooltip__heading");
        _glyph = new WarbandGlyph();
        _glyph.AddToClassList("runtime-tooltip__glyph");
        var headingCopy = new VisualElement();
        headingCopy.AddToClassList("runtime-tooltip__heading-copy");
        _eyebrow = Label("runtime-tooltip__eyebrow");
        _title = Label("runtime-tooltip__title");
        headingCopy.Add(_eyebrow);
        headingCopy.Add(_title);
        heading.Add(_glyph);
        heading.Add(headingCopy);
        _root.Add(heading);

        _domain = Label("runtime-tooltip__domain");
        _body = Label("runtime-tooltip__body");
        _body.enableRichText = true;
        _facts = new VisualElement();
        _facts.AddToClassList("runtime-tooltip__facts");
        _context = Label("runtime-tooltip__context");
        _footer = Label("runtime-tooltip__footer");
        _root.Add(_domain);
        _root.Add(_body);
        _root.Add(_facts);
        _root.Add(_context);
        _root.Add(_footer);
        _host.Add(_root);
        SetDisplayed(_root, false);
    }

    public void Attach(VisualElement anchor, Func<RuntimeTooltipModel> model)
    {
        if (_disposed || anchor == null || model == null) return;
        anchor.AddManipulator(new AnchorManipulator(this, model));
    }

    public void AttachSemantic(Label anchor, SemanticTextBinding binding)
    {
        if (_disposed || anchor == null || binding == null || !binding.HasTokens) return;
        anchor.AddManipulator(new SemanticLinkManipulator(this, binding));
    }

    public void SetReducedMotion(bool reducedMotion)
    {
        _reducedMotion = reducedMotion;
        _root.EnableInClassList("motion--reduced", reducedMotion);
    }

    public void Hide()
    {
        bool wasVisible = _anchor != null &&
                          _root.resolvedStyle.display != DisplayStyle.None;
        string targetId = _anchor?.name ?? "";
        _generation++;
        _anchor = null;
        _root.RemoveFromClassList("runtime-tooltip--visible");
        SetDisplayed(_root, false);
        _lastHideTime = Time.unscaledTime;
        if (wasVisible)
            UiPolishSignals.Emit(UiPolishSignals.Cue.TooltipDismiss,
                targetId: targetId, tone: UiFeedbackTone.Preview);
    }

    private void RequestShow(VisualElement anchor, Func<RuntimeTooltipModel> model,
                             bool fromFocus)
    {
        if (_disposed || anchor?.panel == null) return;
        int generation = ++_generation;
        int delay = fromFocus
            ? _config.tooltip.focusDelayMs
            : _config.tooltip.pointerDelayMs;
        if (Time.unscaledTime - _lastHideTime < 0.45f)
            delay = Mathf.Min(delay, _config.tooltip.reshowDelayMs);
        _root.schedule.Execute(() =>
        {
            if (_disposed || generation != _generation || anchor.panel == null) return;
            RuntimeTooltipModel next = model();
            if (next == null || string.IsNullOrWhiteSpace(next.Title)) return;
            Reveal(next, anchor);
        }).StartingIn(delay);
    }

    private void RequestHide(VisualElement anchor)
    {
        if (_disposed || (_anchor != null && _anchor != anchor)) return;
        int generation = ++_generation;
        int delay = _reducedMotion ? 0 : _config.tooltip.closeDelayMs;
        _root.schedule.Execute(() =>
        {
            if (_disposed || generation != _generation) return;
            _root.RemoveFromClassList("runtime-tooltip--visible");
            int dismiss = _reducedMotion ? 0 : _config.tooltip.dismissMs;
            _root.schedule.Execute(() =>
            {
                if (_disposed || generation != _generation) return;
                Hide();
            }).StartingIn(dismiss);
        }).StartingIn(delay);
    }

    private void HideIfAnchored(VisualElement anchor)
    {
        if (_anchor == anchor) Hide();
    }

    private void Reveal(RuntimeTooltipModel model, VisualElement anchor)
    {
        _anchor = anchor;
        foreach (string className in KindClasses)
            _root.RemoveFromClassList(className);
        _root.AddToClassList("runtime-tooltip--" +
            model.Kind.ToString().ToLowerInvariant());
        MechanicPresentation.Apply(_root, model.Family);
        MechanicDefinition definition = MechanicPresentation.Definition(model.Family);
        _glyph.Set(model.Glyph == UiGlyphId.Unknown ? definition.Glyph : model.Glyph);
        _glyph.SetColor(definition.Color);
        _eyebrow.text = model.Eyebrow;
        _title.text = model.Title;
        _domain.text = model.Domain;
        MechanicPresentation.BindInline(_body, model.Body);
        _context.text = model.Context;
        _footer.text = model.Footer;
        SetDisplayed(_domain, !string.IsNullOrWhiteSpace(model.Domain));
        SetDisplayed(_body, !string.IsNullOrWhiteSpace(model.Body));
        SetDisplayed(_context, !string.IsNullOrWhiteSpace(model.Context));
        SetDisplayed(_footer, !string.IsNullOrWhiteSpace(model.Footer));

        _facts.Clear();
        foreach (StatChipModel fact in model.Facts)
        {
            var tile = new MechanicStatTile(
                "runtime-tooltip-fact", "runtime-tooltip-fact");
            tile.Bind(fact);
            tile.tooltip = "";
            _facts.Add(tile);
        }
        SetDisplayed(_facts, _facts.childCount > 0);
        _root.style.maxWidth = model.Kind == RuntimeTooltipKind.Equipment
            ? _config.tooltip.equipmentMaxWidthPx
            : _config.tooltip.maxWidthPx;
        var reveal = new TimeValue(
            _reducedMotion ? 0 : _config.tooltip.revealMs, TimeUnit.Millisecond);
        _root.style.transitionDuration = new List<TimeValue> { reveal, reveal };
        SetDisplayed(_root, true);
        _root.BringToFront();
        _root.RemoveFromClassList("runtime-tooltip--visible");
        _root.schedule.Execute(() =>
        {
            if (_anchor != anchor || anchor.panel == null) return;
            Position(anchor);
            _root.AddToClassList("runtime-tooltip--visible");
            // UI Toolkit resolves wrapped copy and fact tiles after the first display pass.
            // Re-clamp once with the final geometry so tall equipment cards never escape a
            // screen edge when their pre-layout height was only the fallback estimate.
            _root.schedule.Execute(() =>
            {
                if (_anchor == anchor && anchor.panel != null) Position(anchor);
            });
            UiPolishSignals.Emit(UiPolishSignals.Cue.TooltipReveal,
                targetId: anchor.name, tone: UiFeedbackTone.Preview);
        });
    }

    private void Position(VisualElement anchor)
    {
        Rect host = _host.worldBound;
        Rect target = anchor.worldBound;
        float width = SafeSize(_root.resolvedStyle.width, _config.tooltip.maxWidthPx);
        float height = SafeSize(_root.resolvedStyle.height, 160f);
        float margin = _config.tooltip.safeMarginPx;
        float offset = _config.tooltip.offsetPx;

        float left = target.center.x - host.xMin - width * 0.5f;
        float top = target.yMin - host.yMin - height - offset;
        if (top < margin)
            top = target.yMax - host.yMin + offset;
        if (top + height > host.height - margin)
        {
            left = target.xMax - host.xMin + offset;
            top = target.center.y - host.yMin - height * 0.5f;
            if (left + width > host.width - margin)
                left = target.xMin - host.xMin - width - offset;
        }
        left = Mathf.Clamp(left, margin, Mathf.Max(margin, host.width - width - margin));
        top = Mathf.Clamp(top, margin, Mathf.Max(margin, host.height - height - margin));
        _root.style.left = left;
        _root.style.top = top;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _root.RemoveFromHierarchy();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void EditorShowKeyword(VisualElement anchor, string note, string context)
    {
        if (anchor == null) return;
        Reveal(RuntimeTooltipModel.Keyword(note, context), anchor);
    }

    public void EditorShow(VisualElement anchor, RuntimeTooltipModel model)
    {
        if (anchor == null || model == null) return;
        Reveal(model, anchor);
    }

    public string EditorResolvedLayoutReport()
    {
        var report = new UiLayoutReport("Runtime tooltip");
        if (_root.resolvedStyle.display == DisplayStyle.None) return report.ToString();
        UiLayoutContract.RequireResolved(report, _root, "tooltip");
        UiLayoutContract.RequireInside(report, _root, _host, "tooltip");
        UiLayoutContract.RequireNoScrollView(report, _root, "tooltip");
        UiLayoutContract.RequireMinimumFont(report, _root, "runtime-tooltip__body", 14f);
        UiLayoutContract.RequireWrappedTextFits(report, _root, "runtime-tooltip__body");
        UiLayoutContract.RequireWrappedTextFits(report, _root, "runtime-tooltip__title");
        UiLayoutContract.RequireWrappedTextFits(report, _root, "runtime-tooltip__footer");
        return report.ToString();
    }
#endif

    private static Label Label(string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    private static float SafeSize(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
