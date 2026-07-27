using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Crisp, code-native icon family for dense decision UI. Every mark shares one stroke language
/// and remains legible without relying on platform emoji fonts.
/// </summary>
internal sealed class WarbandGlyph : VisualElement
{
    private static readonly Color DefaultColor = new Color32(206, 218, 234, 255);
    private UiGlyphId _glyph;
    private Color _color = DefaultColor;

    public WarbandGlyph(UiGlyphId glyph = UiGlyphId.Unknown)
    {
        pickingMode = PickingMode.Ignore;
        AddToClassList("warband-glyph");
        generateVisualContent += Draw;
        Set(glyph);
    }

    public void Set(UiGlyphId glyph)
    {
        _glyph = glyph;
        name = "glyph-" + glyph.ToString().ToLowerInvariant();
        MarkDirtyRepaint();
    }

    public void SetColor(Color color)
    {
        _color = color;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        if (rect.width <= 1f || rect.height <= 1f) return;

        Painter2D painter = context.painter2D;
        painter.strokeColor = _color;
        painter.lineWidth = Mathf.Max(1.35f, Mathf.Min(rect.width, rect.height) * 0.085f);
        Vector2 c = rect.center;
        float r = Mathf.Min(rect.width, rect.height) * 0.34f;

        switch (_glyph)
        {
            case UiGlyphId.Health:
                Heart(painter, c, r);
                break;
            case UiGlyphId.Damage:
            case UiGlyphId.Bruiser:
                CrossedBlades(painter, c, r);
                break;
            case UiGlyphId.Heal:
            case UiGlyphId.Healer:
                Plus(painter, c, r);
                break;
            case UiGlyphId.Cadence:
                Poly(painter, c, r, 14, -90f);
                Line(painter, c, c + new Vector2(0f, -r * 0.60f));
                Line(painter, c, c + new Vector2(r * 0.46f, r * 0.20f));
                break;
            case UiGlyphId.Reach:
                Poly(painter, c, r, 6, 30f);
                Dot(painter, c, r * 0.12f);
                break;
            case UiGlyphId.Signature:
            case UiGlyphId.Burst:
                Star(painter, c, r);
                break;
            case UiGlyphId.Passive:
                Diamond(painter, c, r);
                Diamond(painter, c, r * 0.45f);
                break;
            case UiGlyphId.Tank:
            case UiGlyphId.Shield:
                Shield(painter, c, r);
                break;
            case UiGlyphId.Diver:
            case UiGlyphId.Leap:
                Leap(painter, c, r);
                break;
            case UiGlyphId.Sniper:
                Poly(painter, c, r, 14, -90f);
                Line(painter, c + new Vector2(-r, 0f), c + new Vector2(r, 0f));
                Line(painter, c + new Vector2(0f, -r), c + new Vector2(0f, r));
                Dot(painter, c, r * 0.13f);
                break;
            case UiGlyphId.Zoner:
            case UiGlyphId.Area:
                Poly(painter, c, r, 6, 30f);
                Poly(painter, c, r * 0.48f, 6, 30f);
                break;
            case UiGlyphId.Frontline:
                Line(painter, c + new Vector2(-r, -r * 0.72f),
                    c + new Vector2(-r, r * 0.72f));
                Line(painter, c + new Vector2(0f, -r),
                    c + new Vector2(0f, r));
                Line(painter, c + new Vector2(r, -r * 0.72f),
                    c + new Vector2(r, r * 0.72f));
                break;
            case UiGlyphId.Captain:
                Line(painter, c + new Vector2(-r * 0.64f, r),
                    c + new Vector2(-r * 0.64f, -r));
                Path(painter,
                    c + new Vector2(-r * 0.58f, -r * 0.82f),
                    c + new Vector2(r * 0.88f, -r * 0.45f),
                    c + new Vector2(-r * 0.58f, r * 0.05f));
                break;
            case UiGlyphId.Regen:
                Plus(painter, c + new Vector2(-r * 0.20f, r * 0.18f), r * 0.65f);
                Line(painter, c + new Vector2(r * 0.45f, r * 0.72f),
                    c + new Vector2(r * 0.45f, -r * 0.72f));
                Path(painter,
                    c + new Vector2(r * 0.12f, -r * 0.40f),
                    c + new Vector2(r * 0.45f, -r * 0.75f),
                    c + new Vector2(r * 0.78f, -r * 0.40f));
                break;
            case UiGlyphId.Stun:
                Path(painter,
                    c + new Vector2(r * 0.20f, -r),
                    c + new Vector2(-r * 0.48f, r * 0.08f),
                    c + new Vector2(r * 0.06f, r * 0.02f),
                    c + new Vector2(-r * 0.25f, r),
                    c + new Vector2(r * 0.62f, -r * 0.14f),
                    c + new Vector2(r * 0.05f, -r * 0.05f));
                break;
            case UiGlyphId.Line:
                Line(painter, c + new Vector2(-r, 0f), c + new Vector2(r, 0f));
                Path(painter,
                    c + new Vector2(r * 0.48f, -r * 0.48f),
                    c + new Vector2(r, 0f),
                    c + new Vector2(r * 0.48f, r * 0.48f));
                break;
            case UiGlyphId.Distance:
                Line(painter, c + new Vector2(-r, 0f), c + new Vector2(r, 0f));
                Line(painter, c + new Vector2(-r, -r * 0.55f),
                    c + new Vector2(-r, r * 0.55f));
                Line(painter, c + new Vector2(r, -r * 0.55f),
                    c + new Vector2(r, r * 0.55f));
                break;
            case UiGlyphId.Glyph:
                Diamond(painter, c, r);
                Poly(painter, c, r * 0.48f, 6, 30f);
                break;
            case UiGlyphId.Burn:
                Flame(painter, c, r);
                break;
            case UiGlyphId.Frenzy:
                for (int i = -1; i <= 1; i++)
                    Line(painter,
                        c + new Vector2(-r + i * r * 0.44f, r * 0.82f),
                        c + new Vector2(r + i * r * 0.44f, -r * 0.82f));
                break;
            case UiGlyphId.LowHealth:
                Heart(painter, c + new Vector2(-r * 0.12f, -r * 0.08f), r * 0.74f);
                Line(painter, c + new Vector2(r * 0.64f, -r * 0.65f),
                    c + new Vector2(r * 0.64f, r * 0.66f));
                Path(painter,
                    c + new Vector2(r * 0.36f, r * 0.36f),
                    c + new Vector2(r * 0.64f, r * 0.70f),
                    c + new Vector2(r * 0.92f, r * 0.36f));
                break;
            case UiGlyphId.Counter:
                Path(painter,
                    c + new Vector2(r * 0.90f, r * 0.24f),
                    c + new Vector2(r * 0.38f, -r * 0.62f),
                    c + new Vector2(-r * 0.48f, -r * 0.62f),
                    c + new Vector2(-r * 0.90f, r * 0.10f),
                    c + new Vector2(-r * 0.32f, r * 0.76f));
                Path(painter,
                    c + new Vector2(-r * 0.86f, r * 0.12f),
                    c + new Vector2(-r * 0.24f, r * 0.18f),
                    c + new Vector2(-r * 0.34f, r * 0.76f));
                break;
            case UiGlyphId.Mana:
                Path(painter,
                    c + new Vector2(0f, -r),
                    c + new Vector2(r * 0.72f, r * 0.10f),
                    c + new Vector2(r * 0.44f, r * 0.78f),
                    c + new Vector2(0f, r),
                    c + new Vector2(-r * 0.44f, r * 0.78f),
                    c + new Vector2(-r * 0.72f, r * 0.10f),
                    c + new Vector2(0f, -r));
                break;
            case UiGlyphId.Haste:
                Chevron(painter, c + new Vector2(-r * 0.34f, 0f), r * 0.60f);
                Chevron(painter, c + new Vector2(r * 0.34f, 0f), r * 0.60f);
                break;
            case UiGlyphId.Check:
                Path(painter, c + new Vector2(-r, 0f),
                    c + new Vector2(-r * 0.25f, r * 0.65f),
                    c + new Vector2(r, -r * 0.72f));
                break;
            case UiGlyphId.Lock:
                Poly(painter, c + new Vector2(0f, r * 0.28f), r * 0.66f, 4, 45f);
                Path(painter,
                    c + new Vector2(-r * 0.48f, -r * 0.08f),
                    c + new Vector2(-r * 0.48f, -r * 0.66f),
                    c + new Vector2(r * 0.48f, -r * 0.66f),
                    c + new Vector2(r * 0.48f, -r * 0.08f));
                break;
            case UiGlyphId.Hourstone:
                Line(painter, c + new Vector2(-r * 0.74f, -r),
                    c + new Vector2(r * 0.74f, -r));
                Line(painter, c + new Vector2(-r * 0.74f, r),
                    c + new Vector2(r * 0.74f, r));
                Path(painter,
                    c + new Vector2(-r * 0.58f, -r * 0.82f),
                    c + new Vector2(-r * 0.42f, -r * 0.28f),
                    c,
                    c + new Vector2(r * 0.42f, -r * 0.28f),
                    c + new Vector2(r * 0.58f, -r * 0.82f));
                Path(painter,
                    c + new Vector2(-r * 0.58f, r * 0.82f),
                    c + new Vector2(-r * 0.42f, r * 0.28f),
                    c,
                    c + new Vector2(r * 0.42f, r * 0.28f),
                    c + new Vector2(r * 0.58f, r * 0.82f));
                break;
            default:
                Diamond(painter, c, r * 0.70f);
                break;
        }
    }

