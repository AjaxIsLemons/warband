using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// Hosts the board-first PvE Planning proof. The controller translates UI intentions into
/// Warband.Run Planning actions; the view never owns draft state and the replay renderer never
/// owns combat rules. Formation, roster, and equipment remain freely editable until Begin Fight.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public sealed class SkirmishController : MonoBehaviour
{
    private const float BoardDragThreshold = 7f;
    private const float UnitPickPadding = 12f;

    // Startup order is owned by GameBoot — see that class before adding one back here.

    private IReadOnlyList<SkirmishHeroDef> _heroes;
    private ReplayPlayer _player;
    private EncounterDef _encounter;
    private BattleResult _result;
    private PlanningSession _session;
    private SkirmishScreenState _flow;
    private SkirmishScreen _screen;
    private PanelSettings _panelSettings;
    private string _selectedHeroId = "";
    private bool _drawerOpen = true;
    private bool _initialized;
    private bool _subscribed;

    private int _boardPointerId = -1;
    private Vector2 _boardStart;
    private string _boardSourceHeroId = "";
    private bool _boardDragging;
    private Hex _boardHoverHex;
    private bool _boardHoverValid;

    private void Start()
    {
        _player = FindFirstObjectByType<ReplayPlayer>();
        if (_player == null)
        {
            Debug.LogError("[Skirmish] A ReplayPlayer is required.");
            enabled = false;
            return;
        }

        _heroes = SkirmishProof.Heroes;
        _encounter = Encounters.BondedPair();
        _session = new PlanningSession(
            SkirmishProof.CreatePlanningDraft(),
            new SkirmishPlanningRules());
        _session.Changed += OnPlanningChanged;
        _selectedHeroId = _session.Current.Heroes[0].Id;

        try
        {
            BuildUI();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            enabled = false;
            return;
        }

        _initialized = true;
        SubscribePlayback();
        EnterPlanning();
    }

    private void OnEnable()
    {
        if (_initialized) SubscribePlayback();
    }

    private void OnDisable()
    {
        CancelBoardDrag(restoreFormation: true);
        UnsubscribePlayback();
    }

    private void OnDestroy()
    {
        if (_session != null) _session.Changed -= OnPlanningChanged;
        _screen?.Dispose();
        if (_panelSettings != null) Destroy(_panelSettings);
    }

    private void SubscribePlayback()
    {
        if (_subscribed || _player == null) return;
        _player.PlaybackEnded += OnPlaybackEnded;
        _subscribed = true;
    }

    private void UnsubscribePlayback()
    {
        if (!_subscribed || _player == null) return;
        _player.PlaybackEnded -= OnPlaybackEnded;
        _subscribed = false;
    }

    private void BuildUI()
    {
        var document = GetComponent<UIDocument>();
        _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        var theme = Resources.Load<ThemeStyleSheet>("DebugTheme");
        if (theme != null) _panelSettings.themeStyleSheet = theme;
        UiPanelProfile.ConfigureShipping(_panelSettings, 800);
        document.panelSettings = _panelSettings;
        document.sortingOrder = 800;

        _screen = new SkirmishScreen(
            document,
            _encounter,
            OnPrimary,
            SelectHero,
            SelectWeapon,
            ToggleDrawer,
            Undo,
            Redo,
            UseRosterAction,
            UseBenchSlot,
            OnRosterDrop,
            OnBoardPointerDown,
            OnBoardPointerMove,
            OnBoardPointerUp,
            OnBoardPointerCancel);
    }

    private void EnterPlanning()
    {
        _flow = SkirmishScreenState.Planning;
        _result = null;
        _drawerOpen = true;
        ShowCurrentFormation();
        RefreshUI();
    }

    private void Fight()
    {
        var commit = _session.Commit();
        if (!commit.Succeeded)
        {
            Debug.LogWarning($"[Skirmish] Commit refused: {commit.Message}");
            foreach (var issue in commit.Validation.Issues)
                if (issue.Severity == PlanningIssueSeverity.Error &&
                    _session.Current.FindHero(issue.SubjectId) != null)
                {
                    _selectedHeroId = issue.SubjectId;
                    break;
                }
            _drawerOpen = true;
            ShowPlanningSelection();
            RefreshUI();
            return;
        }

        _flow = SkirmishScreenState.Playing;
        _result = new Battle(BuildUnits(commit.Draft), seed: 20260724).Run();
        RefreshUI();
        _player.PlayBattle(_result);
    }

    private void OnPlaybackEnded()
    {
        if (_flow != SkirmishScreenState.Playing) return;
        _flow = SkirmishScreenState.Result;
        RefreshUI();
    }

    private void OnPrimary()
    {
        if (_flow == SkirmishScreenState.Planning)
            Fight();
        else if (_flow == SkirmishScreenState.Result)
            EnterPlanning();
    }

    private void SelectHero(string heroId)
    {
        if (_flow != SkirmishScreenState.Planning ||
            _session.Current.FindHero(heroId) == null)
            return;

        _selectedHeroId = heroId;
        _drawerOpen = true;
        ShowPlanningSelection();
        RefreshUI();
    }

    private void SelectWeapon(string weaponId)
    {
        if (_flow != SkirmishScreenState.Planning) return;
        Execute(new SetPlanningLoadoutOptionAction(
            _selectedHeroId,
            "weapon",
            weaponId));
    }

    private void ToggleDrawer()
    {
        if (_flow != SkirmishScreenState.Planning) return;
        _drawerOpen = !_drawerOpen;
        RefreshUI();
    }

    private void Undo()
    {
        if (_flow != SkirmishScreenState.Planning || !_session.Undo()) return;
        RefreshUI();
    }

    private void Redo()
    {
        if (_flow != SkirmishScreenState.Planning || !_session.Redo()) return;
        RefreshUI();
    }

    private void UseRosterAction()
    {
        if (_flow != SkirmishScreenState.Planning) return;
        var selected = SelectedHero();
        if (selected == null) return;

        if (selected.Zone == PlanningZone.Field)
        {
            int empty = FirstEmptyBenchSlot();
            if (empty < 0)
            {
                Fail("The reserve is full.");
                return;
            }
            Execute(new MovePlanningHeroToBenchAction(selected.Id, empty));
            return;
        }

        if (_session.Current.FieldCount >= _session.Current.FieldCapacity)
        {
            Fail("The active lineup is full. Drag this reserve onto a fielded hero to replace them.");
            return;
        }

        if (!TryFindOpenHex(selected.Position, out var destination))
        {
            Fail("No legal deployment hex is open.");
            return;
        }
        Execute(new MovePlanningHeroToFieldAction(selected.Id, destination));
    }

    private void UseBenchSlot(int benchSlot)
    {
        if (_flow != SkirmishScreenState.Planning) return;
        var selected = SelectedHero();
        if (selected == null) return;

        IPlanningAction action = selected.Zone == PlanningZone.Field
            ? new MovePlanningHeroToBenchAction(selected.Id, benchSlot)
            : new MovePlanningBenchHeroAction(selected.Id, benchSlot);
        Execute(action);
    }

    private void OnRosterDrop(SkirmishRosterDrop drop)
    {
        if (_flow != SkirmishScreenState.Planning) return;
        var source = _session.Current.FindHero(drop.SourceHeroId);
        if (source == null) return;
        _selectedHeroId = source.Id;

        if (drop.Kind == SkirmishRosterDropKind.Hero)
        {
            var target = _session.Current.FindHero(drop.TargetHeroId);
            if (target == null || target.Id == source.Id)
            {
                RefreshUI();
                return;
            }

            if (source.Zone != target.Zone)
            {
                string fieldId = source.Zone == PlanningZone.Field ? source.Id : target.Id;
                string benchId = source.Zone == PlanningZone.Bench ? source.Id : target.Id;
                Execute(new SwapFieldBenchPlanningAction(fieldId, benchId));
            }
            else if (source.Zone == PlanningZone.Field)
            {
                Execute(new MovePlanningHeroAction(source.Id, target.Position));
            }
            else
            {
                Execute(new MovePlanningBenchHeroAction(source.Id, target.BenchSlot));
            }
            return;
        }

        if (drop.Kind == SkirmishRosterDropKind.BenchSlot)
        {
            UseBenchSlot(drop.BenchSlot);
            return;
        }

        HandleBoardDrop(source.Id, PanelToScreen(drop.PanelPosition));
    }

    private void OnBoardPointerDown(PointerDownEvent evt)
    {
        if (_flow != SkirmishScreenState.Planning ||
            evt.button != 0 ||
            _boardPointerId >= 0)
            return;

        Vector2 panel = new Vector2(evt.position.x, evt.position.y);
        var picked = _player.PickUnit(
            PanelToScreen(panel),
            UnitPickPadding,
            FieldUnitIds());
        var selected = SelectedHero();
        if (picked != null && picked.Team == 0)
        {
            string pickedHeroId = HeroIdFromUnitId(picked.Id);
            if (selected != null && selected.Zone == PlanningZone.Bench)
                _boardSourceHeroId = selected.Id;
            else
            {
                _boardSourceHeroId = pickedHeroId;
                _selectedHeroId = pickedHeroId;
                ShowPlanningSelection();
                RefreshUI();
            }
        }
        else
        {
            _boardSourceHeroId = selected?.Id ?? "";
        }

        if (string.IsNullOrEmpty(_boardSourceHeroId)) return;
        _boardPointerId = evt.pointerId;
        _boardStart = panel;
        _boardDragging = false;
        _boardHoverValid = false;
        if (evt.currentTarget is VisualElement surface)
            surface.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnBoardPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != _boardPointerId) return;
        Vector2 panel = new Vector2(evt.position.x, evt.position.y);
        if (!_boardDragging && Vector2.Distance(panel, _boardStart) >= BoardDragThreshold)
            _boardDragging = true;

        if (_boardDragging)
        {
            Vector2 screenPosition = PanelToScreen(panel);
            PlanningHeroState source = _session.Current.FindHero(_boardSourceHeroId);
            bool direct = source != null && source.Zone == PlanningZone.Field;
            if (direct)
                _player.MovePlanningUnit(
                    UnitIdFromHeroId(source.Id), screenPosition, out _);
            _screen.SetExternalDragGhost(
                HeroName(_boardSourceHeroId), panel, !direct);

            bool hasHover = TryResolveBoardHex(
                _boardSourceHeroId, screenPosition, out _boardHoverHex);
            _boardHoverValid = hasHover &&
                (new SkirmishPlanningRules()).IsLegalPosition(_boardHoverHex);
            if (hasHover)
            {
                PlanningHeroState occupant = FieldHeroAt(_boardHoverHex);
                _player.SetPlanningDropTarget(
                    _boardHoverHex,
                    legal: _boardHoverValid,
                    swap: _boardHoverValid &&
                        occupant != null &&
                        occupant.Id != _boardSourceHeroId);
            }
            else
            {
                _player.ClearPlanningDragFeedback();
            }
        }
        evt.StopPropagation();
    }

    private void OnBoardPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != _boardPointerId) return;
        Vector2 panel = new Vector2(evt.position.x, evt.position.y);
        if (evt.currentTarget is VisualElement surface &&
            surface.HasPointerCapture(evt.pointerId))
            surface.ReleasePointer(evt.pointerId);

        _screen.SetExternalDragGhost("", Vector2.zero, false);
        string sourceId = _boardSourceHeroId;
        bool dragged = _boardDragging;
        bool validDrop = _boardHoverValid;
        Hex dropHex = _boardHoverHex;
        _boardPointerId = -1;
        _boardSourceHeroId = "";
        _boardDragging = false;
        _boardHoverValid = false;
        _player.ClearPlanningDragFeedback();
        if (dragged)
        {
            if (validDrop) HandleBoardDropAtHex(sourceId, dropHex);
            else
            {
                ShowCurrentFormation();
                Fail("Choose one of the three blue deployment rows.");
            }
        }
        else HandleBoardDrop(sourceId, PanelToScreen(panel));
        evt.StopPropagation();
    }

    private void OnBoardPointerCancel(PointerCancelEvent evt)
    {
        if (evt.pointerId != _boardPointerId) return;
        CancelBoardDrag(restoreFormation: true);
        evt.StopPropagation();
    }

    private void CancelBoardDrag(bool restoreFormation)
    {
        bool movedFieldUnit = _boardDragging &&
            _session?.Current.FindHero(_boardSourceHeroId)?.Zone == PlanningZone.Field;
        _screen?.SetExternalDragGhost("", Vector2.zero, false);
        _player?.ClearPlanningDragFeedback();
        _boardPointerId = -1;
        _boardSourceHeroId = "";
        _boardDragging = false;
        _boardHoverValid = false;
        if (restoreFormation && movedFieldUnit)
            ShowCurrentFormation();
    }

    private void HandleBoardDrop(string sourceHeroId, Vector2 screenPosition)
    {
        var source = _session.Current.FindHero(sourceHeroId);
        if (source == null) return;
        _selectedHeroId = source.Id;

        if (TryResolveBoardHex(sourceHeroId, screenPosition, out Hex hex))
        {
            HandleBoardDropAtHex(sourceHeroId, hex);
            return;
        }

        Fail("Choose one of the three blue deployment rows.");
    }

    private void HandleBoardDropAtHex(string sourceHeroId, Hex hex)
    {
        PlanningHeroState source = _session.Current.FindHero(sourceHeroId);
        if (source == null) return;
        _selectedHeroId = source.Id;
        if (!(new SkirmishPlanningRules()).IsLegalPosition(hex))
        {
            ShowCurrentFormation();
            Fail("Choose one of the three blue deployment rows.");
            return;
        }

        PlanningHeroState target = FieldHeroAt(hex);
        if (target != null)
        {
            if (target.Id == source.Id)
            {
                ShowCurrentFormation();
                ShowPlanningSelection();
                RefreshUI();
                return;
            }
            if (source.Zone == PlanningZone.Bench)
                Execute(new SwapFieldBenchPlanningAction(target.Id, source.Id));
            else
                Execute(new MovePlanningHeroAction(source.Id, target.Position));
            return;
        }
        if (source.Zone == PlanningZone.Field)
            Execute(new MovePlanningHeroAction(source.Id, hex));
        else
            Execute(new MovePlanningHeroToFieldAction(source.Id, hex));
    }

    private bool TryResolveBoardHex(
        string sourceHeroId,
        Vector2 screenPosition,
        out Hex hex)
    {
        PlaybackUnit picked = _player.PickUnit(
            screenPosition,
            UnitPickPadding,
            FieldUnitIds(sourceHeroId));
        if (picked != null)
        {
            PlanningHeroState target =
                _session.Current.FindHero(HeroIdFromUnitId(picked.Id));
            if (target != null && target.Zone == PlanningZone.Field)
            {
                hex = target.Position;
                return true;
            }
        }
        return _player.TryScreenToHex(screenPosition, out hex);
    }

    private List<int> FieldUnitIds(string exceptHeroId = "")
    {
        var ids = new List<int>();
        foreach (PlanningHeroState hero in _session.Current.Heroes)
            if (hero.Zone == PlanningZone.Field && hero.Id != exceptHeroId)
                ids.Add(UnitIdFromHeroId(hero.Id));
        return ids;
    }

    private PlanningHeroState FieldHeroAt(Hex hex)
    {
        foreach (PlanningHeroState hero in _session.Current.Heroes)
            if (hero.Zone == PlanningZone.Field && hero.Position.Equals(hex))
                return hero;
        return null;
    }

    private PlanningActionResult Execute(IPlanningAction action)
    {
        var result = _session.Execute(action);
        if (!result.Succeeded)
            Debug.LogWarning($"[Skirmish] Planning action refused: {result.Message}");
        RefreshUI();
        return result;
    }

    private void OnPlanningChanged()
    {
        if (_flow != SkirmishScreenState.Planning) return;
        ShowCurrentFormation();
        RefreshUI();
    }

    private void ShowCurrentFormation()
    {
        _player.ShowSnapshot(BuildUnits(_session.Current).ConvertAll(PlaybackUnit.From));
        ShowPlanningSelection();
    }

    private void ShowPlanningSelection()
    {
        if (_player == null || _session == null) return;
        var selected = SelectedHero();
        _player.SetPlanningSelection(
            selected != null && selected.Zone == PlanningZone.Field
                ? UnitIdFromHeroId(selected.Id)
                : -1);
    }

    private List<UnitState> BuildUnits(PlanningDraft draft)
    {
        var units = new List<UnitState>();
        foreach (var hero in draft.Heroes)
            if (hero.Zone == PlanningZone.Field)
                units.Add(Loadout.Spawn(
                    UnitIdFromHeroId(hero.Id),
                    0,
                    ComposeHero(hero),
                    hero.Position));

        for (int i = 0; i < _encounter.Enemies.Count; i++)
        {
            var enemy = _encounter.Enemies[i];
            units.Add(UnitState.Spawn(100 + i, 1, enemy.Def, enemy.Pos));
        }
        return units;
    }

    private ComposedLoadout ComposeHero(PlanningHeroState hero)
    {
        var chassis = Kits.Chassis[hero.ContentId];
        var weapon = Weapons.All[hero.Loadout["weapon"]];
        return Loadout.Compose(chassis, weapon, mastered: IsMastered(chassis, weapon));
    }

    private void RefreshUI()
    {
        _screen.Render(BuildScreenModel());
    }

    private SkirmishScreenModel BuildScreenModel()
    {
        var model = new SkirmishScreenModel
        {
            State = _flow,
            EncounterName = _encounter.Name,
            Pressure = _encounter.Pressure,
            Rule = $"{_encounter.RuleName}  •  {_encounter.RuleText}",
        };

        switch (_flow)
        {
            case SkirmishScreenState.Planning:
                model.Step = "PLANNING";
                model.Instruction =
                    "Click a champion to inspect · drag directly to a highlighted hex · Begin Fight commits.";
                model.PrimaryText = "BEGIN FIGHT";
                model.PrimaryEnabled = true;
                model.DrawerOpen = _drawerOpen;
                model.FieldCount = _session.Current.FieldCount;
                model.FieldCapacity = _session.Current.FieldCapacity;
                model.BenchCount = _session.Current.BenchCount;
                model.BenchCapacity = _session.Current.BenchCapacity;
                model.CanUndo = _session.CanUndo;
                model.CanRedo = _session.CanRedo;
                BuildHeroViews(model);
                BuildSelectedHeroModel(model);
                break;
            case SkirmishScreenState.Playing:
                model.Step = "COMBAT";
                model.Instruction = "Planning is locked while the deterministic battle resolves.";
                model.PrimaryText = "AUTOBATTLE RUNNING";
                model.PrimaryEnabled = false;
                break;
            case SkirmishScreenState.Result:
                model.Step = "RESULT";
                model.Instruction = "The last draft is preserved for another attempt.";
                model.PrimaryText = "PLAN ANOTHER ATTEMPT";
                model.PrimaryEnabled = true;
                BuildResultModel(model);
                break;
        }
        return model;
    }

    private void BuildHeroViews(SkirmishScreenModel model)
    {
        foreach (var hero in _session.Current.Heroes)
        {
            var definition = SkirmishProof.Hero(hero.ContentId);
            var weapon = Weapons.All[hero.Loadout["weapon"]];
            model.Heroes.Add(new SkirmishHeroView
            {
                Id = hero.Id,
                Name = Kits.Chassis[hero.ContentId].Name,
                ChampionName = definition?.ChampionName ?? "",
                Role = definition?.Role ?? "",
                Position = $"ROW {hero.Position.Row + 1}  •  COL {hero.Position.Col + 1}",
                WeaponName = weapon.Name,
                Zone = hero.Zone,
                BenchSlot = hero.BenchSlot,
                Selected = hero.Id == _selectedHeroId,
            });
        }
    }

    private void BuildSelectedHeroModel(SkirmishScreenModel model)
    {
        var hero = SelectedHero();
        if (hero == null) return;
        var definition = SkirmishProof.Hero(hero.ContentId);
        if (definition == null) return;

        var chassis = Kits.Chassis[hero.ContentId];
        var selectedWeapon = Weapons.All[hero.Loadout["weapon"]];
        var loadout = ComposeHero(hero).Def;
        bool masteryActive = IsMastered(chassis, selectedWeapon);
        var mastery = SkirmishProof.MasteryCopy[selectedWeapon.Category];

        model.HeroName = chassis.Name;
        model.ChampionName = definition.ChampionName;
        model.Role = definition.Role;
        model.PassiveName = definition.PassiveName;
        model.PassiveText = definition.PassiveText;
        model.SignatureName = definition.SignatureName;
        model.SignatureText = definition.SignatureText;
        model.MasteryName = mastery.Name;
        model.MasteryActive = masteryActive;
        model.MasteryText = masteryActive
            ? mastery.Text
            : $"{mastery.Text} Inactive on this Worn off-label weapon.";

        model.Stats.Add(Stat("HP", loadout.MaxHp.ToString()));
        model.Stats.Add(Stat(loadout.HealAutos ? "HEAL" : "BASE ATK", loadout.Attack.ToString()));
        model.Stats.Add(Stat("RANGE", loadout.Range.ToString()));
        model.Stats.Add(Stat("SPEED", $"{10f / loadout.AttackInterval:0.00}/s"));
        model.Stats.Add(Stat("MANA", loadout.ManaMax.ToString()));

        foreach (string weaponId in definition.WeaponIds)
        {
            var weapon = Weapons.All[weaponId];
            bool mastered = IsMastered(chassis, weapon);
            var copy = SkirmishProof.MasteryCopy[weapon.Category];
            string verb = weapon.HealAutos ? "HEAL" : "ATK";
            string shape = weapon.CleavePct > 0 ? $" • {weapon.CleavePct}% CLEAVE" : "";
            model.Weapons.Add(new SkirmishWeaponView
            {
                Id = weaponId,
                Name = weapon.Name,
                Summary =
                    $"{verb} {weapon.Damage}  •  {10f / weapon.Interval:0.00}/s  •  " +
                    $"RANGE {weapon.Range}{shape}",
                Trait = mastered
                    ? $"{copy.Name}: {copy.Text}"
                    : "Base attack profile only; mastery is inactive at Worn.",
                Mastered = mastered,
                Selected = weaponId == hero.Loadout["weapon"],
            });
        }

        if (hero.Zone == PlanningZone.Field)
        {
            model.RosterActionText = "MOVE TO RESERVE";
            model.RosterActionEnabled =
                FirstEmptyBenchSlot() >= 0 &&
                _session.Current.FieldCount > _session.Current.MinimumFieldCount;
        }
        else
        {
            model.RosterActionText =
                _session.Current.FieldCount < _session.Current.FieldCapacity
                    ? "ADD TO FIELD"
                    : "DRAG TO REPLACE";
            model.RosterActionEnabled =
                _session.Current.FieldCount < _session.Current.FieldCapacity;
        }
    }

    private void BuildResultModel(SkirmishScreenModel model)
    {
        if (_result == null) return;
        switch (_result.Winner)
        {
            case Winner.Team0:
                model.Outcome = $"VICTORY\nResolved at {_result.EndTick / 10f:0.0}s";
                model.OutcomeTone = SkirmishOutcomeTone.Victory;
                break;
            case Winner.Team1:
                model.Outcome = $"DEFEAT\nResolved at {_result.EndTick / 10f:0.0}s";
                model.OutcomeTone = SkirmishOutcomeTone.Defeat;
                break;
            default:
                model.Outcome = $"DRAW\nResolved at {_result.EndTick / 10f:0.0}s";
                model.OutcomeTone = SkirmishOutcomeTone.Draw;
                break;
        }
    }

    private PlanningHeroState SelectedHero() =>
        _session?.Current.FindHero(_selectedHeroId);

    private int FirstEmptyBenchSlot()
    {
        for (int slot = 0; slot < _session.Current.BenchCapacity; slot++)
        {
            bool occupied = false;
            foreach (var hero in _session.Current.Heroes)
                if (hero.Zone == PlanningZone.Bench && hero.BenchSlot == slot)
                {
                    occupied = true;
                    break;
                }
            if (!occupied) return slot;
        }
        return -1;
    }

    private bool TryFindOpenHex(Hex preferred, out Hex destination)
    {
        if (IsOpenFieldHex(preferred))
        {
            destination = preferred;
            return true;
        }

        for (int row = 0; row <= 2; row++)
            for (int col = 0; col < Battle.BoardCols; col++)
            {
                var candidate = Hex.FromRowCol(row, col);
                if (IsOpenFieldHex(candidate))
                {
                    destination = candidate;
                    return true;
                }
            }
        destination = default;
        return false;
    }

    private bool IsOpenFieldHex(Hex position)
    {
        if (!Battle.InBounds(position) || position.Row > 2) return false;
        foreach (var hero in _session.Current.Heroes)
            if (hero.Zone == PlanningZone.Field && hero.Position == position)
                return false;
        return true;
    }

    private int UnitIdFromHeroId(string heroId)
    {
        for (int i = 0; i < _heroes.Count; i++)
            if (_heroes[i].HeroId == heroId)
                return i;
        return -1;
    }

    private string HeroIdFromUnitId(int unitId) =>
        unitId >= 0 && unitId < _heroes.Count ? _heroes[unitId].HeroId : "";

    private string HeroName(string heroId)
    {
        var hero = _session.Current.FindHero(heroId);
        return hero == null ? "Hero" : Kits.Chassis[hero.ContentId].Name;
    }

    private Vector2 PanelToScreen(Vector2 panelPosition)
    {
        float panelWidth = _screen?.PanelWidth ?? Screen.width;
        float panelHeight = _screen?.PanelHeight ?? Screen.height;
        float x = panelPosition.x * Screen.width / Mathf.Max(1f, panelWidth);
        float y = Screen.height -
                  panelPosition.y * Screen.height / Mathf.Max(1f, panelHeight);
        return new Vector2(x, y);
    }

    private void Fail(string message)
    {
        Debug.LogWarning($"[Skirmish] Planning action refused: {message}");
    }

    private static bool IsMastered(ChassisDef chassis, WeaponDef weapon) =>
        chassis.Specializations.Contains(weapon.Category);

    private static SkirmishStatView Stat(string name, string value) =>
        new SkirmishStatView { Name = name, Value = value };

