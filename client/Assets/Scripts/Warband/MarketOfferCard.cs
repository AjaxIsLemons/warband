using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Dedicated Market scan card. It renders only comparison information; the attached Detail model
/// remains the source for the selection tray, hover rules, and dossier.
/// </summary>
internal sealed class MarketOfferCard
{
    private static readonly string[] KindClasses =
    {
        "recruit", "rankup", "weapon", "trinket", "inscription", "capacity", "sold",
    };

    private static VisualTreeAsset s_template;

    private readonly Action<string> _onSelected;
    private readonly Action<CardModel, VisualElement> _onHover;
    private readonly Action _onLeave;
    private readonly Label _classification;
    private readonly Label _title;
    private readonly VisualElement _artStage;
    private readonly VisualElement _artVectorHost;
    private readonly Label _artFallback;
    private readonly Label _qualifier;
    private readonly VisualElement _metrics;
    private readonly VisualElement _rule;
    private readonly Label _ruleLabel;
    private readonly Label _ruleName;
    private readonly Label _ruleCopy;
    private readonly VisualElement _commerce;
    private readonly Label _held;
    private readonly Label _economyState;
    private readonly HourstoneAmount _price;
    private readonly MarketOfferArtwork _artwork;

    private MarketOfferCardModel _model = new MarketOfferCardModel();
    private string _key = "";
    private bool _selectable;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private string _lastLayoutWarning = "";
#endif

    public VisualElement Root { get; }

    public MarketOfferCard(Action<string> onSelected,
                           Action<CardModel, VisualElement> onHover = null,
                           Action onLeave = null)
    {
        _onSelected = onSelected;
        _onHover = onHover;
        _onLeave = onLeave;

        if (s_template == null)
            s_template = Resources.Load<VisualTreeAsset>("UI/MarketOfferCard");
        if (s_template == null)
            throw new InvalidOperationException(
                "[Market] Resources/UI/MarketOfferCard.uxml is required.");

        var host = new VisualElement();
        s_template.CloneTree(host);
        Root = Required<VisualElement>(host, "card");
        Root.RemoveFromHierarchy();
        DecisionCardPresentation.ApplyProfile(Root, DecisionCardProfile.Stock);
        Root.usageHints |= UsageHints.DynamicTransform;

        _classification = Required<Label>(Root, "classification");
        _title = Required<Label>(Root, "title");
        _artStage = Required<VisualElement>(Root, "art-stage");
        _artVectorHost = Required<VisualElement>(Root, "art-vector");
        _artFallback = Required<Label>(Root, "art-fallback");
        _qualifier = Required<Label>(Root, "qualifier");
        _metrics = Required<VisualElement>(Root, "metrics");
        _rule = Required<VisualElement>(Root, "rule");
        _ruleLabel = Required<Label>(Root, "rule-label");
        _ruleName = Required<Label>(Root, "rule-name");
        _ruleCopy = Required<Label>(Root, "rule-copy");
        _commerce = Required<VisualElement>(Root, "commerce");
        _held = Required<Label>(Root, "held");
        _economyState = Required<Label>(Root, "economy-state");
        Label legacyPrice = Required<Label>(Root, "price");
        legacyPrice.RemoveFromHierarchy();
        _price = new HourstoneAmount(0, "market-offer-card__price");
        _commerce.Add(_price);

        _artwork = new MarketOfferArtwork();
        _artVectorHost.Add(_artwork);

        Root.RegisterCallback<ClickEvent>(OnClick);
        Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        Root.RegisterCallback<PointerEnterEvent>(_ => _onHover?.Invoke(_model.Detail, Root));
        Root.RegisterCallback<PointerLeaveEvent>(_ => _onLeave?.Invoke());
        Root.RegisterCallback<FocusInEvent>(_ => _onHover?.Invoke(_model.Detail, Root));
        Root.RegisterCallback<FocusOutEvent>(_ => _onLeave?.Invoke());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Root.RegisterCallback<GeometryChangedEvent>(_ => ValidateResolvedLayout());
#endif
    }