    private static void Heart(Painter2D p, Vector2 c, float r) =>
        Path(p,
            c + new Vector2(0f, r),
            c + new Vector2(-r, 0f),
            c + new Vector2(-r * 0.62f, -r * 0.66f),
            c + new Vector2(0f, -r * 0.18f),
            c + new Vector2(r * 0.62f, -r * 0.66f),
            c + new Vector2(r, 0f),
            c + new Vector2(0f, r));

    private static void CrossedBlades(Painter2D p, Vector2 c, float r)
    {
        Line(p, c + new Vector2(-r, -r), c + new Vector2(r, r));
        Line(p, c + new Vector2(r, -r), c + new Vector2(-r, r));
        Line(p, c + new Vector2(-r, r * 0.60f),
            c + new Vector2(-r * 0.60f, r));
        Line(p, c + new Vector2(r, r * 0.60f),
            c + new Vector2(r * 0.60f, r));
    }

    private static void Plus(Painter2D p, Vector2 c, float r)
    {
        Line(p, c + new Vector2(-r, 0f), c + new Vector2(r, 0f));
        Line(p, c + new Vector2(0f, -r), c + new Vector2(0f, r));
    }

    private static void Shield(Painter2D p, Vector2 c, float r) =>
        Path(p,
            c + new Vector2(-r, -r * 0.72f),
            c + new Vector2(0f, -r),
            c + new Vector2(r, -r * 0.72f),
            c + new Vector2(r * 0.72f, r * 0.45f),
            c + new Vector2(0f, r),
            c + new Vector2(-r * 0.72f, r * 0.45f),
            c + new Vector2(-r, -r * 0.72f));

