using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Shipping panel profile shared by every player-facing screen-space document. Height owns the
/// scale so the game's horizontal decision band cannot become taller than the viewport.
/// </summary>
internal static class UiPanelProfile
{
    public static readonly Vector2Int ReferenceResolution = new Vector2Int(1600, 900);

    public static void ConfigureShipping(PanelSettings settings, float sortingOrder)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = ReferenceResolution;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 1f;
        settings.sortingOrder = sortingOrder;
    }
}

internal static class UiStyleCatalog
{
    private static readonly string[] ShippingSheets =
    {
        "UI/UiFoundationTokens",
        "UI/SkirmishStyles",
        "UI/RunShellStyles",
        "UI/PlanningWorkspaceStyles",
        "UI/RunFlowStyles",
        "UI/HubStyles",
        "UI/LastHourTokens",
        "UI/HallPhysicalStyles",
        "UI/MarketOfferCardStyles",
        "UI/WarbandBarStyles",
        "UI/MechanicPresentationStyles",
        "UI/WorkbenchStyles",
        "UI/CombatRecapStyles",
    };

    public static void AttachShipping(VisualElement root, string owner)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        foreach (string resource in ShippingSheets)
        {
            var sheet = Resources.Load<StyleSheet>(resource);
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning($"[{owner}] stylesheet not found: {resource}");
        }
    }
}

internal enum UiFormFactor
{
    Desktop,
    Tablet,
    Phone,
}

internal enum UiInputModality
{
    Pointer,
    Touch,
    Navigation,
}

internal readonly struct UiSafeInsets
{
    public readonly float Left;
    public readonly float Top;
    public readonly float Right;
    public readonly float Bottom;

    public UiSafeInsets(float left, float top, float right, float bottom)
    {
        Left = Mathf.Max(0f, left);
        Top = Mathf.Max(0f, top);
        Right = Mathf.Max(0f, right);
        Bottom = Mathf.Max(0f, bottom);
    }
}

internal readonly struct UiScreenMetrics
{
    public readonly Vector2Int Size;
    public readonly Rect SafeArea;
    public readonly float Dpi;
    public readonly DeviceType DeviceType;

    public UiScreenMetrics(Vector2Int size, Rect safeArea, float dpi, DeviceType deviceType)
    {
        Size = size;
        SafeArea = safeArea;
        Dpi = dpi;
        DeviceType = deviceType;
    }
}

internal interface IUiScreenMetricsProvider
{
    UiScreenMetrics Read();
}

internal sealed class UnityUiScreenMetricsProvider : IUiScreenMetricsProvider
{
    public UiScreenMetrics Read() => new UiScreenMetrics(
        new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)),
        Screen.safeArea,
        Screen.dpi,
        SystemInfo.deviceType);
}

internal readonly struct UiEnvironmentSnapshot
{
    public readonly Vector2 PanelSize;
    public readonly float Aspect;
    public readonly float PixelsPerPoint;
    public readonly UiFormFactor FormFactor;
    public readonly UiInputModality InputModality;
    public readonly UiSafeInsets SafeInsets;
    public readonly bool Narrow;
    public readonly bool Short;
    public readonly bool Ultrawide;
    public readonly bool CoarseInteraction;
    public readonly bool PortraitHandheld;
    public readonly bool ReducedMotion;

    public UiEnvironmentSnapshot(
        Vector2 panelSize,
        float pixelsPerPoint,
        UiFormFactor formFactor,
        UiInputModality inputModality,
        UiSafeInsets safeInsets,
        bool reducedMotion)
    {
        PanelSize = panelSize;
        Aspect = panelSize.x / Mathf.Max(1f, panelSize.y);
        PixelsPerPoint = Mathf.Max(0.01f, pixelsPerPoint);
        FormFactor = formFactor;
        InputModality = inputModality;
        SafeInsets = safeInsets;
        Narrow = panelSize.x < 1360f;
        Short = panelSize.y < 820f;
        Ultrawide = Aspect > 2.05f;
        CoarseInteraction = formFactor != UiFormFactor.Desktop;
        PortraitHandheld = formFactor != UiFormFactor.Desktop && panelSize.y > panelSize.x;
        ReducedMotion = reducedMotion;
    }
}

/// <summary>
/// The sole responsive classifier for the shipping UI document. Layout, form factor, input
/// modality, safe area, and motion are deliberately independent axes.
/// </summary>
internal sealed class UiEnvironment : IDisposable
{
    private readonly VisualElement _root;
    private readonly VisualElement _safeFrame;
    private readonly IUiScreenMetricsProvider _metricsProvider;
    private readonly EventCallback<GeometryChangedEvent> _geometryChanged;
    private readonly EventCallback<PointerDownEvent> _pointerDown;
    private readonly EventCallback<KeyDownEvent> _keyDown;

    private bool _reducedMotion;
    private bool _forcePhone;
    private UiInputModality _inputModality = UiInputModality.Pointer;
    private UiEnvironmentSnapshot _current;

    public UiEnvironmentSnapshot Current => _current;
    public event Action<UiEnvironmentSnapshot> Changed;