    public void Bind(MarketOfferCardModel model)
    {
        _model = model ?? new MarketOfferCardModel();
        _key = _model.Key;
        _selectable = !_model.Sold && !_model.Disabled && !string.IsNullOrEmpty(_key);

        Root.userData = _key;
        Root.SetEnabled(_selectable);
        Root.tooltip = _model.Sold
            ? "This offer has already been purchased."
            : _model.CurrencyCost >= 0
                ? $"{_model.Classification}. Select to inspect. Cost " +
                  $"{_model.CurrencyCost}; balance {_model.CurrencyBalance}."
                : $"{_model.Classification}. Select to inspect.";

        _classification.text = _model.Classification;
        _title.text = _model.Sold ? "SOLD" : _model.Title;
        _artFallback.text = _model.ArtworkFallback;
        _qualifier.text = _model.Qualifier;
        _ruleLabel.text = _model.RuleLabel;
        _ruleName.text = _model.RuleName;
        MechanicPresentation.BindInline(_ruleCopy, _model.ExactRule);
        _held.text = _model.Frozen ? "HELD" : "";
        _economyState.text = _model.EconomyState;
        _price.Bind(_model.CurrencyCost);

        string kind = KindName(_model.Kind);
        foreach (string value in KindClasses)
            Root.EnableInClassList("market-offer-card--" + value, value == kind);
        Root.EnableInClassList("market-offer-card--selected", _model.Selected);
        Root.EnableInClassList("market-offer-card--affordable",
            _model.Affordable && !_model.Sold);
        Root.EnableInClassList("market-offer-card--unaffordable", false);
        Root.EnableInClassList("market-offer-card--frozen", _model.Frozen);
        Root.EnableInClassList("market-offer-card--sold", _model.Sold);
        WarbandCard.SetAccent(Root, _model.Accent);

        Texture2D texture = string.IsNullOrEmpty(_model.ArtworkResource)
            ? null
            : Resources.Load<Texture2D>(_model.ArtworkResource);
        _artStage.style.backgroundImage = texture == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(texture));
        bool showVector = texture == null;
        SetDisplayed(_artVectorHost, showVector);
        SetDisplayed(_artFallback, showVector &&
            (_model.Kind == MarketOfferKind.Recruit ||
             _model.Kind == MarketOfferKind.RankUp));
        // Qualifiers such as Crit percentages belong to the selected Detail stage. Keeping the
        // model value preserves that information for the dossier without adding a second scan
        // number to compact stock.
        SetDisplayed(_qualifier, false);
        SetDisplayed(_rule, !_model.Sold && !string.IsNullOrEmpty(_model.ExactRule));
        SetDisplayed(_held, _model.Frozen);
        SetDisplayed(_price, _model.CurrencyCost >= 0);
        _artwork.SetKind(_model.Kind);