    private static void Leap(Painter2D p, Vector2 c, float r)
    {
        Path(p,
            c + new Vector2(-r, r * 0.60f),
            c + new Vector2(-r * 0.42f, -r * 0.48f),
            c + new Vector2(r * 0.42f, -r * 0.48f),
            c + new Vector2(r, r * 0.15f));
        Path(p,
            c + new Vector2(r * 0.48f, r * 0.02f),
            c + new Vector2(r, r * 0.15f),
            c + new Vector2(r * 0.72f, r * 0.68f));
    }

    private static void Star(Painter2D p, Vector2 c, float r)
    {
        p.BeginPath();
        for (int i = 0; i <= 8; i++)
        {
            float radius = (i & 1) == 0 ? r : r * 0.42f;
            float angle = (-90f + i * 45f) * Mathf.Deg2Rad;
            Vector2 point = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (i == 0) p.MoveTo(point);
            else p.LineTo(point);
        }
        p.Stroke();
    }

    private static void Flame(Painter2D p, Vector2 c, float r) =>
        Path(p,
            c + new Vector2(0f, -r),
            c + new Vector2(r * 0.34f, -r * 0.20f),
            c + new Vector2(r * 0.84f, r * 0.30f),
            c + new Vector2(r * 0.36f, r),
            c + new Vector2(-r * 0.36f, r),
            c + new Vector2(-r * 0.82f, r * 0.18f),
            c + new Vector2(-r * 0.30f, -r * 0.38f),
            c + new Vector2(0f, -r));

    private static void Chevron(Painter2D p, Vector2 c, float r) =>
        Path(p, c + new Vector2(-r * 0.55f, -r),
            c + new Vector2(r * 0.55f, 0f),
            c + new Vector2(-r * 0.55f, r));

    private static void Diamond(Painter2D p, Vector2 c, float r) =>
        Poly(p, c, r, 4, 45f);

    private static void Dot(Painter2D p, Vector2 c, float r) =>
        Poly(p, c, r, 8, 22.5f);

    private static void Poly(Painter2D p, Vector2 c, float r, int sides, float rotation)
    {
        p.BeginPath();
        for (int i = 0; i <= sides; i++)
        {
            float angle = (rotation + i * 360f / sides) * Mathf.Deg2Rad;
            Vector2 point = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            if (i == 0) p.MoveTo(point);
            else p.LineTo(point);
        }
        p.Stroke();
    }

    private static void Line(Painter2D p, Vector2 from, Vector2 to)
    {
        p.BeginPath();
        p.MoveTo(from);
        p.LineTo(to);
        p.Stroke();
    }

    private static void Path(Painter2D p, params Vector2[] points)
    {
        if (points == null || points.Length < 2) return;
        p.BeginPath();
        p.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
        p.Stroke();
    }
}

internal static class UiGlyphCatalog
{
    public static UiGlyphId Parse(string value, UiGlyphId fallback = UiGlyphId.Unknown)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse(value.Replace(" ", ""), true, out UiGlyphId result)
            ? result
            : fallback;
    }

    public static UiGlyphId Keyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return UiGlyphId.Unknown;
        return value.Trim().ToUpperInvariant() switch
        {
            "AREA" => UiGlyphId.Area,
            "REGEN" => UiGlyphId.Regen,
            "STUN" => UiGlyphId.Stun,
            "SHIELD" => UiGlyphId.Shield,
            "BURST" => UiGlyphId.Burst,
            "LEAP" => UiGlyphId.Leap,
            "LINE" => UiGlyphId.Line,
            "DISTANCE" => UiGlyphId.Distance,
            "GLYPH" => UiGlyphId.Glyph,
            "BURN" => UiGlyphId.Burn,
            "FRENZY" => UiGlyphId.Frenzy,
            "LOW HP" => UiGlyphId.LowHealth,
            "COUNTER" => UiGlyphId.Counter,
            "MANA" => UiGlyphId.Mana,
            "HASTE" => UiGlyphId.Haste,
            _ => UiGlyphId.Unknown,
        };
    }
}