    public UiEnvironment(
        VisualElement root,
        VisualElement safeFrame,
        bool reducedMotion,
        IUiScreenMetricsProvider metricsProvider = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _safeFrame = safeFrame ?? throw new ArgumentNullException(nameof(safeFrame));
        _metricsProvider = metricsProvider ?? new UnityUiScreenMetricsProvider();
        _reducedMotion = reducedMotion;

        _geometryChanged = _ => Refresh();
        _pointerDown = OnPointerDown;
        _keyDown = OnKeyDown;
        _root.RegisterCallback(_geometryChanged);
        _root.RegisterCallback(_pointerDown, TrickleDown.TrickleDown);
        _root.RegisterCallback(_keyDown, TrickleDown.TrickleDown);
        Refresh();
    }

    public void Dispose()
    {
        _root.UnregisterCallback(_geometryChanged);
        _root.UnregisterCallback(_pointerDown, TrickleDown.TrickleDown);
        _root.UnregisterCallback(_keyDown, TrickleDown.TrickleDown);
    }

    public void SetReducedMotion(bool reducedMotion)
    {
        if (_reducedMotion == reducedMotion) return;
        _reducedMotion = reducedMotion;
        Refresh();
    }

    public void ForcePhoneForDebug(bool enabled)
    {
        if (_forcePhone == enabled) return;
        _forcePhone = enabled;
        Refresh();
    }

    public void Refresh()
    {
        float width = _root.resolvedStyle.width;
        float height = _root.resolvedStyle.height;
        UiScreenMetrics metrics = _metricsProvider.Read();
        if (!FinitePositive(width)) width = metrics.Size.x;
        if (!FinitePositive(height)) height = metrics.Size.y;

        UiFormFactor formFactor = ClassifyFormFactor(metrics);
        UiSafeInsets insets = ConvertSafeInsets(
            new Vector2(width, height), metrics.Size, metrics.SafeArea);
        float pixelsPerPoint = _root.panel?.scaledPixelsPerPoint ?? 1f;
        _current = new UiEnvironmentSnapshot(
            new Vector2(width, height),
            pixelsPerPoint,
            formFactor,
            _inputModality,
            insets,
            _reducedMotion);

        ApplyClasses(_current);
        ApplySafeArea(insets);
        Changed?.Invoke(_current);
    }

    internal static UiSafeInsets ConvertSafeInsets(
        Vector2 panelSize, Vector2Int screenSize, Rect safeArea)
    {
        float scaleX = panelSize.x / Mathf.Max(1f, screenSize.x);
        float scaleY = panelSize.y / Mathf.Max(1f, screenSize.y);
        return new UiSafeInsets(
            safeArea.xMin * scaleX,
            (screenSize.y - safeArea.yMax) * scaleY,
            (screenSize.x - safeArea.xMax) * scaleX,
            safeArea.yMin * scaleY);
    }

    private UiFormFactor ClassifyFormFactor(UiScreenMetrics metrics)
    {
        if (_forcePhone) return UiFormFactor.Phone;
        if (metrics.DeviceType != DeviceType.Handheld) return UiFormFactor.Desktop;
        if (metrics.Dpi <= 0f) return UiFormFactor.Phone;
        float diagonal = Mathf.Sqrt(
            metrics.Size.x * metrics.Size.x + metrics.Size.y * metrics.Size.y) / metrics.Dpi;
        return diagonal < 8f ? UiFormFactor.Phone : UiFormFactor.Tablet;
    }

    private void ApplyClasses(UiEnvironmentSnapshot value)
    {
        bool phone = value.FormFactor == UiFormFactor.Phone;
        bool tablet = value.FormFactor == UiFormFactor.Tablet;
        bool touch = value.InputModality == UiInputModality.Touch;
        bool navigation = value.InputModality == UiInputModality.Navigation;

        _root.EnableInClassList("layout--narrow", value.Narrow);
        _root.EnableInClassList("layout--short", value.Short || phone);
        _root.EnableInClassList("layout--ultrawide", value.Ultrawide);
        _root.EnableInClassList("layout--phone", phone);
        _root.EnableInClassList("layout--tablet", tablet);
        _root.EnableInClassList("layout--portrait-handheld", value.PortraitHandheld);
        _root.EnableInClassList("interaction--coarse", value.CoarseInteraction);
        _root.EnableInClassList("input--touch", touch);
        _root.EnableInClassList("input--pointer", !touch && !navigation);
        _root.EnableInClassList("input--navigation", navigation);
        _root.EnableInClassList("motion--reduced", value.ReducedMotion);

        // Compatibility alias while active screen styles migrate to independent axes.
        _root.EnableInClassList(
            "layout--compact",
            value.Narrow || value.Short || phone);
    }

    private void ApplySafeArea(UiSafeInsets value)
    {
        _safeFrame.style.left = value.Left;
        _safeFrame.style.top = value.Top;
        _safeFrame.style.right = value.Right;
        _safeFrame.style.bottom = value.Bottom;
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        UiInputModality next =
            string.Equals(
                evt.pointerType,
                UnityEngine.UIElements.PointerType.touch,
                StringComparison.OrdinalIgnoreCase)
                ? UiInputModality.Touch
                : UiInputModality.Pointer;
        if (next == _inputModality) return;
        _inputModality = next;
        Refresh();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (_inputModality == UiInputModality.Navigation) return;
        _inputModality = UiInputModality.Navigation;
        Refresh();
    }

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
