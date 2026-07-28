using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A small, reusable structural contract for fixed UI compositions. It deliberately validates
/// resolved geometry and text rather than screenshots: image review remains human-owned, while
/// clipping, overlap, accidental scrolling, and unreadably small contract text fail every time.
/// </summary>
internal sealed class UiLayoutReport
{
    private readonly List<string> _failures = new List<string>();

    public string Surface { get; }
    public IReadOnlyList<string> Failures => _failures;
    public bool Passed => _failures.Count == 0;

    public UiLayoutReport(string surface)
    {
        Surface = string.IsNullOrWhiteSpace(surface) ? "UI" : surface;
    }

    public void Fail(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) _failures.Add(message);
    }

    public override string ToString() => Passed
        ? $"{Surface}: PASS"
        : $"{Surface}: FAIL · {string.Join("; ", _failures)}";
}

internal static class UiLayoutContract
{
    public static void RequireResolved(
        UiLayoutReport report, VisualElement element, string label)
    {
        if (report == null) return;
        if (element == null)
        {
            report.Fail($"{label} is missing");
            return;
        }
        if (!IsFinitePositive(element.resolvedStyle.width) ||
            !IsFinitePositive(element.resolvedStyle.height))
            report.Fail($"{label} did not resolve");
    }

    public static void RequireInside(
        UiLayoutReport report, VisualElement element, VisualElement container,
        string label, float tolerance = 1f)
    {
        if (report == null || !IsVisible(element) || !IsVisible(container)) return;
        Rect item = element.worldBound;
        Rect bounds = container.worldBound;
        if (item.xMin < bounds.xMin - tolerance ||
            item.yMin < bounds.yMin - tolerance ||
            item.xMax > bounds.xMax + tolerance ||
            item.yMax > bounds.yMax + tolerance)
            report.Fail(
                $"{label} escapes {Name(container)} " +
                $"[{item.xMin:0.#},{item.yMin:0.#}–{item.xMax:0.#},{item.yMax:0.#}] " +
                $"vs [{bounds.xMin:0.#},{bounds.yMin:0.#}–" +
                $"{bounds.xMax:0.#},{bounds.yMax:0.#}]");
    }

    public static void RequireAbove(
        UiLayoutReport report, VisualElement upper, VisualElement lower,
        string label, float tolerance = 1f)
    {
        if (report == null || !IsVisible(upper) || !IsVisible(lower)) return;
        if (upper.worldBound.yMax > lower.worldBound.yMin + tolerance)
            report.Fail($"{label} overlap");
    }

    public static void RequireNoOverlap(
        UiLayoutReport report, VisualElement first, VisualElement second,
        string label, float tolerance = 1f)
    {
        if (report == null || !IsVisible(first) || !IsVisible(second)) return;
        Rect a = first.worldBound;
        Rect b = second.worldBound;
        bool overlaps =
            a.xMin < b.xMax - tolerance &&
            a.xMax > b.xMin + tolerance &&
            a.yMin < b.yMax - tolerance &&
            a.yMax > b.yMin + tolerance;
        if (overlaps) report.Fail($"{label} overlap");
    }

    public static void RequireNoScrollView(
        UiLayoutReport report, VisualElement root, string label)
    {
        if (report == null || root == null) return;
        if (root.Q<ScrollView>() != null) report.Fail($"{label} contains a ScrollView");
    }

    public static void RequireNonBlocking(
        UiLayoutReport report, VisualElement element, string label)
    {
        if (report == null) return;
        if (element == null)
        {
            report.Fail($"{label} is missing");
            return;
        }
        if (element.pickingMode != PickingMode.Ignore)
            report.Fail($"{label} consumes pointer input");
    }

