using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Item 5b (Jake, 2026-07-27): the persistent Hourstone tray. One icon per owned Inscription,
/// top-center on EVERY surface, following WarbandBarView's persistent-chrome pattern. Three
/// states: a compact collapsed tray (capped icons + "+N" overflow), a hover-opened drawer
/// (full-size icons, counter pips, per-icon full-rule tooltips), and combat — always expanded,
/// because a law firing mid-fight must be readable at a glance, never behind a hover.
/// Views never touch it; RunShell pushes resolved entries (ids→words happen there, per the
/// shell law) and the fight bridge calls <see cref="Flash"/>/<see cref="SetPips"/>.
/// </summary>
internal sealed class InscriptionRailView
{
    internal struct Entry
    {
        public string Key;        // registry key ("stilledbell") — Flash/SetPips route by this
        public string Icon;       // PresentationCatalog glyph
        public string Name;       // display name ("The Stilled Bell")
        public string Rule;       // full mechanical copy for the tooltip
        public string Accent;     // accent--<kind>, shared vocabulary with spec badges
    }

    private const int CollapsedCap = 6;   // icons shown before "+N" takes over
    private const float PulseSeconds = 0.6f;

    private readonly VisualElement _root;
    private readonly VisualElement _tray;
    private readonly Label _more;
    private readonly RuntimeTooltipService _tooltip;
    private readonly List<VisualElement> _icons = new List<VisualElement>();
    private readonly Dictionary<string, int> _byKey = new Dictionary<string, int>();
    private readonly List<Label> _pips = new List<Label>();
    private readonly List<float> _glow = new List<float>();
    private bool _combat;
    private bool _hover;

    public VisualElement Root => _root;

    public InscriptionRailView(VisualElement shellRoot, RuntimeTooltipService tooltip)
    {
        _tooltip = tooltip;
        _root = new VisualElement { name = "persistent-inscription-rail" };
        _root.AddToClassList("inscription-rail");
        _root.pickingMode = PickingMode.Ignore;      // the strip spans the width; only the tray hits
        _tray = new VisualElement { name = "inscription-tray" };
        _tray.AddToClassList("inscription-rail__tray");
        _tray.pickingMode = PickingMode.Position;
        _root.Add(_tray);
        _more = new Label("");
        _more.AddToClassList("inscription-rail__more");
        _more.pickingMode = PickingMode.Ignore;
        shellRoot.Add(_root);

        // The drawer: hover opens, leave collapses. Combat pins it open and ignores both.
        _tray.RegisterCallback<MouseEnterEvent>(_ => { _hover = true; SyncOpen(); });
        _tray.RegisterCallback<MouseLeaveEvent>(_ => { _hover = false; SyncOpen(); });
    }

    /// <summary>Combat pins the tray fully expanded — every icon always visible (Jake: you must
    /// be able to read activations at a glance while fighting).</summary>
    public void SetCombat(bool combat)
    {
        _combat = combat;
        SyncOpen();
    }

    private void SyncOpen()
    {
        bool open = _combat || _hover;
        _root.EnableInClassList("inscription-rail--combat", _combat);
        _root.EnableInClassList("inscription-rail--open", open && !_combat);
    }