        SyncMetrics(_model.Metrics);
        MarketOfferPresentationContract.ValidateOrWarn(_model);
    }

    private void SyncMetrics(IReadOnlyList<StatChipModel> models)
    {
        int count = Mathf.Min(4, models?.Count ?? 0);
        while (_metrics.childCount > count) _metrics.RemoveAt(_metrics.childCount - 1);
        while (_metrics.childCount < count)
            _metrics.Add(new MechanicStatTile(
                "market-offer-metric", "market-offer-metric"));

        for (int i = 0; i < count; i++)
        {
            StatChipModel model = models[i];
            ((MechanicStatTile)_metrics.ElementAt(i)).Bind(model);
        }
        SetDisplayed(_metrics, count > 0);
    }

    private void OnClick(ClickEvent evt)
    {
        if (_selectable) _onSelected?.Invoke(_key);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
        OnClick(null);
        evt.StopPropagation();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ValidateResolvedLayout()
    {
        if (Root.panel == null || _model.Sold || Root.resolvedStyle.display == DisplayStyle.None)
            return;
        // Stock profile deliberately removes the entire mechanics/read region. Its layout
        // contract is enforced by ManagementView against the visible identity + commerce dock;
        // measuring a hidden rule label here reports intentional progressive disclosure as clip.
        if (_rule.parent != null &&
            _rule.parent.resolvedStyle.display == DisplayStyle.None)
        {
            _lastLayoutWarning = "";
            return;
        }

        const float epsilon = 0.75f;
        bool overlap = _rule.worldBound.yMax > _commerce.worldBound.yMin + epsilon;
        bool titleEscapes = _title.worldBound.xMin < Root.worldBound.xMin - epsilon ||
                            _title.worldBound.xMax > Root.worldBound.xMax + epsilon;
        Vector2 desiredCopy = _ruleCopy.MeasureTextSize(
            _ruleCopy.text, _ruleCopy.contentRect.width,
            VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
        float visibleCopyHeight = Mathf.Max(0f,
            _rule.worldBound.yMax - _ruleCopy.worldBound.yMin);
        bool copyClips = desiredCopy.y > visibleCopyHeight + epsilon;
        if (!overlap && !titleEscapes && !copyClips)
        {
            _lastLayoutWarning = "";
            return;
        }

        string signature =
            $"{_key}|{Mathf.RoundToInt(Root.resolvedStyle.width)}x" +
            $"{Mathf.RoundToInt(Root.resolvedStyle.height)}|{overlap}|{titleEscapes}|" +
            $"{copyClips}";
        if (string.Equals(_lastLayoutWarning, signature, StringComparison.Ordinal)) return;
        _lastLayoutWarning = signature;
        Debug.LogError(
            $"[Market Card Layout] '{_model.Title}' violated its layout contract at " +
            $"{Root.resolvedStyle.width:0}×{Root.resolvedStyle.height:0}. " +
            $"rule/dock overlap={overlap}, title escape={titleEscapes}, " +
            $"rule copy clipped={copyClips}.");
    }
#endif

    private static string KindName(MarketOfferKind kind) =>
        kind.ToString().ToLowerInvariant();

    private static string MetricSemantic(string label) =>
        (label ?? "").ToUpperInvariant() switch
        {
            "HP" => "hp",
            "ATK" => "attack",
            "DMG" => "attack",
            "HEAL" => "heal",
            "REACH" => "reach",
            "SPEED" => "speed",
            "CADENCE" => "speed",
            "MANA/SWING" => "mana",
            "MANA/HIT" => "mana",
            "MANA" => "mana",
            "RANK" => "rank",
            "CHOICE" => "choice",
            "CLEAVE" => "cleave",
            "DURATION" => "term",
            "SCOPE" => "scope",
            "TERM" => "term",
            "FIELD" => "field",
            "CAP" => "cap",
            _ => "",
        };

    private static string MetricLabel(string label) =>
        (label ?? "").ToUpperInvariant() switch
        {
            "ATK" => "DAMAGE",
            "SPEED" => "CADENCE",
            _ => (label ?? "").ToUpperInvariant(),
        };

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        T element = root.Q<T>(name);
        if (element == null)
            throw new InvalidOperationException($"[Market] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}

/// <summary>Code-native placeholder art with one consistent cut-iron drawing language.</summary>
internal sealed class MarketOfferArtwork : VisualElement
{
    private static readonly Color Bone = new Color32(213, 221, 230, 255);
    private static readonly Color Sand = new Color32(217, 164, 58, 255);
    private static readonly Color Blue = new Color32(104, 165, 231, 255);
    private MarketOfferKind _kind;

    public MarketOfferArtwork()
    {
        AddToClassList("market-offer-artwork");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    public void SetKind(MarketOfferKind kind)
    {
        if (_kind == kind) return;
        _kind = kind;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        Painter2D painter = context.painter2D;
        float unit = Mathf.Min(rect.width, rect.height);
        float radius = unit * 0.31f;
        Vector2 center = rect.center;
        painter.lineWidth = Mathf.Max(2f, unit * 0.035f);
        painter.strokeColor = _kind == MarketOfferKind.Inscription ||
                              _kind == MarketOfferKind.Capacity ? Sand : Bone;

        switch (_kind)
        {
            case MarketOfferKind.Recruit:
            case MarketOfferKind.RankUp:
                Poly(painter, center + new Vector2(0f, -radius * 0.48f),
                    radius * 0.34f, 8, 22.5f);
                Path(painter,
                    center + new Vector2(-radius * 0.68f, radius * 0.86f),
                    center + new Vector2(-radius * 0.52f, radius * 0.12f),
                    center + new Vector2(0f, -radius * 0.02f),
                    center + new Vector2(radius * 0.52f, radius * 0.12f),
                    center + new Vector2(radius * 0.68f, radius * 0.86f));
                if (_kind == MarketOfferKind.RankUp)
                {
                    painter.strokeColor = Sand;
                    Path(painter,
                        center + new Vector2(-radius * 0.46f, radius * 0.70f),
                        center + new Vector2(0f, radius * 0.40f),
                        center + new Vector2(radius * 0.46f, radius * 0.70f));
                    Path(painter,
                        center + new Vector2(-radius * 0.46f, radius * 1.04f),
                        center + new Vector2(0f, radius * 0.74f),
                        center + new Vector2(radius * 0.46f, radius * 1.04f));
                }
                break;
            case MarketOfferKind.Weapon:
                Path(painter,
                    center + new Vector2(-radius * 0.72f, radius * 0.88f),
                    center + new Vector2(radius * 0.48f, -radius * 0.68f),
                    center + new Vector2(radius * 0.78f, -radius * 0.90f),
                    center + new Vector2(radius * 0.62f, -radius * 0.48f));
                Line(painter,
                    center + new Vector2(-radius * 0.38f, radius * 0.36f),
                    center + new Vector2(radius * 0.24f, radius * 0.84f));
                Diamond(painter, center + new Vector2(-radius * 0.74f, radius * 0.90f),
                    radius * 0.18f);
                break;
            case MarketOfferKind.Trinket:
                Poly(painter, center, radius, 6, 30f);
                Poly(painter, center, radius * 0.58f, 4, 45f);
                Line(painter, center + new Vector2(0f, -radius),
                    center + new Vector2(0f, -radius * 1.34f));
                ArcPoly(painter, center + new Vector2(0f, -radius * 1.48f),
                    radius * 0.28f, 10);
                break;
            case MarketOfferKind.Inscription:
                Poly(painter, center, radius * 1.10f, 12, 15f);
                Path(painter,
                    center + new Vector2(-radius * 0.48f, -radius * 0.72f),
                    center + new Vector2(radius * 0.48f, -radius * 0.72f),
                    center + new Vector2(-radius * 0.38f, radius * 0.72f),
                    center + new Vector2(radius * 0.38f, radius * 0.72f),
                    center + new Vector2(-radius * 0.48f, -radius * 0.72f));
                Line(painter, center + new Vector2(-radius * 0.58f, 0f),
                    center + new Vector2(radius * 0.58f, 0f));
                break;
            case MarketOfferKind.Capacity:
                painter.strokeColor = Blue;
                Poly(painter, center, radius * 1.08f, 6, 30f);
                painter.strokeColor = Sand;
                Line(painter, center + new Vector2(-radius * 0.48f, 0f),
                    center + new Vector2(radius * 0.48f, 0f));
                Line(painter, center + new Vector2(0f, -radius * 0.48f),
                    center + new Vector2(0f, radius * 0.48f));
                break;
            case MarketOfferKind.Sold:
                painter.strokeColor = new Color32(122, 133, 149, 255);
                Poly(painter, center, radius, 4, 45f);
                Line(painter, center + new Vector2(-radius * 0.74f, radius * 0.74f),
                    center + new Vector2(radius * 0.74f, -radius * 0.74f));
                break;
        }
    }

    private static void Diamond(Painter2D painter, Vector2 center, float radius) =>
        Poly(painter, center, radius, 4, 45f);

    private static void ArcPoly(Painter2D painter, Vector2 center, float radius, int sides) =>
        Poly(painter, center, radius, sides, 0f);

    private static void Poly(Painter2D painter, Vector2 center, float radius, int sides,
                             float rotationDegrees)
    {
        painter.BeginPath();
        for (int i = 0; i <= sides; i++)
        {
            float angle = (rotationDegrees + i * 360f / sides) * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }
        painter.Stroke();
    }

    private static void Line(Painter2D painter, Vector2 from, Vector2 to)
    {
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }

    private static void Path(Painter2D painter, params Vector2[] points)
    {
        if (points == null || points.Length < 2) return;
        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
        painter.Stroke();
    }
}

internal static class MarketOfferPresentationContract
{
    private static readonly MarketOfferKind[] FixtureKinds =
    {
        MarketOfferKind.Recruit,
        MarketOfferKind.RankUp,
        MarketOfferKind.Weapon,
        MarketOfferKind.Trinket,
        MarketOfferKind.Inscription,
        MarketOfferKind.Capacity,
    };

    public static void Validate(IReadOnlyList<MarketOfferCardModel> models)
    {
        if (models == null)
            throw new InvalidOperationException("[Market Contract] Offer list is null.");
        for (int i = 0; i < models.Count; i++) Validate(models[i]);
    }

    public static void Validate(MarketOfferCardModel model)
    {
        if (model == null)
            throw new InvalidOperationException("[Market Contract] Offer is null.");
        Require(!string.IsNullOrWhiteSpace(model.Key), "Every offer needs a stable key.");
        Require(!string.IsNullOrWhiteSpace(model.Title), "Every offer needs a title.");
        Require(!string.IsNullOrWhiteSpace(model.Classification),
            $"'{model.Title}' needs a classification.");
        Require(model.Metrics != null && model.Metrics.Count <= 4,
            $"'{model.Title}' exceeds the four-metric scan budget.");
        var factIds = new HashSet<PresentationFactId>();
        if (model.Metrics != null)
            foreach (StatChipModel metric in model.Metrics)
                if (metric.Id != PresentationFactId.Unknown)
                    Require(factIds.Add(metric.Id),
                        $"'{model.Title}' repeats the {metric.Id} scan fact.");
        if (model.Sold) return;
        Require(!string.IsNullOrWhiteSpace(model.RuleLabel),
            $"'{model.Title}' needs a mechanical rule label.");
        Require(!string.IsNullOrWhiteSpace(model.ExactRule),
            $"'{model.Title}' needs one exact rule.");
        Require(model.CurrencyCost >= 0 || !string.IsNullOrWhiteSpace(model.Price),
            $"'{model.Title}' needs a protected commerce state or typed currency price.");
        Require(model.Detail != null && model.Detail.Key == model.Key,
            $"'{model.Title}' must reference its matching dossier card.");
        Require(model.ExactRule == model.Detail.AbilitySummary ||
                model.ExactRule == model.Detail.PassiveSummary,
            $"'{model.Title}' scan rule must match a dossier mechanical rule.");
        if (model.Kind == MarketOfferKind.RankUp)
        {
            Require(Has(model, PresentationFactId.Rank),
                $"'{model.Title}' rank offer must expose the guaranteed rank change.");
            Require(Has(model, PresentationFactId.ChoiceCount),
                $"'{model.Title}' rank offer must expose its pending choice.");
        }
        if (model.Kind == MarketOfferKind.Weapon)
            Require(Has(model, PresentationFactId.ManaPerSwing),
                $"'{model.Title}' weapon must expose Mana per swing.");
        if (model.Kind == MarketOfferKind.Inscription)
        {
            Require(Has(model, PresentationFactId.Scope),
                $"'{model.Title}' inscription must expose its scope.");
            Require(Has(model, PresentationFactId.Duration),
                $"'{model.Title}' inscription must expose its duration.");
        }
        if (model.Kind == MarketOfferKind.Capacity)
            Require(Has(model, PresentationFactId.FieldCapacity),
                $"'{model.Title}' capacity offer must expose the field-size change.");
    }

    /// <summary>
    /// Covers every dedicated presentation kind plus important commerce states without depending
    /// on whichever offers a seeded run happens to roll.
    /// </summary>
    public static void ValidateFixtures()
    {
        for (int i = 0; i < FixtureKinds.Length; i++)
        {
            MarketOfferKind kind = FixtureKinds[i];
            string key = "fixture:" + kind.ToString().ToLowerInvariant();
            string exactRule = kind == MarketOfferKind.Inscription
                ? "All allies gain +1 Reach this run."
                : "Deal 12 damage to the nearest enemy.";
            var detail = new CardModel
            {
                Key = key,
                Title = kind + " Fixture",
                AbilitySummary = exactRule,
            };
            var model = new MarketOfferCardModel
            {
                Key = key,
                Kind = kind,
                Classification = kind.ToString().ToUpperInvariant(),
                Title = detail.Title,
                RuleLabel = kind == MarketOfferKind.Inscription ? "WARBAND RULE" : "ACTIVE",
                RuleName = "Contract Fixture",
                ExactRule = exactRule,
                CurrencyCost = 9,
                CurrencyBalance = i % 2 == 0 ? 12 : 7,
                EconomyState = "",
                Affordable = i % 2 == 0,
                Frozen = i == 1,
                Selected = i == 2,
                Detail = detail,
                Metrics = FixtureMetrics(kind),
            };
            Validate(model);
        }

        Validate(new MarketOfferCardModel
        {
            Key = "fixture:sold",
            Kind = MarketOfferKind.Sold,
            Classification = "SOLD OFFER",
            Title = "Sold Fixture",
            Sold = true,
            Disabled = true,
        });
    }

    public static void ValidateOrWarn(MarketOfferCardModel model)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try { Validate(model); }
        catch (Exception ex) { Debug.LogError(ex.Message); }
#endif
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Market Contract] " + message);
    }

    private static bool Has(MarketOfferCardModel model, PresentationFactId id) =>
        model.Metrics != null && model.Metrics.Exists(metric => metric.Id == id);

    private static List<StatChipModel> FixtureMetrics(MarketOfferKind kind)
    {
        switch (kind)
        {
            case MarketOfferKind.RankUp:
                return new List<StatChipModel>
                {
                    new StatChipModel("RANK", "B → A", id: PresentationFactId.Rank),
                    new StatChipModel("HP", "+20", id: PresentationFactId.Hp),
                    new StatChipModel("POWER", "+3", id: PresentationFactId.BasicPower),
                    new StatChipModel("CHOICE", "1 OF 2", id: PresentationFactId.ChoiceCount),
                };
            case MarketOfferKind.Weapon:
                return new List<StatChipModel>
                {
                    new StatChipModel("POWER", "12", id: PresentationFactId.BasicPower),
                    new StatChipModel("REACH", "2", id: PresentationFactId.Reach),
                    new StatChipModel("CADENCE", "1.0", id: PresentationFactId.Cadence),
                    new StatChipModel("MANA/HIT", "1", id: PresentationFactId.ManaPerSwing),
                };
            case MarketOfferKind.Inscription:
                return new List<StatChipModel>
                {
                    new StatChipModel("SCOPE", "WARBAND", id: PresentationFactId.Scope),
                    new StatChipModel("DURATION", "THIS RUN", id: PresentationFactId.Duration),
                    new StatChipModel("CHOICE", "BOUND", id: PresentationFactId.ChoiceCount),
                };
            case MarketOfferKind.Capacity:
                return new List<StatChipModel>
                {
                    new StatChipModel("FIELD", "3 → 4", id: PresentationFactId.FieldCapacity),
                    new StatChipModel("GAIN", "+1", id: PresentationFactId.RankDelta),
                };
            default:
                return new List<StatChipModel>
                {
                    new StatChipModel("HP", "120", id: PresentationFactId.Hp),
                    new StatChipModel("POWER", "12", id: PresentationFactId.BasicPower),
                    new StatChipModel("REACH", "2", id: PresentationFactId.Reach),
                    new StatChipModel("CADENCE", "1.0", id: PresentationFactId.Cadence),
                };
        }
    }
}