#if UNITY_EDITOR
    /// <summary>Editor-only seam for the committed MCP playtest bridge. Not part of player API.</summary>
    public string EditorStateSummary()
    {
        if (_session == null) return "flow=initializing";
        var loadouts = new List<string>();
        var roster = new List<string>();
        foreach (var hero in _session.Current.Heroes)
        {
            loadouts.Add($"{HeroName(hero.Id)}={Weapons.All[hero.Loadout["weapon"]].Name}");
            roster.Add(
                hero.Zone == PlanningZone.Field
                    ? $"{hero.Id}@r{hero.Position.Row}c{hero.Position.Col}"
                    : $"{hero.Id}@bench{hero.BenchSlot}");
        }
        string result = _result == null ? "none" : $"{_result.Winner}@{_result.EndTick}";
        return
            $"flow={_flow}; selected={_selectedHeroId}; drawer={_drawerOpen}; " +
            $"roster={string.Join(",", roster)}; loadouts={string.Join(",", loadouts)}; " +
            $"undo={_session.CanUndo}; redo={_session.CanRedo}; result={result}";
    }

    public void EditorAdvance()
    {
        OnPrimary();
    }

    public bool EditorSelectWeapon(int heroIndex, string weaponId)
    {
        if (_flow != SkirmishScreenState.Planning ||
            heroIndex < 0 ||
            heroIndex >= _heroes.Count)
            return false;

        string heroId = _heroes[heroIndex].HeroId;
        _selectedHeroId = heroId;
        return Execute(new SetPlanningLoadoutOptionAction(
            heroId,
            "weapon",
            weaponId)).Succeeded;
    }

    public bool EditorPlaceHero(int heroIndex, int row, int col)
    {
        if (_flow != SkirmishScreenState.Planning ||
            heroIndex < 0 ||
            heroIndex >= _heroes.Count)
            return false;

        var hero = _session.Current.FindHero(_heroes[heroIndex].HeroId);
        var hex = Hex.FromRowCol(row, col);
        if (hero == null) return false;
        _selectedHeroId = hero.Id;
        return Execute(
            hero.Zone == PlanningZone.Field
                ? (IPlanningAction)new MovePlanningHeroAction(hero.Id, hex)
                : new MovePlanningHeroToFieldAction(hero.Id, hex)).Succeeded;
    }

    public bool EditorSwapReserve(int fieldHeroIndex, int reserveHeroIndex)
    {
        if (_flow != SkirmishScreenState.Planning ||
            fieldHeroIndex < 0 ||
            fieldHeroIndex >= _heroes.Count ||
            reserveHeroIndex < 0 ||
            reserveHeroIndex >= _heroes.Count)
            return false;

        string fieldId = _heroes[fieldHeroIndex].HeroId;
        string reserveId = _heroes[reserveHeroIndex].HeroId;
        _selectedHeroId = reserveId;
        return Execute(new SwapFieldBenchPlanningAction(fieldId, reserveId)).Succeeded;
    }

    public void EditorToggleDrawer() => ToggleDrawer();

    public bool EditorUndo()
    {
        if (_flow != SkirmishScreenState.Planning || !_session.CanUndo) return false;
        Undo();
        return true;
    }

    public int EditorPreviewEnrage()
    {
        if (_result == null) return -1;
        foreach (var battleEvent in _result.Events)
            if (battleEvent.Kind == EventKind.StatusApplied &&
                battleEvent.Aux == (int)StatusKind.Haste &&
                battleEvent.Amount == Encounters.BondHaste &&
                (battleEvent.Target == 100 || battleEvent.Target == 101))
            {
                _player.BuildLoadedPreview(battleEvent.Tick);
                return battleEvent.Tick;
            }
        return -1;
    }
#endif
}
