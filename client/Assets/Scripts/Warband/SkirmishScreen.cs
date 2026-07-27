using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Content;
using Warband.Run;

internal enum SkirmishScreenState
{
    Planning,
    Playing,
    Result,
}

internal enum SkirmishOutcomeTone
{
    None,
    Victory,
    Defeat,
    Draw,
}

internal enum SkirmishRosterDropKind
{
    Board,
    Hero,
    BenchSlot,
}

internal sealed class SkirmishRosterDrop
{
    public string SourceHeroId = "";
    public SkirmishRosterDropKind Kind;
    public string TargetHeroId = "";
    public int BenchSlot = -1;
    public Vector2 PanelPosition;
}

internal sealed class SkirmishHeroView
{
    public string Id = "";
    public string Name = "";
    public string ChampionName = "";
    public string Role = "";
    public string Position = "";
    public string WeaponName = "";
    public PlanningZone Zone;
    public int BenchSlot = -1;
    public bool Selected;
}

internal sealed class SkirmishWeaponView
{
    public string Id = "";
    public string Name = "";
    public string Summary = "";
    public string Trait = "";
    public bool Mastered;
    public bool Selected;
}

internal sealed class SkirmishStatView
{
    public string Name = "";
    public string Value = "";
}

internal sealed class SkirmishScreenModel
{
    public SkirmishScreenState State;
    public string Step = "";
    public string EncounterName = "";
    public string Pressure = "";
    public string Rule = "";
    public string Feedback = "";
    public bool FeedbackIsError;
    public string Instruction = "";
    public string PrimaryText = "";
    public bool PrimaryEnabled;
    public string Outcome = "";
    public SkirmishOutcomeTone OutcomeTone;

    public bool DrawerOpen;
    public int FieldCount;
    public int FieldCapacity;
    public int BenchCount;
    public int BenchCapacity;
    public bool CanUndo;
    public bool CanRedo;
    public string RosterActionText = "";
    public bool RosterActionEnabled;

    public string HeroName = "";
    public string ChampionName = "";
    public string Role = "";
    public string PassiveName = "";
    public string PassiveText = "";
    public string SignatureName = "";
    public string SignatureText = "";
    public string MasteryName = "";
    public string MasteryText = "";
    public bool MasteryActive;

    public readonly List<SkirmishHeroView> Heroes = new List<SkirmishHeroView>();
    public readonly List<SkirmishWeaponView> Weapons = new List<SkirmishWeaponView>();
    public readonly List<SkirmishStatView> Stats = new List<SkirmishStatView>();
}

/// <summary>
/// UXML-backed view for the board-first Planning workspace. It renders immutable view data and
/// emits intentions; roster, loadout, formation, history, and combat rules stay outside the view.
/// </summary>
internal sealed class SkirmishScreen : IDisposable
{
    private sealed class DropTargetData
    {
        public SkirmishRosterDropKind Kind;
        public string HeroId = "";
        public int BenchSlot = -1;
    }

    private const float DragThreshold = 7f;

    private readonly Action _onPrimary;
    private readonly Action<string> _onHero;
    private readonly Action<string> _onWeapon;
    private readonly Action _onToggleDrawer;
    private readonly Action _onUndo;
    private readonly Action _onRedo;
    private readonly Action _onRosterAction;
    private readonly Action<int> _onBenchSlot;
    private readonly Action<SkirmishRosterDrop> _onRosterDrop;
    private readonly EventCallback<PointerDownEvent> _onBoardDown;
    private readonly EventCallback<PointerMoveEvent> _onBoardMove;
    private readonly EventCallback<PointerUpEvent> _onBoardUp;

    private readonly VisualElement _root;
    private readonly VisualElement _boardSurface;
    private readonly Label _step;
    private readonly Label _title;
    private readonly Label _pressure;
    private readonly Label _rule;
    private readonly Label _instruction;
    private readonly Label _feedback;
    private readonly VisualElement _drawer;
    private readonly VisualElement _drawerBody;
    private readonly Button _drawerToggle;
    private readonly Label _drawerCount;
    private readonly Label _drawerChevron;
    private readonly VisualElement _fieldList;
    private readonly VisualElement _benchList;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _rosterAction;
    private readonly VisualElement _weaponList;
    private readonly VisualElement _statList;
    private readonly Label _loadoutHero;
    private readonly Label _loadoutChampion;
    private readonly Label _loadoutRole;
    private readonly Label _passiveName;
    private readonly Label _passiveText;
    private readonly Label _signatureName;
    private readonly Label _signatureText;
    private readonly VisualElement _masteryCard;
    private readonly Label _masteryName;
    private readonly Label _masteryText;
    private readonly VisualElement _commitCluster;
    private readonly VisualElement _resultPanel;
    private readonly Label _outcome;
    private readonly Button _primary;
    private readonly Label _dragGhost;

