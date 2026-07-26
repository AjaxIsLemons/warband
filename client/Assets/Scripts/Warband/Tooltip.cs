using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Warband.Sim;

/// <summary>
/// Play-mode hover tooltip (unit-identity spec §C). A code-built runtime UI Toolkit card that reads
/// the live fold each frame while hovered: chassis name, team chip, HP, Shield (when &gt;0), Mana
/// (when a caster), and one line per live status. Picking is screen-space nearest with NO colliders
/// (MakePrimitive strips them) — <see cref="ReplayPlayer.PickUnit"/> owns it, so this stays a thin
/// view. sortingOrder 900 keeps it below the DebugMenu (1000). Auto-spawns like DebugMenu.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class Tooltip : MonoBehaviour
{
    // Startup order is owned by GameBoot — see that class before adding one back here.

    // Mirror DebugMenu's dark-panel palette so the two cockpits read as one system.
    private static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.97f);
    private static readonly Color Border = new Color(0.30f, 0.55f, 0.90f, 0.55f);
    private static readonly Color TextCol = new Color(0.88f, 0.91f, 0.96f);
    private static readonly Color Muted = new Color(0.58f, 0.63f, 0.71f);
    private static readonly Color TitleCol = new Color(0.85f, 0.92f, 1f);
    private static readonly Color TeamBlue = new Color(0.30f, 0.55f, 0.95f);
    private static readonly Color TeamRed = new Color(0.90f, 0.35f, 0.30f);

    private const float PickRadius = 48f; // px — spec §C

    private UIDocument _doc;
    private ReplayPlayer _player;
    private VisualElement _card, _chip, _statusBox;
    private Label _name, _chipLabel, _hp, _shield, _mana;

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        var theme = Resources.Load<ThemeStyleSheet>("DebugTheme"); // same base theme the DebugMenu uses
        if (theme != null) ps.themeStyleSheet = theme;
        ps.scaleMode = PanelScaleMode.ConstantPixelSize;
        ps.sortingOrder = 900;
        _doc.panelSettings = ps;
        _doc.sortingOrder = 900; // below DebugMenu's 1000

        _player = FindFirstObjectByType<ReplayPlayer>();
        BuildCard();
    }

    private void BuildCard()
    {
        var root = _doc.rootVisualElement;
        root.Clear();
        root.pickingMode = PickingMode.Ignore;

        _card = new VisualElement();
        _card.pickingMode = PickingMode.Ignore;
        var s = _card.style;
        s.position = Position.Absolute;
        s.minWidth = 250;
        s.paddingLeft = s.paddingRight = 13; s.paddingTop = s.paddingBottom = 11;
        s.backgroundColor = PanelBg;
        s.color = TextCol;
        Round(s, 6);
        s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = s.borderBottomWidth = 1;
        s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = Border;
        s.display = DisplayStyle.None;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.marginBottom = 4;
        _name = new Label("—");
        _name.style.unityFontStyleAndWeight = FontStyle.Bold;
        _name.style.fontSize = 18; _name.style.color = TitleCol; _name.style.marginRight = 14;
        _chip = new VisualElement();
        _chip.style.paddingLeft = 6; _chip.style.paddingRight = 6;
        _chip.style.paddingTop = 3; _chip.style.paddingBottom = 3;
        Round(_chip.style, 3);
        _chipLabel = new Label("BLUE");
        _chipLabel.style.fontSize = 12; _chipLabel.style.color = Color.white;
        _chipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _chip.Add(_chipLabel);
        header.Add(_name); header.Add(_chip);
        _card.Add(header);

        _hp = StatLine(); _card.Add(_hp);
        _shield = StatLine(); _card.Add(_shield);
        _mana = StatLine(); _card.Add(_mana);
        _statusBox = new VisualElement();
        _statusBox.style.marginTop = 2;
        _card.Add(_statusBox);

        root.Add(_card);
        _card.Query<VisualElement>().ForEach(element =>
            element.pickingMode = PickingMode.Ignore);
    }

    private static Label StatLine()
    {
        var l = new Label("");
        l.style.fontSize = 15; l.style.marginTop = 3;
        return l;
    }

    private void Update()
    {
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
        var mouse = Mouse.current;
        // PickUnit returns null in edit mode, without a camera, during the fight-end hold, or when
        // nothing is within PickRadius — so a null here is the single "hide the card" signal.
        PlaybackUnit u = (_player != null && mouse != null)
            ? _player.PickUnit(mouse.position.ReadValue(), PickRadius) : null;
        if (u == null) { Hide(); return; }
        Fill(u);
        PlaceAt(mouse.position.ReadValue());
        if (_card.style.display != DisplayStyle.Flex) _card.style.display = DisplayStyle.Flex;
    }

    private void Hide()
    {
        if (_card != null && _card.style.display != DisplayStyle.None)
            _card.style.display = DisplayStyle.None;
    }

    private void Fill(PlaybackUnit u)
    {
        _name.text = string.IsNullOrEmpty(u.Name) ? $"Unit {u.Id}" : u.Name;
        bool blue = u.Team == 0;
        _chipLabel.text = blue ? "BLUE" : "RED";
        _chip.style.backgroundColor = blue ? TeamBlue : TeamRed;

        _hp.text = $"HP  {u.Hp} / {u.MaxHp}";
        bool hasShield = u.Shield > 0;
        _shield.style.display = hasShield ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasShield) _shield.text = $"Shield  {u.Shield}";
        bool caster = u.ManaMax > 0;
        _mana.style.display = caster ? DisplayStyle.Flex : DisplayStyle.None;
        if (caster) _mana.text = $"Mana  {u.Mana} / {u.ManaMax}";

        _statusBox.Clear();
        foreach (var st in u.Statuses)
        {
            var l = new Label($"{st.Kind} ×{st.Mag}");
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = 13; l.style.color = Muted;
            _statusBox.Add(l);
        }
    }

    /// <summary>Mouse.current.position is bottom-left origin; the UI Toolkit panel is top-left, so flip
    /// Y and offset off the cursor. Constant-pixel panel ⇒ ~1:1 with screen px (Jake eyeballs the nudge).</summary>
    private void PlaceAt(Vector2 mouse)
    {
        _card.style.left = Mathf.Min(mouse.x + 18f, Screen.width - 286f);
        _card.style.top = Mathf.Min((Screen.height - mouse.y) + 18f, Screen.height - 250f);
    }

    private static void Round(IStyle s, float r)
    {
        s.borderTopLeftRadius = r; s.borderTopRightRadius = r;
        s.borderBottomLeftRadius = r; s.borderBottomRightRadius = r;
    }
}
