using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Warband.Content;
using Warband.Sim;

/// <summary>
/// Play-mode hover tooltip (unit-identity spec §C). A code-built runtime UI Toolkit card that reads
/// the live fold each frame while hovered: name + team chip, the identity line (chassis, signature,
/// weapon and temper), HP/Shield/Mana, the placement facts (reach, cadence, step, crit), the
/// TARGETING RULE, the unit's PASSIVE ROSTER with the conditional ones marked live or idle, and one
/// line per status. Before 2026-07-27 this showed a name and three bars: everything else was already
/// on the fold and thrown away (roadmap item 21). Picking is screen-space nearest with NO colliders
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
    private static readonly Color Dimmed = new Color(0.44f, 0.47f, 0.53f);   // an armed-but-idle passive

    private const float PickRadius = 48f; // px — spec §C

    private UIDocument _doc;
    private ReplayPlayer _player;
    private VisualElement _card, _chip, _statusBox, _stats, _passiveBox;
    private Label _name, _chipLabel, _identity, _facts, _targeting, _passiveHeader, _statusHeader;
    private MechanicStatTile _hp, _shield, _mana;

    private static Label Muted13()
    {
        var l = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
        l.style.fontSize = 13; l.style.color = Muted; l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

    private static Label Header(string text)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = 10; l.style.color = Muted;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.letterSpacing = 1.5f;
        l.style.marginTop = 7; l.style.marginBottom = 2;
        return l;
    }

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
        var mechanics = Resources.Load<StyleSheet>("UI/MechanicPresentationStyles");
        if (mechanics != null) root.styleSheets.Add(mechanics);

        _card = new VisualElement();
        _card.pickingMode = PickingMode.Ignore;
        var s = _card.style;
        s.position = Position.Absolute;
        s.minWidth = 330;
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

        _stats = new VisualElement();
        _stats.AddToClassList("combat-tooltip-stats");
        _hp = new MechanicStatTile("combat-tooltip-stat", "combat-tooltip-stat");
        _shield = new MechanicStatTile("combat-tooltip-stat", "combat-tooltip-stat");
        _mana = new MechanicStatTile("combat-tooltip-stat", "combat-tooltip-stat");
        _stats.Add(_hp);
        _stats.Add(_shield);
        _stats.Add(_mana);
        _card.Add(_stats);

        // "What IS this unit" — chassis · signature · weapon, one line under the name. Everything
        // below it answers pve-encounters.md's inspectable-mechanics requirement, which the card
        // used to meet with three bars and nothing else.
        _identity = Muted13();
        _identity.style.marginTop = 1;
        _card.Add(_identity);

        // The placement facts: reach, cadence, step, crit. These are the questions a viewer asks
        // about a body they cannot control.
        _facts = Muted13();
        _facts.style.marginTop = 4;
        _card.Add(_facts);

        // The targeting rule (item 12 / ADR 0024's disclosure contract): a Sanddrift Gunner whose
        // entire design is "acquires FARTHEST, holds standoff 5" was previously a name and a number.
        _targeting = Muted13();
        _card.Add(_targeting);

        _passiveHeader = Header("PASSIVES");
        _card.Add(_passiveHeader);
        _passiveBox = new VisualElement();
        _card.Add(_passiveBox);

        _statusHeader = Header("STATUSES");
        _card.Add(_statusHeader);
        _statusBox = new VisualElement();
        _statusBox.style.marginTop = 2;
        _card.Add(_statusBox);

        root.Add(_card);
        _card.Query<VisualElement>().ForEach(element =>
            element.pickingMode = PickingMode.Ignore);
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

        _hp.Bind(new StatChipModel("HEALTH", $"{u.Hp} / {u.MaxHp}",
            u.Hp * 3 <= u.MaxHp ? "bad" : "", PresentationFactId.Hp));
        bool hasShield = u.Shield > 0;
        _shield.style.display = hasShield ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasShield)
            _shield.Bind(new StatChipModel("PROTECTION", u.Shield.ToString(), "good",
                PresentationFactId.Protection));
        bool caster = u.ManaMax > 0;
        _mana.style.display = caster ? DisplayStyle.Flex : DisplayStyle.None;
        if (caster)
            _mana.Bind(new StatChipModel("MANA", $"{u.Mana} / {u.ManaMax}", "",
                PresentationFactId.ManaThreshold));

        FillIdentity(u);
        FillFacts(u);
        FillPassives(u);

        _statusBox.Clear();
        foreach (var st in u.Statuses)
        {
            var l = new Label();
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = 13; l.style.color = Muted;
            MechanicPresentation.BindInline(l, $"{Lexicon.Of(st.Kind).Name} ×{st.Mag}");
            _statusBox.Add(l);
        }
        _statusHeader.style.display = u.Statuses.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Chassis · signature · weapon. The display Name is whatever the encounter author
    /// called this body ("Oathbound Bulwark"), so the identity line is where the mechanical truth
    /// underneath it goes — and it is resolved through the same content assembly the sim uses, so
    /// the card can never disagree with the fight.</summary>
    private void FillIdentity(PlaybackUnit u)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(u.ChassisId))
        {
            string chassis = ContentLexicon.Chassis(u.ChassisId).Name;
            string ability = AbilityIdentity.DisplayName(AbilityIdentity.Resolve(u.ChassisId, u.Traits));
            parts.Add(ability != chassis ? $"{chassis} · <b>{ability}</b>" : chassis);
        }
        if (!string.IsNullOrEmpty(u.WeaponName))
            parts.Add(u.WeaponTier == WeaponTier.Worn
                ? u.WeaponName
                : $"{u.WeaponName} <b>({u.WeaponTier})</b>");
        _identity.text = string.Join("  ·  ", parts);
        _identity.style.display = parts.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Reach/cadence/step in SECONDS, not ticks — the player has no tick clock. 10 ticks
    /// per second is the render contract's mapping, and it is the sim's own constant.</summary>
    private void FillFacts(PlaybackUnit u)
    {
        var facts = new List<string> { $"reach {u.Range}" };
        if (u.AttackInterval > 0) facts.Add($"cadence {u.AttackInterval / 10f:0.#}s");
        if (u.MoveInterval > 0) facts.Add($"step {u.MoveInterval / 10f:0.#}s");
        if (u.CritChance > 0) facts.Add($"crit {u.CritChance}%");
        if (u.HealAutos) facts.Add("swings heal");
        MechanicPresentation.BindInline(_facts, string.Join(" · ", facts));

        string acquire = u.TargetPref switch
        {
            TargetPref.Farthest => "Acquires the FARTHEST enemy",
            TargetPref.LowestHp => "Acquires the WEAKEST enemy",
            TargetPref.HighestHp => "Acquires the STRONGEST enemy",
            _ => "Acquires the NEAREST enemy",
        };
        if (u.Standoff > 0) acquire += $", holds {u.Standoff} hexes";
        MechanicPresentation.BindInline(_targeting, acquire);
    }

    /// <summary>
    /// The passive roster — what this unit's engine actually IS, and which parts of it are running
    /// right now. Names come from the rule ids the composer stamps (Design/passive-legibility.md),
    /// so a spec node authored tomorrow appears here with no work.
    ///
    /// <para>Triggers and StatRules read differently on purpose. A trigger is always ARMED and fires
    /// on an event, so a lit/unlit state would be a lie; a StatRule is a live predicate, and the
    /// fold knows whether it is on. That distinction is the whole point — a conditional passive that
    /// has come online is exactly the thing that was invisible before, and this is where it becomes
    /// persistent state rather than a flash you had to be watching for.</para>
    /// </summary>
    private void FillPassives(PlaybackUnit u)
    {
        _passiveBox.Clear();
        int shown = 0;
        for (int i = 0; i < u.TriggerRuleCount; i++)
            shown += AddPassive(u.TriggerRuleBase + i, live: null) ? 1 : 0;
        for (int i = 0; i < u.StatRuleCount; i++)
            shown += AddPassive(u.StatRuleBase + i, live: u.ActiveRules.Contains(u.StatRuleBase + i)) ? 1 : 0;
        _passiveHeader.style.display = shown > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool AddPassive(int index, bool? live)
    {
        if (index < 0 || _player == null) return false;
        string id = _player.RuleIdAt(index);
        if (string.IsNullOrEmpty(id)) return false;
        var entry = ContentLexicon.Rule(id);

        var row = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
        row.style.fontSize = 13;
        row.style.whiteSpace = WhiteSpace.Normal;
        // A live conditional rule is the one thing on this card worth being bright about; an
        // armed-but-idle one stays quiet, per the proportionality law the tells follow.
        bool dim = live == false;
        row.style.color = dim ? Dimmed : TextCol;
        string dot = live == null ? "·" : live.Value ? "●" : "○";
        row.text = $"{dot} {entry.Name}" + (live == true ? "  <b>LIVE</b>" : "");
        _passiveBox.Add(row);

        if (!string.IsNullOrEmpty(entry.Text))
        {
            var sub = Muted13();
            sub.style.fontSize = 11;
            sub.style.marginLeft = 13; sub.style.marginBottom = 2;
            if (dim) sub.style.color = Dimmed;
            MechanicPresentation.BindInline(sub, entry.Text);
            _passiveBox.Add(sub);
        }
        return true;
    }

    /// <summary>Mouse.current.position is bottom-left origin; the UI Toolkit panel is top-left, so flip
    /// Y and offset off the cursor. Constant-pixel panel ⇒ ~1:1 with screen px (Jake eyeballs the nudge).</summary>
    private void PlaceAt(Vector2 mouse)
    {
        // Measured, not guessed. The card used to be a name and three bars, so a fixed 286×250
        // clamp was close enough; a full inspector's height depends on how many passives the unit
        // carries, and a hardcoded clamp would push a Berserker's card off the bottom of the screen.
        // resolvedStyle is 0 on the first frame a card is shown — fall back to the old constants
        // for that one frame rather than clamping to zero and pinning it to the corner.
        float w = _card.resolvedStyle.width > 1f ? _card.resolvedStyle.width : 286f;
        float h = _card.resolvedStyle.height > 1f ? _card.resolvedStyle.height : 250f;
        _card.style.left = Mathf.Max(4f, Mathf.Min(mouse.x + 18f, Screen.width - w - 4f));
        _card.style.top = Mathf.Max(4f, Mathf.Min((Screen.height - mouse.y) + 18f, Screen.height - h - 4f));
    }

    private static void Round(IStyle s, float r)
    {
        s.borderTopLeftRadius = r; s.borderTopRightRadius = r;
        s.borderBottomLeftRadius = r; s.borderBottomRightRadius = r;
    }
}