    private VisualElement _dragSourceElement;
    private string _dragSourceHeroId = "";
    private string _dragSourceLabel = "";
    private Vector2 _dragStart;
    private int _dragPointerId = -1;
    private bool _dragging;

    public float PanelWidth =>
        float.IsNaN(_root.resolvedStyle.width) || _root.resolvedStyle.width < 1f
            ? Screen.width
            : _root.resolvedStyle.width;

    public float PanelHeight =>
        float.IsNaN(_root.resolvedStyle.height) || _root.resolvedStyle.height < 1f
            ? Screen.height
            : _root.resolvedStyle.height;

    public SkirmishScreen(
        UIDocument document,
        EncounterDef encounter,
        Action onPrimary,
        Action<string> onHero,
        Action<string> onWeapon,
        Action onToggleDrawer,
        Action onUndo,
        Action onRedo,
        Action onRosterAction,
        Action<int> onBenchSlot,
        Action<SkirmishRosterDrop> onRosterDrop,
        EventCallback<PointerDownEvent> onBoardDown,
        EventCallback<PointerMoveEvent> onBoardMove,
        EventCallback<PointerUpEvent> onBoardUp)
    {
        _onPrimary = onPrimary;
        _onHero = onHero;
        _onWeapon = onWeapon;
        _onToggleDrawer = onToggleDrawer;
        _onUndo = onUndo;
        _onRedo = onRedo;
        _onRosterAction = onRosterAction;
        _onBenchSlot = onBenchSlot;
        _onRosterDrop = onRosterDrop;
        _onBoardDown = onBoardDown;
        _onBoardMove = onBoardMove;
        _onBoardUp = onBoardUp;

        var tree = Resources.Load<VisualTreeAsset>("UI/Skirmish");
        // Keep the USS basename distinct from the UXML. Resources.Load<T> can otherwise return
        // the UXML's generated inline StyleSheet subasset instead of the authored USS.
        var styles = Resources.Load<StyleSheet>("UI/SkirmishStyles");
        if (tree == null || styles == null)
            throw new InvalidOperationException(
                "[Skirmish] Resources/UI/Skirmish.uxml and SkirmishStyles.uss are required.");

        var documentRoot = document.rootVisualElement;
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1;
        tree.CloneTree(documentRoot);
        documentRoot.styleSheets.Add(styles);
        var mechanics = Resources.Load<StyleSheet>("UI/MechanicPresentationStyles");
        if (mechanics != null) documentRoot.styleSheets.Add(mechanics);

        _root = Required<VisualElement>(documentRoot, "skirmish-root");
        _boardSurface = Required<VisualElement>(_root, "board-hit-surface");
        _step = Required<Label>(_root, "step-label");
        _title = Required<Label>(_root, "title-label");
        _pressure = Required<Label>(_root, "pressure-label");
        _rule = Required<Label>(_root, "rule-label");
        _instruction = Required<Label>(_root, "instruction-label");
        _feedback = Required<Label>(_root, "feedback-label");
        _drawer = Required<VisualElement>(_root, "muster-drawer");
        _drawerBody = Required<VisualElement>(_root, "drawer-body");
        _drawerToggle = Required<Button>(_root, "drawer-toggle");
        _drawerCount = Required<Label>(_root, "drawer-count-label");
        _drawerChevron = Required<Label>(_root, "drawer-chevron-label");
        _fieldList = Required<VisualElement>(_root, "field-list");
        _benchList = Required<VisualElement>(_root, "bench-list");
        _undo = Required<Button>(_root, "undo-button");
        _redo = Required<Button>(_root, "redo-button");
        _rosterAction = Required<Button>(_root, "roster-action-button");
        _weaponList = Required<VisualElement>(_root, "weapon-list");
        _statList = Required<VisualElement>(_root, "stat-list");
        _loadoutHero = Required<Label>(_root, "loadout-hero-label");
        _loadoutChampion = Required<Label>(_root, "loadout-champion-label");
        _loadoutRole = Required<Label>(_root, "loadout-role-label");
        _passiveName = Required<Label>(_root, "passive-name-label");
        _passiveText = Required<Label>(_root, "passive-text-label");
        _signatureName = Required<Label>(_root, "signature-name-label");
        _signatureText = Required<Label>(_root, "signature-text-label");
        _masteryName = Required<Label>(_root, "mastery-name-label");
        _masteryText = Required<Label>(_root, "mastery-text-label");
        _masteryCard = _masteryName.parent;
        _commitCluster = Required<VisualElement>(_root, "commit-cluster");
        _resultPanel = Required<VisualElement>(_root, "result-panel");
        _outcome = Required<Label>(_root, "outcome-label");
        _primary = Required<Button>(_root, "primary-button");
        _dragGhost = Required<Label>(_root, "drag-ghost");
        _dragGhost.pickingMode = PickingMode.Ignore;
        SetDisplayed(_dragGhost, false);

        BuildEnemyList(Required<VisualElement>(_root, "enemy-list"), encounter);

        _primary.clicked += _onPrimary;
        _drawerToggle.clicked += _onToggleDrawer;
        _undo.clicked += _onUndo;
        _redo.clicked += _onRedo;
        _rosterAction.clicked += _onRosterAction;
        _boardSurface.RegisterCallback(_onBoardDown);
        _boardSurface.RegisterCallback(_onBoardMove);
        _boardSurface.RegisterCallback(_onBoardUp);
    }