    public static void RequireClassInside(
        UiLayoutReport report, VisualElement root, string className,
        VisualElement container, VisualElement boundary = null,
        float tolerance = 1f)
    {
        if (report == null || root == null || container == null) return;
        List<VisualElement> elements =
            root.Query<VisualElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            VisualElement element = elements[i];
            if (!IsVisible(element)) continue;
            RequireInside(report, element, container, $"{className} {i}", tolerance);
            if (IsVisible(boundary) &&
                element.worldBound.yMax > boundary.worldBound.yMin + tolerance)
                report.Fail($"{className} {i} runs behind {Name(boundary)}");
        }
    }

    public static void RequireWrappedTextFits(
        UiLayoutReport report, VisualElement root, string className,
        float tolerance = 1f)
    {
        if (report == null || root == null) return;
        List<TextElement> elements = root.Query<TextElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            TextElement element = elements[i];
            if (!IsVisible(element) || element.resolvedStyle.width <= 1f) continue;
            float width = Mathf.Max(1f, element.contentRect.width);
            Vector2 measured = element.MeasureTextSize(
                element.text, width, VisualElement.MeasureMode.Exactly,
                0f, VisualElement.MeasureMode.Undefined);
            if (measured.y > element.contentRect.height + tolerance)
                report.Fail(
                    $"{className} {i} clips wrapped text " +
                    $"({measured.y:0.#} > {element.contentRect.height:0.#})");
        }
    }

    public static void RequireMinimumFont(
        UiLayoutReport report, VisualElement root, string className, float minimum)
    {
        if (report == null || root == null) return;
        List<TextElement> elements = root.Query<TextElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            TextElement element = elements[i];
            if (!IsVisible(element)) continue;
            float size = element.resolvedStyle.fontSize;
            if (float.IsNaN(size) || float.IsInfinity(size) || size + 0.01f < minimum)
                report.Fail($"{className} {i} font is {size:0.#}px; minimum is {minimum:0.#}px");
        }
    }

    /// <summary>
    /// Checks the actual device-pixel result after PanelSettings scaling. Logical type tokens stop
    /// stylesheet drift; this guard catches a valid token rendered too small by a panel profile.
    /// </summary>
    public static void RequireMinimumRenderedFont(
        UiLayoutReport report, VisualElement root, string className, float minimum)
    {
        if (report == null || root == null) return;
        float pixelsPerPoint = root.panel?.scaledPixelsPerPoint ?? 1f;
        List<TextElement> elements = root.Query<TextElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            TextElement element = elements[i];
            if (!IsVisible(element)) continue;
            float logical = element.resolvedStyle.fontSize;
            float rendered = logical * pixelsPerPoint;
            if (float.IsNaN(rendered) || float.IsInfinity(rendered) ||
                rendered + 0.01f < minimum)
                report.Fail(
                    $"{className} {i} renders at {rendered:0.#}px " +
                    $"({logical:0.#} logical × {pixelsPerPoint:0.###}); " +
                    $"minimum is {minimum:0.#}px");
        }
    }

    public static void RequireSingleLineTextFits(
        UiLayoutReport report, VisualElement root, string className,
        float tolerance = 1f)
    {
        if (report == null || root == null) return;
        List<TextElement> elements = root.Query<TextElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            TextElement element = elements[i];
            if (!IsVisible(element) || element.resolvedStyle.width <= 1f) continue;
            Vector2 measured = element.MeasureTextSize(
                element.text, 0f, VisualElement.MeasureMode.Undefined,
                element.contentRect.height, VisualElement.MeasureMode.Exactly);
            if (measured.x > element.contentRect.width + tolerance)
                report.Fail(
                    $"{className} {i} clips single-line text " +
                    $"({measured.x:0.#} > {element.contentRect.width:0.#})");
        }
    }

    public static void RequireHidden(
        UiLayoutReport report, VisualElement element, string label)
    {
        if (report == null) return;
        if (element == null)
        {
            report.Fail($"{label} is missing");
            return;
        }
        if (IsVisible(element)) report.Fail($"{label} must be hidden");
    }

    public static void RequireVisibleChildCount(
        UiLayoutReport report, VisualElement element, int expected, string label)
    {
        if (report == null) return;
        if (element == null)
        {
            report.Fail($"{label} is missing");
            return;
        }
        int visible = 0;
        for (int i = 0; i < element.childCount; i++)
            if (IsVisible(element[i])) visible++;
        if (visible != expected)
            report.Fail($"{label} has {visible} visible children; expected {expected}");
    }

    public static void RequireMinimumHeight(
        UiLayoutReport report, VisualElement root, string className, float minimum)
    {
        if (report == null || root == null) return;
        List<VisualElement> elements =
            root.Query<VisualElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            VisualElement element = elements[i];
            if (!IsVisible(element)) continue;
            float height = element.resolvedStyle.height;
            if (float.IsNaN(height) || float.IsInfinity(height) ||
                height + 0.01f < minimum)
                report.Fail(
                    $"{className} {i} resolves to {height:0.#}px; " +
                    $"minimum is {minimum:0.#}px");
        }
    }

    public static void RequireMinimumWidth(
        UiLayoutReport report, VisualElement root, string className, float minimum)
    {
        if (report == null || root == null) return;
        List<VisualElement> elements =
            root.Query<VisualElement>(className: className).ToList();
        for (int i = 0; i < elements.Count; i++)
        {
            VisualElement element = elements[i];
            if (!IsVisible(element)) continue;
            float width = element.resolvedStyle.width;
            if (float.IsNaN(width) || float.IsInfinity(width) ||
                width + 0.01f < minimum)
                report.Fail(
                    $"{className} {i} resolves to {width:0.#}px; " +
                    $"minimum is {minimum:0.#}px");
        }
    }

    private static bool IsVisible(VisualElement element)
    {
        for (VisualElement current = element; current != null; current = current.parent)
            if (current.resolvedStyle.display == DisplayStyle.None ||
                current.resolvedStyle.visibility != Visibility.Visible)
                return false;
        return element != null;
    }

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 1f;

    private static string Name(VisualElement element) =>
        element == null
            ? "missing boundary"
            : string.IsNullOrWhiteSpace(element.name)
                ? element.GetType().Name
                : element.name;
}