    /// <summary>Rebuild from the run's owned laws, acquisition order. Called by RunShell on
    /// model refresh; an empty run hides the tray entirely.</summary>
    public void SetEntries(IReadOnlyList<Entry> entries)
    {
        _tray.Clear(); _icons.Clear(); _byKey.Clear(); _pips.Clear(); _glow.Clear();
        bool any = entries != null && entries.Count > 0;
        _root.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
        if (!any) return;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            var slot = new VisualElement();
            slot.AddToClassList("inscription-rail__slot");
            if (i >= CollapsedCap) slot.AddToClassList("inscription-rail__slot--overflow");
            var icon = new Label(e.Icon);
            icon.AddToClassList("inscription-rail__icon");
            if (!string.IsNullOrEmpty(e.Accent)) icon.AddToClassList("accent--" + e.Accent);
            icon.pickingMode = PickingMode.Position;
            icon.focusable = true;
            string name = e.Name; string rule = e.Rule;
            _tooltip?.Attach(icon, () => new RuntimeTooltipModel { Title = name, Body = rule });
            var pip = new Label("");
            pip.AddToClassList("inscription-rail__pips");
            slot.Add(icon); slot.Add(pip);
            _tray.Add(slot);
            _byKey[e.Key] = i; _icons.Add(icon); _pips.Add(pip); _glow.Add(0f);
        }
        int hidden = entries.Count - CollapsedCap;
        _more.text = hidden > 0 ? "+" + hidden : "";
        if (hidden > 0) _tray.Add(_more);
        SyncOpen();
    }

    /// <summary>A law fired: pulse its icon. Coalescing/rationing is the CALLER's job (the fight
    /// bridge hears the dispatch); this is purely the visual. While collapsed, an overflowed
    /// icon's pulse lights the "+N" chip instead, so nothing ever fires invisibly.</summary>
    public void Flash(string key)
    {
        if (!_byKey.TryGetValue(key, out int i)) return;
        _glow[i] = 1f;
        _icons[i].AddToClassList("inscription-rail__icon--live");
        if (i >= CollapsedCap && !_combat)
            _more.AddToClassList("inscription-rail__icon--live");
    }

    /// <summary>Counter progress under an icon ("2/3"); progress ≤ 0 clears. SET from the fold,
    /// never accumulated (ADR 0004) — visible only while expanded, by stylesheet.</summary>
    public void SetPips(string key, int progress, int n)
    {
        if (!_byKey.TryGetValue(key, out int i)) return;
        _pips[i].text = progress > 0 && n > 1 ? progress + "/" + n : "";
    }

    /// <summary>Decay pulses toward quiet. Driven from RunShell's Update.</summary>
    public void Tick(float dt)
    {
        bool anyLive = false;
        for (int i = 0; i < _glow.Count; i++)
        {
            if (_glow[i] <= 0f) continue;
            _glow[i] -= dt / PulseSeconds;
            if (_glow[i] <= 0f)
            { _glow[i] = 0f; _icons[i].RemoveFromClassList("inscription-rail__icon--live"); }
            else anyLive = true;
        }
        if (!anyLive) _more.RemoveFromClassList("inscription-rail__icon--live");
    }

    /// <summary>Screen-space centre of an icon, for the fight bridge's indicator line.</summary>
    public bool TryIconCenter(string key, out Vector2 panelPos)
    {
        panelPos = default;
        if (!_byKey.TryGetValue(key, out int i)) return false;
        Rect r = _icons[i].worldBound;
        if (float.IsNaN(r.x) || r.width <= 0f) return false;
        panelPos = r.center;
        return true;
    }
}

/// <summary>
/// Item 5b's activation indicator: when a law fires in combat, a brief line draws from its tray
/// icon down to the affected unit and fades. Pure decoration on a full-screen, non-picking
/// overlay — painter2D, no layout participation, so it can never move or squash anything. Lines
/// age out in a fraction of a second; a chain draws several in dispatch order, which IS the
/// "badges pulse in resolution order" law from Design/inscriptions.md.
/// </summary>
internal sealed class InscriptionIndicatorLayer : VisualElement
{
    private const float LifeSeconds = 0.45f;
    private static readonly Color LineColor = new Color(0.85f, 0.64f, 0.23f); // --lh-sand

    private struct Line { public Vector2 From, To; public float Age; }
    private readonly List<Line> _lines = new List<Line>();

    public InscriptionIndicatorLayer()
    {
        name = "inscription-indicators";
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.top = 0; style.left = 0; style.right = 0; style.bottom = 0;
        generateVisualContent += Draw;
    }

    public void Add(Vector2 fromPanel, Vector2 toPanel)
    {
        _lines.Add(new Line { From = fromPanel, To = toPanel });
        MarkDirtyRepaint();
    }

    public void Tick(float dt)
    {
        if (_lines.Count == 0) return;
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            Line l = _lines[i];
            l.Age += dt;
            if (l.Age >= LifeSeconds) _lines.RemoveAt(i);
            else _lines[i] = l;
        }
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext ctx)
    {
        if (_lines.Count == 0) return;
        var painter = ctx.painter2D;
        foreach (Line l in _lines)
        {
            float a = 1f - l.Age / LifeSeconds;
            painter.strokeColor = new Color(LineColor.r, LineColor.g, LineColor.b, a * 0.8f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            // Panel coordinates: this element spans the whole panel, so world-bound positions
            // from other elements map straight in.
            painter.MoveTo(this.WorldToLocal(l.From));
            painter.LineTo(this.WorldToLocal(l.To));
            painter.Stroke();
        }
    }
}