    public void Render(SkirmishScreenModel model)
    {
        _step.text = model.Step;
        _title.text = model.EncounterName;
        _pressure.text = model.Pressure;
        MechanicPresentation.BindInline(_rule, model.Rule);
        _instruction.text = model.Instruction;
        _primary.text = model.PrimaryText;
        _primary.SetEnabled(model.PrimaryEnabled);

        SetStateClass(model.State);
        SetDisplayed(_drawer, model.State == SkirmishScreenState.Planning);
        SetDisplayed(_commitCluster, model.State != SkirmishScreenState.Playing);
        SetDisplayed(_resultPanel, model.State == SkirmishScreenState.Result);
        SetDisplayed(_feedback, model.State == SkirmishScreenState.Planning &&
                                !string.IsNullOrEmpty(model.Feedback));

        _feedback.text = model.Feedback;
        _feedback.EnableInClassList("feedback-label--error", model.FeedbackIsError);

        if (model.State == SkirmishScreenState.Planning)
            RenderPlanning(model);
        if (model.State == SkirmishScreenState.Result)
            RenderOutcome(model);
    }

    public void SetExternalDragGhost(string label, Vector2 panelPosition, bool visible)
    {
        if (!visible)
        {
            SetDisplayed(_dragGhost, false);
            return;
        }

        _dragGhost.text = label;
        _dragGhost.style.left = panelPosition.x + 15f;
        _dragGhost.style.top = panelPosition.y + 15f;
        SetDisplayed(_dragGhost, true);
    }

    public void Dispose()
    {
        EndRosterDrag();
        _primary.clicked -= _onPrimary;
        _drawerToggle.clicked -= _onToggleDrawer;
        _undo.clicked -= _onUndo;
        _redo.clicked -= _onRedo;
        _rosterAction.clicked -= _onRosterAction;
        _boardSurface.UnregisterCallback(_onBoardDown);
        _boardSurface.UnregisterCallback(_onBoardMove);
        _boardSurface.UnregisterCallback(_onBoardUp);
    }

    private void RenderPlanning(SkirmishScreenModel model)
    {
        _drawer.EnableInClassList("drawer--collapsed", !model.DrawerOpen);
        _drawerBody.pickingMode = model.DrawerOpen ? PickingMode.Position : PickingMode.Ignore;
        _drawerChevron.text = model.DrawerOpen ? "▼" : "▲";
        _drawerCount.text =
            $"{model.FieldCount}/{model.FieldCapacity} FIELD   •   " +
            $"{model.BenchCount}/{model.BenchCapacity} RESERVE";
        _undo.SetEnabled(model.CanUndo);
        _redo.SetEnabled(model.CanRedo);
        _rosterAction.text = model.RosterActionText;
        _rosterAction.SetEnabled(model.RosterActionEnabled);

        _fieldList.Clear();
        foreach (var hero in model.Heroes)
            if (hero.Zone == PlanningZone.Field)
                _fieldList.Add(BuildHeroCard(hero));

        _benchList.Clear();
        for (int slot = 0; slot < model.BenchCapacity; slot++)
        {
            SkirmishHeroView occupant = null;
            foreach (var hero in model.Heroes)
                if (hero.Zone == PlanningZone.Bench && hero.BenchSlot == slot)
                {
                    occupant = hero;
                    break;
                }

            if (occupant != null)
            {
                _benchList.Add(BuildHeroCard(occupant));
                continue;
            }

            int capturedSlot = slot;
            var empty = new Button(() => _onBenchSlot(capturedSlot))
            {
                text = $"EMPTY  {slot + 1}",
                userData = new DropTargetData
                {
                    Kind = SkirmishRosterDropKind.BenchSlot,
                    BenchSlot = slot,
                },
            };
            empty.AddToClassList("empty-bench-slot");
            _benchList.Add(empty);
        }

        _loadoutHero.text = model.HeroName;
        _loadoutChampion.text = model.ChampionName;
        _loadoutRole.text = model.Role.ToUpperInvariant();

        _statList.Clear();
        foreach (var stat in model.Stats)
        {
            var chip = new MechanicStatTile("stat-chip");
            chip.Bind(new StatChipModel(stat.Name, stat.Value, "",
                DecisionCardPresentation.FactId(stat.Name)));
            _statList.Add(chip);
        }

        _passiveName.text = $"PASSIVE  •  {model.PassiveName}";
        MechanicPresentation.BindInline(_passiveText, model.PassiveText);
        _signatureName.text = $"SIGNATURE  •  {model.SignatureName}";
        MechanicPresentation.BindInline(_signatureText, model.SignatureText);
        _masteryName.text = model.MasteryActive
            ? $"WEAPON MASTERY  •  {model.MasteryName}"
            : $"MASTERY INACTIVE  •  {model.MasteryName}";
        MechanicPresentation.BindInline(_masteryText, model.MasteryText);
        _masteryCard.EnableInClassList("mastery-card--inactive", !model.MasteryActive);

        _weaponList.Clear();
        foreach (var weapon in model.Weapons)
        {
            string weaponId = weapon.Id;
            var button = new Button(() => _onWeapon(weaponId));
            button.AddToClassList("weapon-card");
            button.EnableInClassList("weapon-card--selected", weapon.Selected);

            var title = new Label(
                $"{weapon.Name.ToUpperInvariant()}  •  " +
                $"{(weapon.Mastered ? "MASTERED" : "OFF-LABEL")}");
            title.AddToClassList("weapon-title");
            button.Add(title);

            var summary = new Label();
            summary.AddToClassList("weapon-summary");
            MechanicPresentation.BindInline(summary, weapon.Summary);
            button.Add(summary);

            var trait = new Label();
            trait.AddToClassList("weapon-trait");
            MechanicPresentation.BindInline(trait, weapon.Trait);
            button.Add(trait);
            _weaponList.Add(button);
        }
    }

    private VisualElement BuildHeroCard(SkirmishHeroView hero)
    {
        var card = new VisualElement
        {
            focusable = true,
            tabIndex = 0,
            userData = new DropTargetData
            {
                Kind = SkirmishRosterDropKind.Hero,
                HeroId = hero.Id,
            },
        };
        card.AddToClassList("hero-card");
        card.EnableInClassList("hero-card--selected", hero.Selected);
        card.EnableInClassList("hero-card--reserve", hero.Zone == PlanningZone.Bench);

        var name = new Label(hero.Name);
        name.AddToClassList("hero-card-name");
        card.Add(name);

        var detail = new Label(hero.WeaponName);
        detail.AddToClassList("hero-card-detail");
        card.Add(detail);

        var location = new Label(
            hero.Zone == PlanningZone.Field ? hero.Position : $"RESERVE {hero.BenchSlot + 1}");
        location.AddToClassList("hero-card-location");
        card.Add(location);

        card.RegisterCallback<PointerDownEvent>(evt => BeginRosterDrag(evt, card, hero));
        card.RegisterCallback<PointerMoveEvent>(evt => MoveRosterDrag(evt, card));
        card.RegisterCallback<PointerUpEvent>(evt => EndRosterDrag(evt, card, hero));
        card.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
            _onHero(hero.Id);
            evt.StopPropagation();
        });
        return card;
    }

    private void BeginRosterDrag(
        PointerDownEvent evt,
        VisualElement card,
        SkirmishHeroView hero)
    {
        if (evt.button != 0 || _dragPointerId >= 0) return;
        _dragSourceElement = card;
        _dragSourceHeroId = hero.Id;
        _dragSourceLabel = $"{hero.Name}\n{hero.WeaponName}";
        _dragStart = new Vector2(evt.position.x, evt.position.y);
        _dragPointerId = evt.pointerId;
        _dragging = false;
        card.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void MoveRosterDrag(PointerMoveEvent evt, VisualElement card)
    {
        if (evt.pointerId != _dragPointerId || card != _dragSourceElement) return;
        var position = new Vector2(evt.position.x, evt.position.y);
        if (!_dragging && Vector2.Distance(position, _dragStart) >= DragThreshold)
            _dragging = true;
        if (_dragging)
            SetExternalDragGhost(_dragSourceLabel, position, true);
        evt.StopPropagation();
    }

    private void EndRosterDrag(
        PointerUpEvent evt,
        VisualElement card,
        SkirmishHeroView hero)
    {
        if (evt.pointerId != _dragPointerId || card != _dragSourceElement) return;
        var position = new Vector2(evt.position.x, evt.position.y);
        if (card.HasPointerCapture(evt.pointerId))
            card.ReleasePointer(evt.pointerId);

        var drop = _dragging ? BuildDrop(hero.Id, position) : null;
        EndRosterDrag();
        if (drop != null)
            _onRosterDrop(drop);
        else
            _onHero(hero.Id);
        evt.StopPropagation();
    }

    private SkirmishRosterDrop BuildDrop(string sourceHeroId, Vector2 panelPosition)
    {
        var drop = new SkirmishRosterDrop
        {
            SourceHeroId = sourceHeroId,
            Kind = SkirmishRosterDropKind.Board,
            PanelPosition = panelPosition,
        };

        VisualElement picked = _root.panel?.Pick(panelPosition);
        while (picked != null)
        {
            if (picked.userData is DropTargetData target)
            {
                drop.Kind = target.Kind;
                drop.TargetHeroId = target.HeroId;
                drop.BenchSlot = target.BenchSlot;
                break;
            }
            picked = picked.parent;
        }
        return drop;
    }

    private void EndRosterDrag()
    {
        if (_dragSourceElement != null &&
            _dragPointerId >= 0 &&
            _dragSourceElement.HasPointerCapture(_dragPointerId))
            _dragSourceElement.ReleasePointer(_dragPointerId);

        _dragSourceElement = null;
        _dragSourceHeroId = "";
        _dragSourceLabel = "";
        _dragPointerId = -1;
        _dragging = false;
        SetExternalDragGhost("", Vector2.zero, false);
    }

    private void RenderOutcome(SkirmishScreenModel model)
    {
        _outcome.text = model.Outcome;
        _outcome.EnableInClassList(
            "outcome-label--victory",
            model.OutcomeTone == SkirmishOutcomeTone.Victory);
        _outcome.EnableInClassList(
            "outcome-label--defeat",
            model.OutcomeTone == SkirmishOutcomeTone.Defeat);
        _outcome.EnableInClassList(
            "outcome-label--draw",
            model.OutcomeTone == SkirmishOutcomeTone.Draw);
    }

    private void SetStateClass(SkirmishScreenState state)
    {
        _root.EnableInClassList("state--planning", state == SkirmishScreenState.Planning);
        _root.EnableInClassList("state--playing", state == SkirmishScreenState.Playing);
        _root.EnableInClassList("state--result", state == SkirmishScreenState.Result);
    }

    private static void BuildEnemyList(VisualElement list, EncounterDef encounter)
    {
        list.Clear();
        foreach (var enemy in encounter.Enemies)
        {
            var entry = new VisualElement();
            entry.AddToClassList("enemy-entry");
            var name = new Label(enemy.Def.Name);
            name.AddToClassList("enemy-name");
            entry.Add(name);
            var role = new Label(enemy.Role);
            role.AddToClassList("enemy-role");
            entry.Add(role);
            list.Add(entry);
        }
    }

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null)
            throw new InvalidOperationException(
                $"[Skirmish] UXML element '{name}' ({typeof(T).Name}) is missing.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool displayed)
    {
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
