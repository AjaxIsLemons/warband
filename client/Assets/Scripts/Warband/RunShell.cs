using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The run shell: Menu → Recruit → Map → Fight → Shop → … → Victory/Defeat → Menu.
///
/// Same law as the Planning screen — this controller owns ALL state and translates view intents
/// into Warband.Run calls; views render a plain model and can reach nothing. It is also the only
/// place content ids are turned into words: every string handed to a view is hydrated through
/// Warband.Sim.Lexicon / Warband.Content.ContentLexicon, so `bulwark.warden` can never surface.
///
/// Extending: add a RunScreen case, a view implementing IRunScreenView, register it in
/// BuildViews, and fill its model in Rebuild. The router itself never changes.
/// </summary>
[DefaultExecutionOrder(90)]
[RequireComponent(typeof(UIDocument))]
public sealed class RunShell : MonoBehaviour
{
    // Startup order is owned by GameBoot — see that class before adding one back here.

    private readonly RunShellActions _actions = new RunShellActions();
    private readonly RunShellModel _model = new RunShellModel();
    private readonly List<IRunScreenView> _views = new List<IRunScreenView>();
    private IRunScreenView _activeView;

    // Held as the INTERFACE, not as Catalog: the content boundary (ADR 0008) is the whole point,
    // and Catalog implements some members explicitly so they are only reachable this way.
    private IRunContent _content;
    private PresentationCatalog _presentation;
    private RunConfig _cfg;
    private RunController _run;
    // Item 19: line formatter (pure, Warband.Run) + byte owner (client). Null until a run exists;
    // every hook goes through LogLine/LogFight, which are fail-silent — telemetry must never
    // break a purchase or a fight.
    private RunTelemetry _telemetry;
    private RunTelemetryWriter _telemetryWriter;
    private string _telemetryPhase = "";
    private ReplayPlayer _player;
    private HallEnvironmentController _hallEnvironment;
    private PanelSettings _panelSettings;
    private VisualElement _root;
    private VisualElement _safeAreaFrame;
    private VisualElement _screenHost;
    private VisualElement _rotationGuard;
    private UiEnvironment _uiEnvironment;
    private VisualElement _fightOverlay;
    private VisualElement _fightHitSurface;
    private Label _fightHint;
    private Button _fightSkip;
    private Button _fightOptions;
    private VisualElement _fightCard, _fightCardRing;
    private string _deployEnemyKey = "";
    private InspectorPanel _fightInspector;
    private ResultGateView _resultGateView;
    private RevisionCombatOverlay _revisionCombatOverlay;
    private readonly RevisionCombatModel _revisionCombat = new RevisionCombatModel();
    private WarbandBarView _warbandBarView;
    private InscriptionRailView _inscriptionRail;
    private OptionsPanel _optionsPanel;
    private InscriptionIndicatorLayer _railIndicators;
    private RuntimeTooltipService _runtimeTooltips;
    private PlaybackUnit _fightInspectedUnit;
    private BattleResult _lastBattle;
    private FightOutcome _lastFightOutcome;
    private PreparedFight _preparedFight;
    private RunMutationSnapshot _pendingFightBefore;
    private EncounterBrief _pendingFightBrief;
    private NodeKind _pendingFightKind;
    private int _revisionPresentTick = -1;
    private int _revisionBranchTick = -1;
    private int _revisionFatalTick = -1;
    private readonly List<int> _revisionTargetIds = new List<int>();
    private TuningConfig _tuning;
    private bool _revisionOpenedOnce;
    private Coroutine _revisionCeremony;
    private Coroutine _revisionScrub;
    private float _revisionScrubClock = -1f;   // where the held board is actually rendered
    /// <summary>Wall-clock backstop so a stalled playhead can never strand the ceremony's run-up.
    /// Generous on purpose: the run-up rides battle speed, which the player owns.</summary>
    private const float RevisionRunUpGuardSeconds = 30f;
    /// <summary>How long the Hourstone must be held before the Hour splits.</summary>
    private const float RevisionHoldSeconds = 0.45f;
    private float _revisionHold;
    /// <summary>Set by the layout fixture, which has no real battle to resolve targets against and
    /// would otherwise have its cluster cleared by the next frame's live refresh.</summary>
    private bool _revisionFixtureCluster;
    private HubSequencePlan _pendingHubPlan = new HubSequencePlan();
    private readonly HubAttentionModel _hubAttention = new HubAttentionModel();
    private RunConclusionReceipt _conclusionReceipt;
    private bool _resultGateOpen;
    private bool _hallOverview;
    private HallStation _recommendedStation = HallStation.Breach;
    private int _fightsCompleted;
    private bool _reducedMotion;
    private VisualElement _flowLab;
    private bool _flowLabVisible;
    private bool _debugPhoneLayout;

    private ulong _seed;
    /// <summary>The exact text last written to disk. Autosave compares against it so an idle
    /// Rebuild (a hover, a selection) does not rewrite an unchanged file.</summary>
    private string _savedText = "";
    private List<string> _offer = new List<string>();
    private readonly List<string> _picked = new List<string>();
    /// <summary>Pre-run muster state (workbench-frame): the workbench IS the muster screen.
    /// True from NEW RUN until the first revision is bound; while true the market shows the
    /// candidate offer and no run state is read.</summary>
    private bool _muster;
    /// <summary>BEGIN RUN pressed with a full muster: the choice scrim presents the first
    /// revision (beat #0) over the muster state until one is bound.</summary>
    private bool _pendingFirstRevision;
    /// <summary>Synthetic WarbandHeroModel ids for muster candidates (no HeroInstance exists
    /// yet). Slot index = id - base. Positive so the rail renders full progression cards.</summary>
    private const long MusterHeroIdBase = 900_000_000;
    private FightTier _tier = FightTier.Fraying;
    private bool _tierChosen;
    private PlanningTab _planningTab = PlanningTab.Market;
    private string _selectedCardKey = "";
    private bool _inspectorOpen;
    private bool _loadoutOpen;
    private string _loadoutReturnCardKey = "";
    private int _loadoutReturnMarketOffer = -1;
    private int _selectedMarketOffer = -1;
    private string _comparisonTargetHeroKey = "";
    private long _equipNowItemInstanceId;
    private int _equipNowOfferIndex = -1;

    // Deployment: hexes chosen for each FIELD index. Kept here, not in the view, and cleared on
    // every entry so a formation can never leak from one fight into the next.
    private readonly Dictionary<int, Hex> _placement = new Dictionary<int, Hex>();
    private const float DeploymentDragThreshold = 7f;
    private int _deploySelected = -1;
    private int _deployPointerId = -1;
    private int _deployPointerUnit = -1;
    private Vector2 _deployPointerStart;
    private bool _deployDragging;
    private Hex _deployHoverHex;
    private bool _deployHoverValid;
    private int _selectedItem = -1;
    private long _focusedWarbandHeroId;
    private long _selectedWarbandGearHeroId;
    private int _selectedWarbandGearKind = -1;
    private bool _started;

    private void Start()
    {
        if (_started) return;
        _started = true;
        _content = new Catalog();
        _presentation = PresentationCatalog.Load();
        _cfg = new RunConfig();
        _player = FindFirstObjectByType<ReplayPlayer>();
        _tuning = FindFirstObjectByType<TuningConfig>();
        _reducedMotion = PlayerPrefs.GetInt("ui.reducedMotion", 0) != 0;
        PlayerOptions.ApplyAudio();   // mixer params reset per session; re-assert the player's
        _hallEnvironment = HallEnvironmentController.Create(Camera.main,
            HubPresentationConfig.Load());

        // The board belongs to the shell now, and the menu is not a fight: park it empty rather
        // than letting a leftover fixture loop behind the front end.
        if (_player != null) _player.Idle();
        // Item 5b fight bridge: the board republishes law events (TriggerFired/RuleProgress) at
        // dispatch; the shell routes them into the persistent tray. Subscribed once for the
        // shell's whole life — the handler itself ignores events outside the Fight screen.
        if (_player != null) _player.LawDispatched += OnLawDispatched;

        NewSeed();
        WireActions();
        BuildUI();
        Rebuild();
    }

    private void OnDestroy()
    {
        SfxPlayer.StopRevisionLoop();
        RevisionScreenEffect.Clear();
        if (_player != null) _player.PlaybackEnded -= OnFightWatched;   // never outlive the shell
        if (_player != null) _player.RevisionPauseReached -= OnRevisionFinalChance;
        if (_player != null) _player.LawDispatched -= OnLawDispatched;
        foreach (var view in _views)
            if (view is IDisposable disposable) disposable.Dispose();
        _warbandBarView?.Dispose();
        _revisionCombatOverlay?.Dispose();
        _runtimeTooltips?.Dispose();
        _uiEnvironment?.Dispose();
        if (_hallEnvironment != null) Destroy(_hallEnvironment.gameObject);
        if (_panelSettings != null) Destroy(_panelSettings);
    }

    /// <summary>Item 5b: a law fired on the board — flash its tray icon, update pips, and draw
    /// the indicator line from icon to victim. Filtered to YOUR laws by the rule's owning team
    /// (the id alone cannot say whose it is — a mirror fight registers the same Inscription on
    /// both sides). Coalescing is the tray's own glow-hold; the passive-onset ration already
    /// thinned the stream sim-side for tells, and Flash is idempotent under repeats.</summary>
    private void OnLawDispatched(BattleEvent e)
    {
        if (_inscriptionRail == null || _player == null || _model.Screen != RunScreen.Fight) return;
        if (_player.RuleTeamOf(e.Aux) != 0) return;
        string id = _player.RuleIdOf(e.Aux);
        if (!id.StartsWith("inscription.", StringComparison.Ordinal)) return;
        int hash = id.LastIndexOf('#');
        string key = (hash > 0 ? id.Substring(0, hash) : id).Substring("inscription.".Length);

        if (e.Kind == EventKind.RuleProgress)
        {
            _inscriptionRail.SetPips(key, e.Amount, e.Aux2);
            return;
        }
        _inscriptionRail.Flash(key);
        if (_railIndicators != null && _root?.panel != null &&
            _inscriptionRail.TryIconCenter(key, out Vector2 icon) &&
            _player.TryUnitScreen(e.Target, out Vector2 unitScreen))
        {
            // Camera screen coords are bottom-left origin; panels are top-left. Flip, then map.
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                _root.panel, new Vector2(unitScreen.x, Screen.height - unitScreen.y));
            _railIndicators.Add(icon, panelPos);
        }
    }

    private void Update()
    {
        _inscriptionRail?.Tick(Time.deltaTime);   // item 5b: decay tray pulses
        _railIndicators?.Tick(Time.deltaTime);    // …and fade indicator lines
        if (_revisionCombat.Mode == RevisionCombatMode.Ready && _preparedFight != null &&
            _model.Screen == RunScreen.Fight && _player != null)
        {
            bool canOpen = _player.CurrentTick >= 10 && !_player.IsEnding;
            if (canOpen != _revisionCombat.CanOpen)
            {
                _revisionCombat.CanOpen = canOpen;
                _revisionCombatOverlay?.Bind(_revisionCombat);
            }
        }
        if (_revisionCombat.Mode == RevisionCombatMode.Selecting)
        {
            // The ability rides a body that the scrub walks around the board, so it re-resolves
            // every frame rather than on selection changes alone.
            UpdateRevisionCluster();
            UpdateRevisionHold();
        }
        // Item 9: Escape toggles the options modal on every surface a keyboard reaches. A rare
        // collision is accepted: cancelling an armed keyboard drag with Escape also opens this —
        // harmless, and Escape again closes it.
        var esc = UnityEngine.InputSystem.Keyboard.current;
        if (esc != null && esc.escapeKey.wasPressedThisFrame &&
            !(_revisionCombatOverlay?.ConsumesEscape ?? false) &&
            // The combat card is the most recently opened thing on screen, so it takes Escape
            // first. Otherwise Escape opens Options *behind* an open card, which reads as a bug.
            !FightCardHandledEscape())
            _optionsPanel?.Toggle();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || !keyboard.f2Key.wasPressedThisFrame) return;
        _flowLabVisible = !_flowLabVisible;
        if (_flowLab != null)
            _flowLab.style.display = _flowLabVisible ? DisplayStyle.Flex : DisplayStyle.None;
#endif
    }

    // ---- wiring ------------------------------------------------------------------

    private void NewSeed() => _seed = (ulong)DateTime.Now.Ticks;

    private void WireActions()
    {
        _actions.NewRun = () =>
        {
            _picked.Clear();
            _offer = RunSetup.RecruitOffer(_content, _seed);
            // The workbench IS the muster screen (workbench-frame): pre-run state, same frame.
            _muster = true;
            _pendingFirstRevision = false;
            _selectedCardKey = _offer.Count > 0 ? "muster:" + _offer[0] : "";
            _inspectorOpen = false;
            _loadoutOpen = false;
            Go(RunScreen.Management);
        };
        _actions.ContinueRun = () =>
        {
            // A run already in memory just re-opens. Otherwise this is a cold CONTINUE from disk —
            // the whole point of item 7, since before this the button could only ever mean
            // "the run you never left".
            if (_run != null) { OpenHallOverview(); return; }

            var loaded = RunSaveFile.Load(_content, _cfg, out string problem);
            if (loaded == null)
            {
                Debug.LogWarning($"[RunShell] Continue failed: {problem}");
                Go(RunScreen.Menu);
                return;
            }
            AdoptResumedRun(loaded);
            OpenHallOverview();
        };
        _actions.OpenOptions = () => _optionsPanel?.Open();
        _actions.Quit = () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        };

        _actions.RerollSeed = () =>
        {
            NewSeed();
            _picked.Clear();
            _offer = RunSetup.RecruitOffer(_content, _seed);
            _selectedCardKey = _offer.Count > 0 ? "muster:" + _offer[0] : "";
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reroll,
                sourceId: "action-reroll", targetId: "workbench-market",
                tone: UiFeedbackTone.Preview);
            Rebuild();
        };
        _actions.ToggleRecruit = id =>
        {
            int selectedSlot = _picked.IndexOf(id);
            if (selectedSlot >= 0)
            {
                UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                    sourceId: "muster-slot:" + selectedSlot,
                    targetId: "muster:" + id,
                    tone: UiFeedbackTone.Preview,
                    transaction: UiTransactionKind.MusterDeselect);
                _picked.RemoveAt(selectedSlot);
            }
            else if (_picked.Count < _cfg.StartingFieldSlots)
            {
                int destination = _picked.Count;
                UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                    sourceId: "muster:" + id,
                    targetId: "muster-slot:" + destination,
                    tone: UiFeedbackTone.Positive,
                    transaction: UiTransactionKind.MusterSelect);
                _picked.Add(id);
            }
            else
            {
                UiPolishSignals.Emit(UiPolishSignals.Cue.Error,
                    sourceId: "muster:" + id,
                    targetId: "muster:" + id,
                    tone: UiFeedbackTone.Negative);
            }
            Rebuild();
        };
        _actions.BeginRun = () =>
        {
            if (RunSetup.PicksRemaining(_picked.Count, _cfg) > 0) return;
            // Beat #0: the first-revision choice rises over the muster state — no screen
            // change (workbench-frame).
            _pendingFirstRevision = true;
            Rebuild();
        };
        _actions.ChooseStartingRevision = BeginSelectedRun;

        _actions.ChooseTier = i =>
        {
            _tier = (FightTier)i;
            _tierChosen = true;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Select);
            Rebuild();
        };
        _actions.Advance = () =>
        {
            if (_muster) { _actions.BeginRun?.Invoke(); return; }
            BeginNode();
        };
        _actions.ConfirmWager = ConfirmWager;
        _actions.ReturnToManagement = OpenHallOverview;

        _actions.SetPlanningTab = i =>
        {
            _planningTab = (PlanningTab)Mathf.Clamp(i, 0, 3);
            _hallOverview = false;
            _inspectorOpen = false;
            SelectDefaultForTab();
            UiPolishSignals.Emit(UiPolishSignals.Cue.Tab);
            Rebuild();
        };
        _actions.OpenHallOverview = OpenHallOverview;
        _actions.OpenHallStation = i => OpenHallStation((HallStation)i);
        _actions.OpenLoadout = OpenLoadout;
        _actions.CloseLoadout = CloseLoadout;
        _actions.SelectLoadoutHero = SelectLoadoutHero;
        _actions.SelectLoadoutItem = SelectLoadoutItem;
        _actions.FocusWarbandHero = FocusWarbandHero;
        _actions.ManageWarbandHero = ManageWarbandHero;
        _actions.MoveWarbandHero = MoveWarbandHero;
        _actions.SelectWarbandEquipment = SelectWarbandEquipment;
        _actions.TransferWarbandEquipment = TransferWarbandEquipment;
        _actions.UnequipWarbandEquipment = UnequipWarbandEquipment;
        _actions.EquipSelectedWarbandItem = EquipSelectedWarbandItem;
        _actions.EquipWarbandItem = EquipWarbandItem;
        _actions.SelectPlanningCard = SelectPlanningCard;
        _actions.ActivatePlanningCard = ActivatePlanningCard;
        _actions.SelectComparisonTarget = SelectComparisonTarget;
        _actions.OpenInspector = () =>
        {
            if (string.IsNullOrEmpty(_selectedCardKey)) return;
            _inspectorOpen = true;
            Rebuild();
        };
        _actions.CloseInspector = () =>
        {
            _inspectorOpen = false;
            Rebuild();
        };
        _actions.BuySelectedOffer = BuySelectedOffer;
        _actions.InspectorAction = UseInspectorAction;
        _actions.ChooseInterlude = (path, option) => ResolveInterlude((InterludePath)path, option);
        _actions.ChooseRevisionUpgrade = ChooseRevisionUpgrade;
        _actions.ChooseBossReward = ChooseBossReward;
        _actions.ChooseEndless = ChooseEndless;
        _actions.WatchFightAgain = WatchFightAgain;
        _actions.ContinueFightResult = ContinueFightResult;

        _actions.BuyOffer = i =>
        {
            _selectedMarketOffer = i;
            _selectedCardKey = $"market:{i}";
            Rebuild();
        };
        _actions.ToggleFreeze = i => ShopAction(() => _run.ToggleFreeze(i));
        _actions.Reroll = () =>
        {
            // Muster: the rail rerolls fate itself — new seed, new candidates, free.
            if (_muster) { _actions.RerollSeed?.Invoke(); return; }
            ShopAction(() =>
            {
                int beforeSand = _run.State.Sand;
                _run.Reroll();
                UiPolishSignals.Emit(UiPolishSignals.Cue.Reroll,
                    sourceId: "action-secondary", targetId: "hub-workspace",
                    resourceId: "ledger-sand", groupId: "market-offers",
                    amount: _run.State.Sand - beforeSand, tone: UiFeedbackTone.Sand);
                LogLine(() => _telemetry.RerollLine(
                    _run.State, DateTime.UtcNow, beforeSand - _run.State.Sand));
            });
        };
        _actions.ChooseSpec = w =>
        {
            bool succeeded = ShopAction(() =>
            {
                _run.ChooseSpec(w);
                UiPolishSignals.Emit(UiPolishSignals.Cue.RankUp,
                    targetId: "warband-shelf", tone: UiFeedbackTone.Major,
                    transaction: UiTransactionKind.RankChoice);
            });
            if (succeeded) OpenHallStation(HallStation.Market);
        };
        _actions.BuySlot = BuyCapacity;
        _actions.LeaveShop = () => ShopAction(() =>
        {
            _run.LeaveShop();
            Go(_run.State.Over ? RunScreen.RunOver : RunScreen.Management);
        });

        _actions.SelectForDeploy = i =>
        {
            _deploySelected = _deploySelected == i ? -1 : i;
            ShowDeploymentOnBoard();
            Rebuild();
        };
        _actions.SelectDeployEnemy = key =>
        {
            _deployEnemyKey = _deployEnemyKey == key ? "" : (key ?? "");
            Rebuild();
        };
        _actions.ClearDeployment = () =>
        {
            CancelDeploymentGesture(refreshBoard: false);
            _placement.Clear();
            _deploySelected = -1;
            ShowDeploymentOnBoard();
            Rebuild();
        };
        _actions.CommitDeployment = () =>
        {
            if (_placement.Count < _run.State.Field.Count) return;
            CancelDeploymentGesture(refreshBoard: false);
            ResolveCurrentNode();
        };
        _actions.BoardPointerDown = OnDeploymentPointerDown;
        _actions.BoardPointerMoved = OnDeploymentPointerMoved;
        _actions.BoardPointerUp = OnDeploymentPointerUp;
        _actions.BoardPointerCanceled = OnDeploymentPointerCanceled;

        _actions.SelectItem = i =>
        {
            _selectedItem = _selectedItem == i ? -1 : i;
            _selectedCardKey = _selectedItem < 0 ? "" : $"item:{_selectedItem}";
            Rebuild();
        };
        _actions.EquipSelected = (bench, idx) => ShopAction(() =>
        {
            if (_selectedItem < 0) return;
            int itemIndex = _selectedItem;
            var item = _run.State.Inventory[_selectedItem];
            long itemInstanceId = item.InstanceId;
            var zone = bench ? RosterZone.Bench : RosterZone.Field;
            if (item.Kind == ItemKind.Weapon) _run.EquipWeapon(zone, idx, _selectedItem);
            else _run.EquipTrinket(zone, idx, _selectedItem);
            UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                sourceId: $"item:{itemIndex}",
                targetId: $"hero:{(bench ? "bench" : "field")}:{idx}",
                tone: UiFeedbackTone.Positive,
                transaction: UiTransactionKind.Equip);
            if (_equipNowItemInstanceId == itemInstanceId)
            {
                _equipNowItemInstanceId = 0;
                _equipNowOfferIndex = -1;
            }
            _selectedItem = -1;      // the index is stale the moment inventory shifts
        });
        _actions.UnequipWeapon = (bench, idx) =>
            ShopAction(() => _run.UnequipWeapon(bench ? RosterZone.Bench : RosterZone.Field, idx));
        _actions.Reforge = (bench, idx) =>
            ShopAction(() => _run.Reforge(bench ? RosterZone.Bench : RosterZone.Field, idx));
        _actions.SellHero = (bench, idx) => ShopAction(() =>
        {
            var roster = bench ? _run.State.Bench : _run.State.Field;
            string soldId = idx >= 0 && idx < roster.Count ? roster[idx].ChassisId : "";
            _run.SellHero(bench ? RosterZone.Bench : RosterZone.Field, idx);
            _selectedItem = -1;
            LogLine(() => _telemetry.SellLine(_run.State, DateTime.UtcNow, "hero", soldId));
        });
        _actions.SellItem = i => ShopAction(() =>
        {
            string soldId = i >= 0 && i < _run.State.Inventory.Count
                ? _run.State.Inventory[i].Id : "";
            _run.SellItem(i);
            _selectedItem = -1;
            LogLine(() => _telemetry.SellLine(_run.State, DateTime.UtcNow, "item", soldId));
        });
        _actions.MoveHero = (bench, idx) =>
            ShopAction(() => { if (bench) _run.BenchToField(idx); else _run.FieldToBench(idx); });

        _actions.BackToMenu = () =>
        {
            _run = null;
            _resultGateOpen = false;
            _lastBattle = null;
            _lastFightOutcome = null;
            ResetRevisionCombat();
            _hubAttention.Reset();
            _equipNowItemInstanceId = 0;
            _equipNowOfferIndex = -1;
            _focusedWarbandHeroId = 0;
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
            NewSeed();
            Go(RunScreen.Menu);
        };
    }

    private void BeginSelectedRun(string revisionId)
    {
        if (RunSetup.PicksRemaining(_picked.Count, _cfg) > 0) return;
        try
        {
            RevisionCatalog.Get(revisionId);
            // The commitment happens here, not when the draft opens: backing out or closing the
            // game while reading the two laws cannot destroy a previous run.
            RunSaveFile.Delete();
            _savedText = "";
            _run = RunSetup.Begin(_seed, _content, _picked, _cfg, revisionId);
            _telemetryWriter = new RunTelemetryWriter();
            _telemetry = new RunTelemetry(_run.State, Application.version);
            _telemetryPhase = "";
            LogLine(() => _telemetry.StartLine(_run.State, DateTime.UtcNow));
            _planningTab = PlanningTab.Market;
            _selectedMarketOffer = _run.State.ShopOffers.FindIndex(o => o != null);
            _selectedCardKey = _selectedMarketOffer >= 0
                ? $"market:{_selectedMarketOffer}"
                : "";
            _inspectorOpen = false;
            _loadoutOpen = false;
            _loadoutReturnCardKey = "";
            _tierChosen = false;
            _hallOverview = false;
            _recommendedStation = HallStation.Breach;
            _hubAttention.Reset();
            _resultGateOpen = false;
            _lastBattle = null;
            _lastFightOutcome = null;
            _pendingHubPlan = new HubSequencePlan
            {
                RecommendedStation = HallStation.Breach,
            };
            _fightsCompleted = 0;
            _conclusionReceipt = null;
            _equipNowItemInstanceId = 0;
            _equipNowOfferIndex = -1;
            _focusedWarbandHeroId = _run.State.Field[0].InstanceId;
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
            _muster = false;
            _pendingFirstRevision = false;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                targetId: "revision:" + revisionId,
                tone: UiFeedbackTone.Major);
            // Item 31: Muster and the starting Revision already establish the opening build.
            // Beat one therefore enters the confidence wager immediately; the first Workbench
            // visit is earned after the first victory, when 4 starting Sand plus the reward can
            // support a real build decision.
            BeginNode();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RunShell] Could not begin run: {ex.Message}");
            Go(RunScreen.Menu);
        }
    }

    private void OpenHallOverview()
    {
        if (_run == null) return;
        HallStation leaving = TabStation(_planningTab);
        UiPolishSignals.Emit(UiPolishSignals.Cue.Route,
            sourceId: AnchorTarget(leaving), targetId: "hub-workspace",
            tone: UiFeedbackTone.Sand);
        _planningTab = PlanningTab.Market;
        _hallOverview = false;
        _inspectorOpen = false;
        _loadoutOpen = false;
        SelectDefaultForTab();
        Go(RunScreen.Management);
    }

    private void OpenHallStation(HallStation station)
    {
        if (_run == null) return;
        if (station == HallStation.Breach)
        {
            BeginNode();
            return;
        }
        if (station != HallStation.Market && station != HallStation.Warband &&
            station != HallStation.Armory && station != HallStation.Hourstone)
            return;

        HallStation leaving = TabStation(_planningTab);
        string source = _hallOverview ? "station-hourstone" : AnchorTarget(leaving);
        string target = _hallOverview ? StationTarget(station) : AnchorTarget(station);
        UiPolishSignals.Emit(UiPolishSignals.Cue.Route, source, target,
            tone: UiFeedbackTone.Sand);
        _planningTab = StationTab(station);
        if (station != HallStation.Armory) _selectedItem = -1;
        _hallOverview = false;
        _inspectorOpen = false;
        _loadoutOpen = false;
        _hubAttention.Clear(station);
        if (_recommendedStation == station && _run.State.PendingSpec == null &&
            _run.State.Phase != RunPhase.Reward)
            _recommendedStation = HallStation.Breach;
        SelectDefaultForTab();
        Go(RunScreen.Management);
    }

    private void OpenLoadout(string heroKey)
    {
        if (_run == null) return;
        if (!_loadoutOpen)
        {
            _loadoutReturnCardKey = _selectedCardKey;
            _loadoutReturnMarketOffer = _selectedMarketOffer;
        }
        _loadoutOpen = true;
        if (!string.IsNullOrEmpty(heroKey) &&
            TryHeroAddress(heroKey, out _, out _))
            _selectedCardKey = heroKey;
        else if (!TryHeroAddress(_selectedCardKey, out _, out _))
            _selectedCardKey = _run.State.Field.Count > 0
                ? "hero:field:0"
                : _run.State.Bench.Count > 0 ? "hero:bench:0" : "";
        _inspectorOpen = false;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select,
            targetId: "warband-shelf", tone: UiFeedbackTone.Preview);
        Rebuild();
    }

    private void CloseLoadout()
    {
        if (!_loadoutOpen) return;
        _loadoutOpen = false;
        _selectedCardKey = _loadoutReturnCardKey;
        _selectedMarketOffer = _loadoutReturnMarketOffer;
        _loadoutReturnCardKey = "";
        _loadoutReturnMarketOffer = -1;
        _selectedItem = _planningTab == PlanningTab.Armory &&
                        TrySimpleIndex(_selectedCardKey, "item", out var item)
            ? item
            : -1;
        Rebuild();
    }

    private void SelectLoadoutHero(string key)
    {
        if (!_loadoutOpen || !TryHeroAddress(key, out _, out _)) return;
        _selectedCardKey = key;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select,
            targetId: key, tone: UiFeedbackTone.Preview);
        Rebuild();
    }

    private void SelectLoadoutItem(string key)
    {
        if (!_loadoutOpen || !TrySimpleIndex(key, "item", out var index) ||
            index < 0 || index >= _run.State.Inventory.Count)
            return;
        _selectedItem = index;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select,
            targetId: "loadout-" + key, tone: UiFeedbackTone.Preview);
        Rebuild();
    }

    private void FocusWarbandHero(long heroInstanceId)
    {
        if (_muster)
        {
            // Rail cards carry synthetic ids at muster — focusing one inspects its candidate.
            int slot = (int)(heroInstanceId - MusterHeroIdBase);
            if (slot < 0 || slot >= _picked.Count) return;
            SelectPlanningCard("muster:" + _picked[slot]);
            return;
        }
        if (_run == null ||
            !_run.TryFindHero(heroInstanceId, out RosterZone zone, out int index))
            return;
        _focusedWarbandHeroId = heroInstanceId;
        if (_model.Screen == RunScreen.Deploy)
        {
            if (zone == RosterZone.Field) _actions.SelectForDeploy?.Invoke(index);
            return;
        }
        if (_model.Screen == RunScreen.Management)
        {
            string key = HeroKey(zone, index);
            _selectedCardKey = key;
            _planningTab = PlanningTab.Muster;
            _hallOverview = false;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Select,
                targetId: key, tone: UiFeedbackTone.Preview);
            Rebuild();
            return;
        }
        Rebuild();
    }

    private void ManageWarbandHero(long heroInstanceId)
    {
        if (_run == null) return;
        if (heroInstanceId <= 0 ||
            !_run.TryFindHero(heroInstanceId, out RosterZone zone, out int index))
        {
            HeroInstance first = _run.State.Field.FirstOrDefault() ??
                                 _run.State.Bench.FirstOrDefault();
            if (first == null) return;
            heroInstanceId = first.InstanceId;
            _run.TryFindHero(heroInstanceId, out zone, out index);
        }
        _focusedWarbandHeroId = heroInstanceId;
        if (_model.Screen != RunScreen.Management || _hallOverview ||
            _planningTab != PlanningTab.Muster)
            OpenHallStation(HallStation.Warband);
        string key = HeroKey(zone, index);
        _selectedCardKey = key;
        OpenLoadout(key);
    }

    private void SelectWarbandEquipment(long heroInstanceId, int kind)
    {
        if (!CanEditWarband() || (kind != (int)ItemKind.Weapon &&
                                  kind != (int)ItemKind.Trinket))
            return;
        if (!_run.TryFindHero(heroInstanceId, out _, out _)) return;
        if (_selectedWarbandGearHeroId == heroInstanceId &&
            _selectedWarbandGearKind == kind)
        {
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
        }
        else
        {
            _selectedWarbandGearHeroId = heroInstanceId;
            _selectedWarbandGearKind = kind;
            _selectedItem = -1;
        }
        Rebuild();
    }

    private void MoveWarbandHero(long heroInstanceId, bool reserve, int slotIndex)
    {
        if (!CanEditWarband()) return;
        RosterZone targetZone = reserve ? RosterZone.Bench : RosterZone.Field;
        bool succeeded = ShopAction(
            () => _run.MoveRosterHero(heroInstanceId, targetZone, slotIndex),
            rebuild: false);
        if (succeeded &&
            _run.TryFindHero(heroInstanceId, out RosterZone resolvedZone, out int resolvedIndex))
        {
            _focusedWarbandHeroId = heroInstanceId;
            _selectedCardKey = HeroKey(resolvedZone, resolvedIndex);
            UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                sourceId: "warband-roster", targetId: _selectedCardKey,
                tone: UiFeedbackTone.Positive);
        }
        Rebuild();
    }

    private void TransferWarbandEquipment(long sourceHeroInstanceId, int kind,
                                          long targetHeroInstanceId)
    {
        if (!CanEditWarband() || (kind != (int)ItemKind.Weapon &&
                                  kind != (int)ItemKind.Trinket))
            return;
        if (ShopAction(() => _run.TransferEquipment(
                sourceHeroInstanceId, (ItemKind)kind, targetHeroInstanceId)))
        {
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
        }
    }

    private void UnequipWarbandEquipment(long heroInstanceId, int kind)
    {
        if (!CanEditWarband() || (kind != (int)ItemKind.Weapon &&
                                  kind != (int)ItemKind.Trinket))
            return;
        if (ShopAction(() => _run.UnequipItem(heroInstanceId, (ItemKind)kind)))
        {
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
        }
    }

    private void EquipSelectedWarbandItem(long heroInstanceId)
    {
        if (!CanEditWarband() || _selectedItem < 0 ||
            _selectedItem >= _run.State.Inventory.Count)
            return;
        long itemInstanceId = _run.State.Inventory[_selectedItem].InstanceId;
        EquipWarbandItem(itemInstanceId, heroInstanceId);
    }

    private void EquipWarbandItem(long itemInstanceId, long heroInstanceId)
    {
        if (!CanEditWarband() || _run.IndexOfItem(itemInstanceId) < 0 ||
            !_run.TryFindHero(heroInstanceId, out _, out _))
            return;
        if (ShopAction(
                () => _run.EquipItem(itemInstanceId, heroInstanceId),
                rebuild: false))
        {
            _selectedItem = -1;
            _selectedWarbandGearHeroId = 0;
            _selectedWarbandGearKind = -1;
        }
        Rebuild();
    }

    private bool CanEditWarband() =>
        _run != null &&
        _model.Screen == RunScreen.Management &&
        _run.State.Phase == RunPhase.Planning &&
        _run.State.PendingSpec == null;

    private static string HeroKey(RosterZone zone, int index) =>
        $"hero:{(zone == RosterZone.Bench ? "bench" : "field")}:{index}";

    private static PlanningTab StationTab(HallStation station) =>
        station == HallStation.Market ? PlanningTab.Market :
        station == HallStation.Armory ? PlanningTab.Armory :
        station == HallStation.Hourstone ? PlanningTab.Hourstone :
        PlanningTab.Muster;

    private static HallStation TabStation(PlanningTab tab) =>
        tab == PlanningTab.Market ? HallStation.Market :
        tab == PlanningTab.Armory ? HallStation.Armory :
        tab == PlanningTab.Hourstone ? HallStation.Hourstone :
        HallStation.Warband;

    /// <summary>
    /// Every shop call can legally refuse (not enough gold, spec choice pending, …). Keep the
    /// mutation boundary interruption-safe and use non-text feedback for refusals.
    /// </summary>
    private bool ShopAction(Action act, bool rebuild = true)
    {
        RunMutationSnapshot before = _run == null ? null : RunMutationSnapshot.Capture(_run.State);
        bool succeeded = false;
        try
        {
            act();
            succeeded = true;
            if (before != null)
            {
                var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
                RecordHubPlan(plan, navigateBlocking: true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Action refused: {ex.Message}");
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "hub-workspace",
                tone: UiFeedbackTone.Negative);
        }
        if (rebuild) Rebuild();
        return succeeded;
    }

    private void RecordHubPlan(HubSequencePlan plan, bool navigateBlocking)
    {
        if (plan == null) return;
        _pendingHubPlan = plan;
        _recommendedStation = plan.RecommendedStation;
        _hubAttention.Apply(plan);
        foreach (var step in plan.Steps)
            if (step.Station != HallStation.Overview)
                UiPolishSignals.Emit(UiPolishSignals.Cue.Attention,
                    targetId: StationTarget(step.Station), tone: UiFeedbackTone.Sand);

        if (!navigateBlocking) return;
        HubStep blocking = plan.Steps.Find(step => step.Blocking);
        if (blocking == null || blocking.Station == HallStation.Overview) return;
        _planningTab = StationTab(blocking.Station);
        _hallOverview = false;
        _inspectorOpen = false;
        SelectDefaultForTab();
    }

    private void SelectPlanningCard(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_muster)
        {
            if (!key.StartsWith("muster:", StringComparison.Ordinal)) return;
            _selectedCardKey = key;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Select, sourceId: key,
                tone: UiFeedbackTone.Preview);
            Rebuild();
            return;
        }
        if (_run == null) return;
        if (TrySimpleIndex(key, "market", out var offer))
        {
            // The Workbench keeps Market and the permanent unit rail visible together. A rail
            // selection may have made the legacy tab state Warband, but a visible Market card
            // must still be allowed to take authoritative dossier ownership.
            _planningTab = PlanningTab.Market;
            _selectedMarketOffer = offer;
        }
        _selectedCardKey = key;
        // Selection seats the choice in the Hall action tray. Full rules are progressive
        // disclosure through INSPECT, so choosing never throws a large dossier over the stage.
        _inspectorOpen = false;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select, sourceId: key,
            tone: UiFeedbackTone.Preview);
        if (TrySimpleIndex(key, "item", out var item))
            _selectedItem = item;
        Rebuild();
    }

    /// <summary>
    /// Executes the primary action for a Workbench card. A double click always establishes
    /// selection first, then confirms the action represented by that card.
    /// </summary>
    private void ActivatePlanningCard(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        SelectPlanningCard(key);
        if (_muster)
        {
            if (key.StartsWith("muster:", StringComparison.Ordinal))
                _actions.ToggleRecruit?.Invoke(key.Substring("muster:".Length));
            return;
        }
        if (_run == null) return;

        if (string.Equals(key, "slot", StringComparison.Ordinal))
        {
            BuyCapacity();
            return;
        }
        if (!TrySimpleIndex(key, "market", out int offerIndex) ||
            offerIndex < 0 || offerIndex >= _run.State.ShopOffers.Count)
            return;

        if (_run.State.ShopOffers[offerIndex] != null)
        {
            BuySelectedOffer();
            return;
        }
        if (offerIndex == _equipNowOfferIndex &&
            _run.IndexOfItem(_equipNowItemInstanceId) >= 0)
            UseInspectorAction(HallActionId.EquipNow);
    }

    private void SelectComparisonTarget(string key)
    {
        if (_run == null || !TryHeroAddress(key, out bool inBench, out int index) ||
            inBench || index < 0 || index >= _run.State.Field.Count)
            return;
        _comparisonTargetHeroKey = key;
        _focusedWarbandHeroId = _run.State.Field[index].InstanceId;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select,
            targetId: key, tone: UiFeedbackTone.Preview);
        Rebuild();
    }

    private void SelectDefaultForTab()
    {
        if (_run == null) return;
        switch (_planningTab)
        {
            case PlanningTab.Market:
                _selectedMarketOffer = _run.State.ShopOffers.FindIndex(o => o != null);
                _selectedCardKey = _selectedMarketOffer >= 0
                    ? $"market:{_selectedMarketOffer}"
                    : _run.SlotOfferOpen ? "slot" : "";
                break;
            case PlanningTab.Armory:
                _selectedCardKey = _run.State.Inventory.Count > 0 ? "item:0" : "";
                if (_run.State.Inventory.Count > 0) _selectedItem = 0;
                break;
            case PlanningTab.Hourstone:
                _selectedCardKey = _run.State.Inscriptions.Count > 0 ? "inscription:0" : "";
                break;
            default:
                _selectedCardKey = _run.State.Field.Count > 0 ? "hero:field:0" : "";
                break;
        }
    }

    private void BuySelectedOffer()
    {
        if (_run == null || _selectedMarketOffer < 0) return;
        int index = _selectedMarketOffer;
        int sandBefore = _run.State.Sand;
        string sourceId = $"market:{index}";
        PurchaseResult purchase = null;
        bool succeeded = ShopAction(() =>
        {
            purchase = _run.BuyOffer(index);
            if (purchase.ItemInstanceId > 0)
            {
                int itemIndex = _run.IndexOfItem(purchase.ItemInstanceId);
                int nextOffer = _run.State.ShopOffers.FindIndex(o => o != null);
                if (!_loadoutOpen)
                {
                    _loadoutReturnMarketOffer = nextOffer;
                    _loadoutReturnCardKey = nextOffer >= 0
                        ? $"market:{nextOffer}"
                        : $"market:{index}";
                }
                _loadoutOpen = true;
                _selectedItem = itemIndex;
                _selectedWarbandGearHeroId = 0;
                _selectedWarbandGearKind = -1;
                _selectedMarketOffer = nextOffer;
                _selectedCardKey = nextOffer >= 0
                    ? $"market:{nextOffer}"
                    : $"market:{index}";
                // Purchased equipment is ordinary Armory inventory immediately. The former
                // sold-card "Equip Now" receipt created a second, modal-feeling route to the
                // same operation and prevented the rack from being the obvious source.
                _equipNowItemInstanceId = 0;
                _equipNowOfferIndex = -1;
            }
            else
            {
                _equipNowItemInstanceId = 0;
                _equipNowOfferIndex = -1;
                _selectedMarketOffer = _run.State.ShopOffers.FindIndex(o => o != null);
                _selectedCardKey = _selectedMarketOffer >= 0
                    ? $"market:{_selectedMarketOffer}"
                    : "";
            }
        }, rebuild: false);
        if (succeeded)
        {
            int spent = Mathf.Max(0, sandBefore - _run.State.Sand);
            UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                sourceId: sourceId, targetId: TransactionTarget(purchase),
                resourceId: "ledger-sand", groupId: "market-offers",
                amount: -spent, tone: UiFeedbackTone.Sand,
                transaction: TransactionFor(purchase.Outcome));
            LogLine(() => _telemetry.PurchaseLine(_run.State, DateTime.UtcNow, purchase));
        }
        Rebuild();
    }

    private void BuyCapacity()
    {
        if (_run == null) return;
        int beforeSand = _run.State.Sand;
        int beforeCapacity = _run.State.FieldSlots;
        bool succeeded = ShopAction(() => _run.BuySlot(), rebuild: false);
        if (succeeded)
        {
            int spent = Mathf.Max(0, beforeSand - _run.State.Sand);
            UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                sourceId: _selectedCardKey, targetId: $"shelf-field:{beforeCapacity}",
                resourceId: "ledger-sand", amount: -spent, tone: UiFeedbackTone.Sand,
                transaction: UiTransactionKind.BuyCapacity);
            LogLine(() => _telemetry.SlotLine(_run.State, DateTime.UtcNow, spent));
        }
        Rebuild();
    }

    private static UiTransactionKind TransactionFor(PurchaseOutcome outcome) =>
        outcome switch
        {
            PurchaseOutcome.Recruit => UiTransactionKind.BuyRecruit,
            PurchaseOutcome.RankUp => UiTransactionKind.BuyRank,
            PurchaseOutcome.Weapon => UiTransactionKind.BuyWeapon,
            PurchaseOutcome.Trinket => UiTransactionKind.BuyTrinket,
            PurchaseOutcome.Inscription => UiTransactionKind.BindInscription,
            PurchaseOutcome.Capacity => UiTransactionKind.BuyCapacity,
            _ => UiTransactionKind.None,
        };

    private void UseInspectorAction(HallActionId action)
    {
        if (_muster)
        {
            // The dossier's one pre-run action: muster (or release) the inspected candidate.
            if (action != HallActionId.Buy) return;
            if (!_selectedCardKey.StartsWith("muster:", StringComparison.Ordinal)) return;
            _actions.ToggleRecruit?.Invoke(
                _selectedCardKey.Substring("muster:".Length));
            return;
        }
        if (_run == null) return;
        if (action == HallActionId.KeepShopping)
        {
            _equipNowItemInstanceId = 0;
            _equipNowOfferIndex = -1;
            SelectDefaultForTab();
            Rebuild();
            return;
        }
        if (action == HallActionId.EquipNow)
        {
            int pinnedIndex = _run.IndexOfItem(_equipNowItemInstanceId);
            if (pinnedIndex < 0)
            {
                _equipNowItemInstanceId = 0;
                _equipNowOfferIndex = -1;
                Rebuild();
                return;
            }
            OpenHallStation(HallStation.Armory);
            _selectedItem = pinnedIndex;
            _selectedCardKey = $"item:{pinnedIndex}";
            Rebuild();
            return;
        }
        if (action == HallActionId.Buy) { BuySelectedOffer(); return; }
        if (action == HallActionId.Freeze)
        {
            if (_selectedMarketOffer >= 0)
                ShopAction(() => _run.ToggleFreeze(_selectedMarketOffer));
            return;
        }
        if (action == HallActionId.BuySlot)
        {
            BuyCapacity();
            return;
        }

        if (TryHeroAddress(_selectedCardKey, out var inBench, out var heroIndex))
        {
            if (action == HallActionId.Deploy)
            {
                if (inBench) return;
                _deploySelected = heroIndex;
                ShowDeploymentOnBoard();
                Rebuild();
                return;
            }
            if (action == HallActionId.Equip)
            {
                _actions.EquipSelected?.Invoke(inBench, heroIndex);
                return;
            }
            if (action == HallActionId.Unequip)
            {
                _actions.UnequipWeapon?.Invoke(inBench, heroIndex);
                return;
            }
            if (action == HallActionId.Reforge)
            {
                ReforgeResult forged = null;
                bool succeeded = ShopAction(() =>
                    forged = _run.Reforge(
                        inBench ? RosterZone.Bench : RosterZone.Field, heroIndex),
                    rebuild: false);
                if (succeeded)
                {
                    LogLine(() => _telemetry.ReforgeLine(_run.State, DateTime.UtcNow, forged));
                    UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                        sourceId: $"hero:{(inBench ? "bench" : "field")}:{heroIndex}",
                        targetId: $"hero:{(inBench ? "bench" : "field")}:{heroIndex}",
                        resourceId: "ledger-sand", amount: -forged.SandSpent,
                        tone: UiFeedbackTone.Major,
                        transaction: UiTransactionKind.Reforge);
                }
                Rebuild();
                return;
            }
            if (action == HallActionId.Move)
            {
                _actions.MoveHero?.Invoke(inBench, heroIndex);
                _selectedCardKey = "hero:" + (inBench ? "field:" : "bench:") +
                                   Math.Max(0, (inBench ? _run.State.Field : _run.State.Bench).Count - 1);
                Rebuild();
                return;
            }
            if (action == HallActionId.SellHero)
            {
                _actions.SellHero?.Invoke(inBench, heroIndex);
                SelectDefaultForTab();
                Rebuild();
                return;
            }
        }

        if (TrySimpleIndex(_selectedCardKey, "item", out var itemIndex) &&
            action == HallActionId.SellItem)
        {
            _actions.SellItem?.Invoke(itemIndex);
            SelectDefaultForTab();
            Rebuild();
        }
    }

    private void ResolveInterlude(InterludePath path, int option)
    {
        if (_run == null) return;
        var before = RunMutationSnapshot.Capture(_run.State);
        try
        {
            var reward = _run.ResolveInterlude(path, option);
            LogLine(() => _telemetry.InterludeLine(
                _run.State, DateTime.UtcNow, path, option, reward.Id));
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reward,
                targetId: StationTarget(path == InterludePath.Armory ? HallStation.Armory :
                    path == InterludePath.Hourstone ? HallStation.Hourstone : HallStation.Market),
                amount: reward.Sand, tone: UiFeedbackTone.Positive,
                transaction: reward.Kind == OfferKind.Inscription
                    ? UiTransactionKind.BindInscription
                    : UiTransactionKind.None);
            var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
            RecordHubPlan(plan, navigateBlocking: false);
            HallStation destination = path == InterludePath.Armory ? HallStation.Armory :
                                      path == InterludePath.Hourstone ? HallStation.Hourstone :
                                      HallStation.Market;
            _planningTab = StationTab(destination);
            _hallOverview = false;
            _hubAttention.Clear(destination);
            _recommendedStation = destination == HallStation.Market
                ? HallStation.Breach
                : HallStation.Market;
            SelectDefaultForTab();
            _inspectorOpen = false;
            Go(RunScreen.Management);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Interlude choice refused: {ex.Message}");
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "hub-workspace",
                tone: UiFeedbackTone.Negative);
            Rebuild();
        }
    }

    private void ChooseRevisionUpgrade(int option)
    {
        if (_run == null) return;
        try
        {
            RevisionUpgradeDef selected = _run.ChooseRevisionUpgrade(option);
            LogLine(() => _telemetry.RevisionUpgradeLine(
                _run.State, DateTime.UtcNow, selected));
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reward,
                targetId: "station-hourstone",
                tone: UiFeedbackTone.Major);
            Rebuild();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Revision upgrade refused: {ex.Message}");
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "hub-workspace",
                tone: UiFeedbackTone.Negative);
            Rebuild();
        }
    }

    private void ChooseBossReward(int option)
    {
        if (_run == null) return;
        var before = RunMutationSnapshot.Capture(_run.State);
        try
        {
            string rewardId = _run.PreviewBossRewards()[option];
            _run.ChooseBossReward(option);
            LogLine(() => _telemetry.BossRewardLine(
                _run.State, DateTime.UtcNow, option, rewardId));
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reward,
                targetId: "station-hourstone", tone: UiFeedbackTone.Major,
                transaction: UiTransactionKind.BindInscription);
            var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
            RecordHubPlan(plan, navigateBlocking: false);
            _planningTab = PlanningTab.Hourstone;
            _hallOverview = false;
            _hubAttention.Clear(HallStation.Hourstone);
            _recommendedStation = HallStation.Market;
            SelectDefaultForTab();
            _inspectorOpen = false;
            Go(RunScreen.Management);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Boss reward refused: {ex.Message}");
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "hub-workspace",
                tone: UiFeedbackTone.Negative);
            Rebuild();
        }
    }

    private void ChooseEndless(bool continueRun)
    {
        if (_run == null || _run.State.Phase != RunPhase.VictoryChoice) return;
        try
        {
            // Log while the banked-victory choice is still the authoritative state; the next
            // mutation deliberately moves either to terminal Complete or virtual Act 4.
            LogLine(() => _telemetry.EndlessChoiceLine(
                _run.State, DateTime.UtcNow, continueRun));
            if (continueRun)
            {
                _run.ContinueBeyondTheHour();
                _telemetryPhase = "";
                _tierChosen = false;
                _placement.Clear();
                _planningTab = PlanningTab.Market;
                _recommendedStation = HallStation.Breach;
                _pendingHubPlan = new HubSequencePlan
                {
                    RecommendedStation = HallStation.Breach,
                };
                _inspectorOpen = false;
                _hallOverview = false;
                SelectDefaultForTab();
                UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                    targetId: "choice:Continue with this warband",
                    tone: UiFeedbackTone.Major);
                Go(RunScreen.Management);
                return;
            }

            _run.RetireWithVictory();
            LogLine(() => _telemetry.EndLine(_run.State, DateTime.UtcNow));
            if (_telemetryWriter != null)
                StartCoroutine(_telemetryWriter.Upload());
            BuildConclusionReceiptIfNeeded();
            UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                targetId: "choice:Retire with victory",
                tone: UiFeedbackTone.Major);
            Go(RunScreen.RunOver);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Beyond the Hour choice refused: {ex.Message}");
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "choice-scrim",
                tone: UiFeedbackTone.Negative);
            Rebuild();
        }
    }

    private static bool TrySimpleIndex(string key, string prefix, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix + ":", StringComparison.Ordinal))
            return false;
        return int.TryParse(key.Substring(prefix.Length + 1), out index);
    }

    private static bool TryHeroAddress(string key, out bool inBench, out int index)
    {
        inBench = false;
        index = -1;
        if (string.IsNullOrEmpty(key)) return false;
        var parts = key.Split(':');
        if (parts.Length != 3 || parts[0] != "hero") return false;
        inBench = parts[1] == "bench";
        return (inBench || parts[1] == "field") && int.TryParse(parts[2], out index);
    }

    private void PreparePlacement()
    {
        CancelDeploymentGesture(refreshBoard: false);
        _placement.Clear();
        _deploySelected = -1;
        if (_run == null || _run.State.Phase != RunPhase.Planning ||
            _run.CurrentNodeKind == NodeKind.Event)
            return;
        var suggested = RunHarness.DefaultPlacement(_run, _content);
        for (int i = 0; i < suggested.Count && i < _run.State.Field.Count; i++)
            _placement[i] = suggested[i];
        ShowDeploymentOnBoard();
    }

    /// <summary>
    /// Leaving the map. An Interlude opens its two-stage Revision/reward draft; a fight opens
    /// DEPLOYMENT first — placing the warband is still the decision the fight is made of.
    /// </summary>
    private void BeginNode()
    {
        if (_run == null || _run.State.Phase != RunPhase.Planning) return;

        if (_run.CurrentNodeKind == NodeKind.Event)
        {
            _planningTab = PlanningTab.Hourstone;
            _hallOverview = false;
            _recommendedStation = HallStation.Hourstone;
            Go(RunScreen.Management);
            return;
        }

        if (_run.CurrentNodeKind != NodeKind.Boss)
        {
            _tierChosen = false;
            Go(RunScreen.Wager);
            return;
        }

        OpenDeployment();
    }

    private void ConfirmWager()
    {
        if (_run == null || _run.State.Phase != RunPhase.Planning ||
            _run.CurrentNodeKind != NodeKind.Fight || !_tierChosen)
            return;
        OpenDeployment();
    }

    private void OpenDeployment()
    {
        // Seed with the auto-formation rather than an empty board: a sensible default is a far
        // better starting point than a blank slate, and the player can move any of it.
        _placement.Clear();
        var suggested = RunHarness.DefaultPlacement(_run, _content);
        for (int i = 0; i < suggested.Count && i < _run.State.Field.Count; i++)
            _placement[i] = suggested[i];
        _deploySelected = -1;
        ShowDeploymentOnBoard();
        Go(RunScreen.Deploy);
    }

    private void ResolveCurrentNode()
    {
        if (_run == null || _run.State.Phase != RunPhase.Planning) return;
        var before = RunMutationSnapshot.Capture(_run.State);
        try
        {
            var kind = _run.CurrentNodeKind;
            if (kind == NodeKind.Event)
            {
                _planningTab = PlanningTab.Hourstone;
                _hallOverview = false;
                Go(RunScreen.Management);
                return;
            }
            else
            {
                var placement = CurrentPlacement();
                _pendingFightBrief = BriefForCurrentNode();
                _pendingFightKind = kind;
                _pendingFightBefore = before;
                _preparedFight = kind == NodeKind.Boss
                    ? _run.PrepareBoss(placement)
                    : _run.PrepareFight(_tier, placement);
                _lastFightOutcome = _preparedFight.Original;
                _lastBattle = _lastFightOutcome.Battle;
                ConfigureRevisionForFight();

                // The run is deliberately still at this node. Playback is now evidence the player
                // may act on; only accepting this future or committing a split advances the run.
                if (_player != null && _lastBattle != null)
                {
                    _player.PlaybackEnded -= OnFightWatched;
                    _player.PlaybackEnded += OnFightWatched;
                    _player.RevisionPauseReached -= OnRevisionFinalChance;
                    _player.RevisionPauseReached += OnRevisionFinalChance;
                    _player.PlayBattle(_lastBattle);
                    if (_revisionFatalTick > 0)
                        _player.ArmRevisionPauseBefore(_revisionFatalTick);
                    _resultGateOpen = false;
                    Go(RunScreen.Fight);
                    return;
                }

                FightOutcome committed = _run.CommitOriginal(_preparedFight);
                FinalizeCommittedFight(committed);
                Go(RunScreen.Fight);
                OpenFightResult();
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunShell] Could not resolve node: {ex.Message}");
        }
        GoAfterNode();
    }

    // ---- deployment --------------------------------------------------------------

    /// <summary>Placement in FIELD order, which is the order ResolveFight/ResolveBoss expect.</summary>
    private IReadOnlyList<Hex> CurrentPlacement()
    {
        var list = new List<Hex>();
        for (int i = 0; i < _run.State.Field.Count; i++)
            list.Add(_placement.TryGetValue(i, out var h) ? h : default);
        return list;
    }

    /// <summary>
    /// Panel → screen, matching the Planning screen's conversion: UI Toolkit's origin is
    /// top-left of the panel, the camera's is bottom-left of the screen, and the panel may be
    /// scaled by PanelSettings. Getting this wrong silently places units on the wrong row.
    /// </summary>
    private static void SetDisplayed(VisualElement element, bool shown)
    {
        if (element != null)
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private Vector2 PanelToScreen(Vector2 panel)
    {
        float pw = _root.resolvedStyle.width, ph = _root.resolvedStyle.height;
        if (pw <= 0f) pw = Screen.width;
        if (ph <= 0f) ph = Screen.height;
        return new Vector2(panel.x * Screen.width / Mathf.Max(1f, pw),
                           Screen.height - panel.y * Screen.height / Mathf.Max(1f, ph));
    }

    private Vector2 ScreenToPanel(Vector2 screen)
    {
        float pw = _root.resolvedStyle.width, ph = _root.resolvedStyle.height;
        if (pw <= 0f) pw = Screen.width;
        if (ph <= 0f) ph = Screen.height;
        return new Vector2(screen.x * pw / Mathf.Max(1f, Screen.width),
                           (Screen.height - screen.y) * ph / Mathf.Max(1f, Screen.height));
    }

    private bool CanInteractWithDeployment() =>
        _model.Screen == RunScreen.Deploy &&
        _player != null &&
        _run != null &&
        _run.State.Phase == RunPhase.Planning &&
        _run.CurrentNodeKind != NodeKind.Event;

    private void OnDeploymentPointerDown(Vector2 panelPos, int pointerId)
    {
        if (!CanInteractWithDeployment() || _deployPointerId >= 0) return;
        _deployPointerId = pointerId;
        _deployPointerStart = panelPos;
        _deployPointerUnit = -1;
        _deployDragging = false;
        _deployHoverValid = false;

        IReadOnlyCollection<int> placedIds = _placement.Keys.ToList();
        PlaybackUnit picked = _player.PickUnit(
            PanelToScreen(panelPos), 12f, placedIds);
        if (picked != null && _placement.ContainsKey(picked.Id))
            _deployPointerUnit = picked.Id;
    }

    private void OnDeploymentPointerMoved(Vector2 panelPos, int pointerId)
    {
        if (pointerId != _deployPointerId || !CanInteractWithDeployment()) return;
        if (!_deployDragging)
        {
            if (_deployPointerUnit < 0 ||
                Vector2.Distance(panelPos, _deployPointerStart) < DeploymentDragThreshold)
                return;
            _deployDragging = true;
            _deploySelected = _deployPointerUnit;
            _runtimeTooltips?.Hide();
            ShowDeploymentOnBoard();
            Rebuild();
        }
        UpdateDeploymentDrag(panelPos);
    }

    private void OnDeploymentPointerUp(Vector2 panelPos, int pointerId)
    {
        if (pointerId != _deployPointerId) return;
        bool dragged = _deployDragging;
        int draggedUnit = _deployPointerUnit;
        if (dragged) UpdateDeploymentDrag(panelPos);
        bool validDrop = dragged && _deployHoverValid;
        Hex dropHex = _deployHoverHex;

        ClearDeploymentGestureState();
        _player?.ClearPlanningDragFeedback();
        if (!dragged)
        {
            OnBoardClicked(panelPos);
            return;
        }

        if (!validDrop || !PlaceDeploymentUnit(draggedUnit, dropHex))
            RejectDeployment();
        _deploySelected = -1;
        ShowDeploymentOnBoard();
        Rebuild();
    }

    private void OnDeploymentPointerCanceled(int pointerId)
    {
        if (pointerId != _deployPointerId) return;
        CancelDeploymentGesture(refreshBoard: _deployDragging);
    }

    private void UpdateDeploymentDrag(Vector2 panelPos)
    {
        _deployHoverValid = false;
        if (_player == null || _deployPointerUnit < 0) return;
        if (!_player.MovePlanningUnit(
                _deployPointerUnit,
                PanelToScreen(panelPos),
                out Hex hover))
        {
            _player.ClearPlanningDragFeedback();
            return;
        }

        _deployHoverHex = hover;
        _deployHoverValid = hover.Row < Battle.BoardRows / 2;
        int occupant = OccupantOf(hover);
        bool swap = occupant >= 0 && occupant != _deployPointerUnit;
        _player.SetPlanningDropTarget(hover, _deployHoverValid, swap);
    }

    private void CancelDeploymentGesture(bool refreshBoard)
    {
        bool wasDragging = _deployDragging;
        ClearDeploymentGestureState();
        _player?.ClearPlanningDragFeedback();
        if (refreshBoard && wasDragging)
        {
            ShowDeploymentOnBoard();
            Rebuild();
        }
    }

    private void ClearDeploymentGestureState()
    {
        _deployPointerId = -1;
        _deployPointerUnit = -1;
        _deployDragging = false;
        _deployHoverValid = false;
    }

    private void OnBoardClicked(Vector2 panelPos)
    {
        if (!CanInteractWithDeployment()) return;
        Vector2 screenPos = PanelToScreen(panelPos);
        IReadOnlyCollection<int> placedIds = _placement.Keys.ToList();
        PlaybackUnit picked = _player.PickUnit(screenPos, 12f, placedIds);
        Hex hex;
        if (picked != null && _placement.TryGetValue(picked.Id, out Hex pickedHex))
            hex = pickedHex;
        else if (!_player.TryScreenToHex(screenPos, out hex))
        {
            RejectDeployment();
            return;
        }
        // The run layer enforces this too, but a refusal AFTER lock-in would be far too late to
        // be useful — say it at the moment of the click.
        if (hex.Row >= Battle.BoardRows / 2)
        {
            RejectDeployment();
            return;
        }

        int occupant = OccupantOf(hex);
        if (_deploySelected < 0)
        {
            // Nothing held: clicking a placed hero picks them up, so a formation can be
            // rearranged without first hunting for their chip in the rail.
            if (occupant >= 0)
            {
                _deploySelected = occupant;
            }
            else RejectDeployment();
            ShowDeploymentOnBoard();
            Rebuild();
            return;
        }

        if (!PlaceDeploymentUnit(_deploySelected, hex))
        {
            RejectDeployment();
            return;
        }
        _deploySelected = -1;
        ShowDeploymentOnBoard();
        Rebuild();
    }

    private bool PlaceDeploymentUnit(int unitIndex, Hex hex)
    {
        if (unitIndex < 0 ||
            _run == null ||
            unitIndex >= _run.State.Field.Count ||
            hex.Row >= Battle.BoardRows / 2)
            return false;

        int occupant = OccupantOf(hex);
        if (occupant >= 0 && occupant != unitIndex)
        {
            // Swap rather than refuse — refusing here would force a tedious move-out-of-the-way
            // dance for the most ordinary rearrangement there is.
            if (_placement.TryGetValue(unitIndex, out Hex from)) _placement[occupant] = from;
            else _placement.Remove(occupant);
        }
        _placement[unitIndex] = hex;
        return true;
    }

    private int OccupantOf(Hex hex)
    {
        foreach (var kv in _placement)
            if (kv.Value.Equals(hex)) return kv.Key;
        return -1;
    }

    private void RejectDeployment()
    {
        UiPolishSignals.Emit(UiPolishSignals.Cue.Error,
            targetId: "deploy-screen", tone: UiFeedbackTone.Negative);
    }

    /// <summary>
    /// Paint the pending formation onto the real board using the SAME projection a fight uses,
    /// so what the player arranges is literally what they will watch.
    /// </summary>
    private void ShowDeploymentOnBoard()
    {
        if (_player == null || _run == null) return;
        var units = new List<PlaybackUnit>();
        var rings = new List<ReplayPlayer.MusterRing>();
        for (int i = 0; i < _run.State.Field.Count; i++)
        {
            if (!_placement.TryGetValue(i, out var hex)) continue;
            var def = ComposeHero(_run.State.Field[i]);
            units.Add(PlaybackUnit.From(UnitState.Spawn(i, 0, def, hex)));
            var seats = MechanicalRulePresenter.MusterSeats(def, hex);
            if (seats.Count > 0)
                rings.Add(new ReplayPlayer.MusterRing(i, seats.ToList(), i == _deploySelected));
        }
        int id = 100;
        foreach (var e in EnemiesForCurrentNode())
            units.Add(PlaybackUnit.From(UnitState.Spawn(id++, 1, e.Def, e.Pos)));

        // ShowSnapshot rebuilds the board, which destroys everything under it — the rings have to
        // be re-established after, never before.
        _player.ShowSnapshot(units);
        _player.SetPlanningSelection(_deploySelected);
        _player.SetMusterRings(rings);
    }


    private List<(UnitDef Def, Hex Pos)> EnemiesForCurrentNode()
    {
        // RunController.PreviewEnemies, never a local guess: the encounter rng is derived from
        // private salts, so any reconstruction here would show a different army than spawns.
        try { return _run.PreviewEnemies(_tier); }
        catch { return new List<(UnitDef, Hex)>(); }
    }

    private void ConfigureRevisionForFight()
    {
        RevisionScreenEffect.Clear();
        _revisionFixtureCluster = false;
        _revisionTargetIds.Clear();
        _revisionPresentTick = -1;
        _revisionBranchTick = -1;
        _revisionFatalTick = _lastFightOutcome != null && !_lastFightOutcome.Won
            ? FindFatalTick(_lastBattle)
            : -1;
        _revisionOpenedOnce = false;
        StopRevisionScrub();
        _revisionScrubClock = -1f;
        _player?.SetRevisionReducedMotion(_reducedMotion);
        _revisionCombat.Presentation = RevisionPresentationPhase.None;
        _revisionCombat.PresentationProgress = 0f;
        _revisionCombat.Receipt = null;
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        RevisionModifier modifiers = RevisionCatalog.Modifiers(_run.State.Revision);
        _revisionCombat.Mode = RevisionCombatMode.Ready;
        _revisionCombat.Name = revision.Name;
        _revisionCombat.Prompt = "";
        _revisionCombat.Status = "Watch the battle, then split one earlier moment.";
        _revisionCombat.FinalChance = false;
        _revisionCombat.CanOpen = false;
        _revisionCombat.CanConfirm = false;
        _revisionCombat.MaxSeconds = modifiers.HasFlag(RevisionModifier.LongMemory) ? 6 : 4;
        _revisionCombat.SelectedSeconds = 1;
        _revisionCombat.Targets.Clear();
        _revisionCombat.Anchors.Clear();
    }

    private static int FindFatalTick(BattleResult battle)
    {
        if (battle == null) return -1;
        PlaybackState fold = PlaybackState.From(battle.InitialUnits, battle.RuleIds);
        int index = 0;
        while (index < battle.Events.Count)
        {
            int tick = battle.Events[index].Tick;
            fold.AdvanceToTick(battle.Events, tick);
            bool anyPlayer = false;
            foreach (PlaybackUnit unit in fold.Units)
                if (unit.Team == 0 && !unit.Dead) { anyPlayer = true; break; }
            if (!anyPlayer) return tick;
            while (index < battle.Events.Count && battle.Events[index].Tick == tick) index++;
        }
        return -1;
    }

    private void OpenRevision() => OpenRevision(false);

    private void OpenRevision(bool finalChance)
    {
        if (_preparedFight == null || _lastBattle == null || _player == null ||
            _revisionCombat.Mode == RevisionCombatMode.Revised ||
            _revisionCeremony != null)
            return;
        int present = finalChance
            ? Mathf.Max(0, _revisionFatalTick - 1)
            : _player.PauseForRevision();
        int available = Mathf.Min(_revisionCombat.MaxSeconds, present / 10);
        if (available < 1)
        {
            if (finalChance) AcceptOriginalFate();
            else
            {
                _revisionCombat.Status = "The Hour needs one full battle-second before it can split.";
                _revisionCombat.CanOpen = false;
                Rebuild();
            }
            return;
        }

        _revisionPresentTick = present;
        _revisionCombat.FinalChance = finalChance;
        _revisionCombat.Mode = RevisionCombatMode.Opening;
        _revisionCombat.MaxSeconds = available;
        // Open ON the present, not one second back (Jake 2026-07-29). Jumping the board the instant
        // you pause is jarring and hides the fact that reaching back is YOUR move; 0 means "the
        // moment you stopped", and the Hour cannot be split until the player turns the stone.
        _revisionCombat.SelectedSeconds = 0;
        _revisionBeatFaulted = false;
        _revisionCombat.Prompt = finalChance
            ? "Defeat is one beat away. Choose a return and a marked target."
            : "Choose a return, then click a marked unit on the board.";
        _revisionCombat.Status = "";
        _revisionTargetIds.Clear();
        _player.RenderRevisionFrame(_revisionPresentTick);
        _revisionScrubClock = _revisionPresentTick;   // the first anchor walks back from here
        _revisionCombat.Sweep = 0f;                   // …and the stone opens at the present
        bool fullRupture = !_revisionOpenedOnce;
        _revisionOpenedOnce = true;
        RevisionDef activeRevision =
            RevisionCatalog.Get(_run.State.Revision.RevisionId);
        RevisionScreenEffect.BeginOpening(
            activeRevision.Effect,
            fullRupture,
            finalChance,
            _reducedMotion,
            RevisionTune());
        SetRevisionCinematicLock(true);
        UiPolishSignals.Emit(UiPolishSignals.Cue.Reveal,
            targetId: "revision-combat",
            tone: finalChance ? UiFeedbackTone.Major : UiFeedbackTone.Preview);
        Rebuild();
        _revisionCeremony = StartCoroutine(PlayRevisionOpening(finalChance, fullRupture));
    }

    private IEnumerator PlayRevisionOpening(bool finalChance, bool fullRupture)
    {
        RevisionPresentationTune tune = RevisionTune();
        float duration = _reducedMotion
            ? tune.reducedOpenSeconds
            : fullRupture ? tune.firstOpenSeconds : tune.reopenSeconds;
        if (RevisionAudioEnabled())
        {
            SfxPlayer.StopBoardVoices();
            SfxPlayer.Play(
                fullRupture ? "revision_split" : "revision_reopen",
                SfxBus.Revision);
            SfxPlayer.StartRevisionLoop(finalChance ? "revision_final_hold" : "revision_hold");
        }
        string title = finalChance ? "THE LAST HOUR REFUSES" : "THE HOUR BREAKS";
        string subtitle = fullRupture
            ? "A WITNESSED FUTURE FRACTURES"
            : "THE SPLIT OPENS AGAIN";
        yield return RevisionBeat(duration, t =>
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            _player?.SetRevisionFreeze(eased);
            SetRevisionPresentation(
                RevisionPresentationPhase.Opening, eased, title, subtitle);
        });
        _revisionCombat.Mode = RevisionCombatMode.Selecting;
        _revisionCombat.Presentation = RevisionPresentationPhase.Held;
        _revisionCombat.PresentationProgress = 1f;
        RevisionScreenEffect.SetPhase(RevisionPresentationPhase.Held, 1f);
        SelectRevisionSeconds(_revisionCombat.SelectedSeconds);
        // Keep ordinary fight chrome out while the player chooses; board target picking remains live.
        SetRevisionCinematicLock(true);
        _revisionCeremony = null;
        Rebuild();
    }

    private void SelectRevisionSeconds(int seconds)
    {
        if (_revisionCombat.Mode != RevisionCombatMode.Selecting || _lastBattle == null) return;
        bool changed = seconds != _revisionCombat.SelectedSeconds;
        _revisionCombat.SelectedSeconds = Mathf.Clamp(seconds, 0, _revisionCombat.MaxSeconds);
        _revisionBranchTick = _revisionPresentTick - _revisionCombat.SelectedSeconds * 10;
        // Reaching further back can put a chosen target outside the split — it may not be alive or
        // active in BOTH moments. That used to happen silently: the chip vanished and the status
        // reverted to the generic prompt, with nothing saying which unit went or why.
        var dropped = new List<string>();
        for (int i = _revisionTargetIds.Count - 1; i >= 0; i--)
        {
            int id = _revisionTargetIds[i];
            if (IsEligibleRevisionTarget(id)) continue;
            dropped.Insert(0, RevisionRosterUnit(id)?.Name ?? $"Unit {id}");
            _revisionTargetIds.RemoveAt(i);
        }
        ScrubRevisionTo(_revisionCombat.SelectedSeconds <= 0
            ? _revisionPresentTick
            : _revisionBranchTick - 1);
        RefreshRevisionTargets();
        if (dropped.Count > 0)
            _revisionCombat.Status =
                $"{string.Join(" and ", dropped)} cannot be reached at −{_revisionCombat.SelectedSeconds}s.";
        if (changed && RevisionAudioEnabled())
            SfxPlayer.Play("revision_scrub", SfxBus.Revision, 0.62f);
        Rebuild();
    }

    /// <summary>
    /// Walk the held board to a new anchor instead of cutting to it (Jake 2026-07-29). Choosing a
    /// second should *play* time to that moment — backward when reaching further into the past,
    /// forward when returning toward the present — so the Hour reads as one continuous timeline
    /// the player is dragging rather than a stack of separate screenshots. Legality and the target
    /// rail are still resolved instantly against the authoritative branch fold: the walk is pure
    /// presentation and never gates a commit.
    /// </summary>
    private void ScrubRevisionTo(float target)
    {
        if (_player == null) return;
        StopRevisionScrub();
        float from = _revisionScrubClock >= 0f ? _revisionScrubClock : _revisionPresentTick;
        // Reduced Motion keeps the same information without spatial reverse playback.
        if (_reducedMotion || Mathf.Abs(target - from) < 0.5f)
        {
            _revisionScrubClock = target;
            _player.BuildRevisionPreview(Mathf.RoundToInt(target));
            _player.SetRevisionFreeze(true);
            UpdateRevisionSweep();
            return;
        }
        _revisionScrub = StartCoroutine(PlayRevisionScrub(from, target));
    }

    private IEnumerator PlayRevisionScrub(float from, float to)
    {
        RevisionPresentationTune tune = RevisionTune();
        // Distance-proportional so one step is snappy and a four-second reach still reads as travel,
        // clamped so held arrow keys never feel gluey.
        float seconds = Mathf.Clamp(
            Mathf.Abs(to - from) / 10f * tune.scrubPerSecond,
            tune.scrubMinSeconds,
            tune.scrubMaxSeconds);
        // Tear the board down ONCE, then walk on the cheap re-fold. The dress is set here for the
        // same reason: SetRevisionFreeze searches the scene for the Volume, and the held Hour is
        // already frozen, so paying for that per frame bought nothing but a hitch.
        _player.RenderRevisionFrame(from);
        _player.SetRevisionFreeze(true);
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            _revisionScrubClock = Mathf.Lerp(from, to, eased);
            _player.ScrubRevisionFrame(_revisionScrubClock);
            UpdateRevisionSweep();
        }
        _revisionScrubClock = to;
        _player.BuildRevisionPreview(Mathf.RoundToInt(to));
        _player.SetRevisionFreeze(true);
        UpdateRevisionSweep();
        _revisionScrub = null;
    }

    /// <summary>Interrupt a walk in flight; the next one departs from wherever it actually got to.</summary>
    private void StopRevisionScrub()
    {
        if (_revisionScrub == null) return;
        StopCoroutine(_revisionScrub);
        _revisionScrub = null;
    }

    private void ShiftRevisionSeconds(int direction)
    {
        SelectRevisionSeconds(_revisionCombat.SelectedSeconds + Math.Sign(direction));
    }

    private bool IsEligibleRevisionTarget(int id)
    {
        if (_lastBattle == null || _revisionPresentTick < 0 || _revisionBranchTick < 0)
            return false;
        PlaybackState present = FoldAt(_lastBattle, _revisionPresentTick);
        PlaybackState branch = FoldAt(_lastBattle, _revisionBranchTick - 1);
        PlaybackUnit now = present.ById(id);
        PlaybackUnit then = branch.ById(id);
        if (now == null || then == null || now.Dead || then.Dead ||
            HasStatus(now, StatusKind.Omitted) || HasStatus(then, StatusKind.Omitted))
            return false;
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        int team = revision.Effect == RevisionEffectKind.BorrowedFuture ? 0 : 1;
        return now.Team == team && then.Team == team &&
               (revision.Effect != RevisionEffectKind.BorrowedFuture || now.ManaMax > 0);
    }

    private static bool HasStatus(PlaybackUnit unit, StatusKind kind)
    {
        foreach (var status in unit.Statuses)
            if (status.Kind == kind) return true;
        return false;
    }

    private static PlaybackState FoldAt(BattleResult battle, int tick)
    {
        PlaybackState fold = PlaybackState.From(battle.InitialUnits, battle.RuleIds);
        fold.AdvanceToTick(battle.Events, tick);
        return fold;
    }

    private void ToggleRevisionTarget(PlaybackUnit picked)
    {
        if (picked == null || !IsEligibleRevisionTarget(picked.Id))
        {
            _revisionCombat.Status = RevisionCatalog.Get(_run.State.Revision.RevisionId).Effect ==
                                     RevisionEffectKind.BorrowedFuture
                ? "Choose a living allied champion with Mana in both moments."
                : "Choose a living enemy present in both moments.";
            Rebuild();
            return;
        }
        if (_revisionTargetIds.Remove(picked.Id))
        {
            RefreshRevisionTargets();
            Rebuild();
            return;
        }
        RevisionModifier modifiers = RevisionCatalog.Modifiers(_run.State.Revision);
        int limit = RevisionCatalog.Get(_run.State.Revision.RevisionId).Effect ==
                    RevisionEffectKind.BorrowedFuture &&
                    modifiers.HasFlag(RevisionModifier.Convergence)
            ? 2
            : 1;
        if (_revisionTargetIds.Count >= limit)
        {
            if (limit == 1) _revisionTargetIds.Clear();
            else
            {
                _revisionCombat.Status = "Convergence can carry two champions at most.";
                Rebuild();
                return;
            }
        }
        _revisionTargetIds.Add(picked.Id);
        RefreshRevisionTargets();
        Rebuild();
    }

    private void RefreshRevisionTargets()
    {
        _revisionCombat.Targets.Clear();
        PlaybackState present = _lastBattle == null
            ? null
            : FoldAt(_lastBattle, _revisionPresentTick);
        foreach (int id in _revisionTargetIds)
        {
            PlaybackUnit unit = present?.ById(id);
            _revisionCombat.Targets.Add(unit?.Name ?? $"Unit {id}");
        }
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        _revisionCombat.Status = _revisionTargetIds.Count > 0
            ? revision.Effect == RevisionEffectKind.BorrowedFuture
                ? "Future Mana will cross the split into this earlier moment."
                : "The target will return to its original formation Disarmed."
            : revision.Effect == RevisionEffectKind.BorrowedFuture
                ? "Select a living allied champion with Mana."
                : "Select a living enemy.";
        _revisionCombat.CanConfirm =
            _revisionTargetIds.Count > 0 && _revisionCombat.SelectedSeconds >= 1;
        if (_player != null && _lastBattle != null)
        {
            List<int> eligible = EligibleRevisionTargetIds();
            _player.SetRevisionTargets(eligible, _revisionTargetIds);
            _revisionCombat.DockSide =
                _player.TryGetUnitScreenBounds(eligible, out Rect targetBounds)
                ? targetBounds.center.y < Screen.height * 0.5f
                    ? RevisionDockSide.Top
                    : RevisionDockSide.Bottom
                : revision.Effect == RevisionEffectKind.BorrowedFuture
                    ? RevisionDockSide.Top
                    : RevisionDockSide.Bottom;
            UpdateRevisionScreenTargets();
        }
        // Anchor prices depend on the chosen targets, so they re-resolve on every target change too.
        RefreshRevisionAnchors();
    }

    private List<int> EligibleRevisionTargetIds()
    {
        var eligible = new List<int>();
        if (_lastBattle == null || _revisionBranchTick < 0) return eligible;
        PlaybackState branch = FoldAt(_lastBattle, _revisionBranchTick - 1);
        foreach (PlaybackUnit unit in branch.Units)
            if (IsEligibleRevisionTarget(unit.Id)) eligible.Add(unit.Id);
        return eligible;
    }

    /// <summary>Team and name for a unit id, read off the battle's opening roster — both are fixed
    /// for a unit's life, so this needs no fold.</summary>
    private PlaybackUnit RevisionRosterUnit(int id)
    {
        if (_lastBattle?.InitialUnits == null) return null;
        foreach (PlaybackUnit unit in _lastBattle.InitialUnits)
            if (unit.Id == id) return unit;
        return null;
    }

    /// <summary>
    /// Place and price every return anchor. The carry is NOT monotonic in the reach — it peaks just
    /// after the champion spent its own Mana — so each notch carries a normalised payoff and the
    /// stone shows where the good seconds are without printing a table.
    /// </summary>
    private void RefreshRevisionAnchors()
    {
        _revisionCombat.Anchors.Clear();
        if (_lastBattle == null || _revisionPresentTick < 0) return;
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        _revisionCombat.Lineage = revision.Effect;
        _revisionCombat.LineageName = revision.Name.ToUpperInvariant();
        PlaybackState present = FoldAt(_lastBattle, _revisionPresentTick);

        var carries = new List<int>();
        int best = 0;
        for (int seconds = 1; seconds <= _revisionCombat.MaxSeconds; seconds++)
        {
            int carried = revision.Effect == RevisionEffectKind.BorrowedFuture &&
                          TryRevisionCarry(present, seconds, out int c, out _, out _)
                ? c
                : 0;
            carries.Add(carried);
            best = Mathf.Max(best, carried);
        }
        _revisionCombat.Anchors.Add(new RevisionAnchorModel
        {
            Seconds = 0,
            Label = "NOW",
            Payoff = 1f,
        });
        for (int seconds = 1; seconds <= _revisionCombat.MaxSeconds; seconds++)
            _revisionCombat.Anchors.Add(new RevisionAnchorModel
            {
                Seconds = seconds,
                Label = $"\u2212{seconds}s",
                // No target yet, or a lineage with a flat effect: every notch weighs the same.
                Payoff = best <= 0 ? 1f : carries[seconds - 1] / (float)best,
            });
    }

    /// <summary>Where the board actually is, in seconds back. The knob rides this, not the
    /// selection, so the stone stays physically joined to the walk.</summary>
    private void UpdateRevisionSweep()
    {
        if (_revisionCombatOverlay == null || _revisionPresentTick < 0) return;
        float clock = _revisionScrubClock >= 0f ? _revisionScrubClock : _revisionPresentTick;
        _revisionCombatOverlay.SetSweep(Mathf.Max(0f, (_revisionPresentTick - clock) / 10f));
    }

    /// <summary>
    /// Draw the ability on the unit it happens to. Refreshed every frame while the Hour is held,
    /// because scrubbing walks the target across the board and the numbers have to ride it.
    /// </summary>
    private void UpdateRevisionCluster()
    {
        if (_revisionCombatOverlay == null || _revisionFixtureCluster) return;
        RevisionClusterModel cluster = _revisionCombat.Cluster;
        cluster.Visible = false;
        if (_revisionCombat.Mode != RevisionCombatMode.Selecting ||
            _player == null || _lastBattle == null || _revisionTargetIds.Count == 0 ||
            _revisionCombat.SelectedSeconds < 1)
        {
            _revisionCombatOverlay.SetCluster(cluster);
            return;
        }
        int id = _revisionTargetIds[0];
        if (!_player.TryGetUnitScreenRect(id, out Rect rect))
        {
            _revisionCombatOverlay.SetCluster(cluster);
            return;
        }
        cluster.Visible = true;
        cluster.Panel = ScreenToPanel(new Vector2(rect.center.x, rect.yMax));
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        cluster.Kind = revision.Effect;

        if (revision.Effect == RevisionEffectKind.BorrowedFuture)
        {
            PlaybackState present = FoldAt(_lastBattle, _revisionPresentTick);
            PlaybackState branch = FoldAt(_lastBattle, Mathf.Max(0, _revisionBranchTick - 1));
            PlaybackUnit then = branch.ById(id);
            TryRevisionCarry(present, _revisionCombat.SelectedSeconds,
                out int carried, out int mana, out int shield);
            cluster.Carry = carried;
            cluster.ManaMax = then?.ManaMax ?? 0;
            cluster.ManaAfter = Mathf.Min(cluster.ManaMax, (then?.Mana ?? 0) + mana);
            cluster.Shield = shield;
            cluster.HasHome = false;
        }
        else
        {
            RevisionModifier modifiers = RevisionCatalog.Modifiers(_run.State.Revision);
            cluster.DisarmSeconds =
                (modifiers.HasFlag(RevisionModifier.LongPeace) ? 25 : 15) / 10f;
            // The destination is the unit's OPENING hex — no body stands there, so it cannot be
            // resolved through the unit-view path.
            PlaybackUnit roster = RevisionRosterUnit(id);
            cluster.HasHome = roster != null &&
                              _player.TryGetHexScreenPosition(roster.Pos, out Vector2 home);
            if (cluster.HasHome)
            {
                _player.TryGetHexScreenPosition(roster.Pos, out Vector2 homeScreen);
                cluster.HomePanel = ScreenToPanel(homeScreen);
            }
        }
        _revisionCombatOverlay.SetCluster(cluster);
    }

    /// <summary>
    /// One irreversible action per battle earns a held beat rather than a click. The fill drains
    /// instantly on release, so a brushed key never edges the split closer.
    /// </summary>
    private void UpdateRevisionHold()
    {
        if (_revisionCombatOverlay == null) return;
        if (_revisionCombat.Mode != RevisionCombatMode.Selecting || !_revisionCombat.CanConfirm)
        {
            if (_revisionHold > 0f)
            {
                _revisionHold = 0f;
                _revisionCombatOverlay.SetHold(0f);
            }
            return;
        }
        if (!_revisionCombatOverlay.ConfirmHeld)
        {
            if (_revisionHold > 0f)
            {
                _revisionHold = 0f;
                _revisionCombatOverlay.SetHold(0f);
            }
            return;
        }
        _revisionHold += Mathf.Max(0f, Time.unscaledDeltaTime);
        float progress = Mathf.Clamp01(_revisionHold / RevisionHoldSeconds);
        _revisionCombatOverlay.SetHold(progress);
        if (progress < 1f) return;
        _revisionHold = 0f;
        _revisionCombatOverlay.SetHold(0f);
        ConfirmRevision();
    }

    /// <summary>
    /// Mirrors <c>Battle.ApplyBorrowedFuture</c>: carried Mana fills toward ManaMax and the
    /// remainder becomes Shield. Both moments were witnessed, so this reports a past fact rather
    /// than forecasting the branch (ADR 0028 law 6).
    /// </summary>
    private bool TryRevisionCarry(
        PlaybackState present, int seconds, out int carried, out int mana, out int shield)
    {
        carried = 0;
        mana = 0;
        shield = 0;
        if (_lastBattle == null || _revisionTargetIds.Count == 0) return false;
        int branchTick = _revisionPresentTick - seconds * 10;
        if (branchTick < 0) return false;
        RevisionModifier modifiers = RevisionCatalog.Modifiers(_run.State.Revision);
        int minimum = modifiers.HasFlag(RevisionModifier.DeepReserve) ? 25 : 15;
        PlaybackState branch = FoldAt(_lastBattle, branchTick - 1);
        foreach (int id in _revisionTargetIds)
        {
            PlaybackUnit now = present.ById(id);
            PlaybackUnit then = branch.ById(id);
            if (now == null || then == null) continue;
            int one = Mathf.Max(minimum, Mathf.Max(0, now.Mana - then.Mana));
            int added = Mathf.Min(one, Mathf.Max(0, then.ManaMax - then.Mana));
            carried += one;
            mana += added;
            shield += one - added;
        }
        return carried > 0;
    }

    private void ConfirmRevision()
    {
        if (_preparedFight == null || !_revisionCombat.CanConfirm) return;
        try
        {
            var choice = new RevisionChoice
            {
                PresentTick = _revisionPresentTick,
                BranchTick = _revisionBranchTick,
                TargetIds = new List<int>(_revisionTargetIds),
            };
            bool finalChance = _revisionCombat.FinalChance;
            FightOutcome original = _lastFightOutcome;
            RevisionEffectKind effect =
                RevisionCatalog.Get(_run.State.Revision.RevisionId).Effect;
            FightOutcome revised = _run.CommitRevision(_preparedFight, choice);
            _revisionCombat.Receipt = RevisionReceiptBuilder.Build(
                original?.Battle, revised.Battle, choice.BranchTick, effect);
            _lastFightOutcome = revised;
            _lastBattle = revised.Battle;
            FinalizeCommittedFight(revised);
            LogLine(() => _telemetry.RevisionLine(
                _run.State, DateTime.UtcNow, finalChance, choice, original, revised));
            _revisionCombat.Mode = RevisionCombatMode.Rewinding;
            _revisionCombat.Presentation = RevisionPresentationPhase.Tear;
            _revisionCombat.CanConfirm = false;
            _revisionCombat.Status = "The witnessed future is being unwritten…";
            UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                targetId: "revision-combat",
                tone: UiFeedbackTone.Major);
            Rebuild();
            SetRevisionCinematicLock(true);
            _revisionCeremony = StartCoroutine(PlayRevisionTransition(revised, effect));
        }
        catch (Exception ex)
        {
            _revisionCombat.Status = ex.Message;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error,
                targetId: "revision-combat",
                tone: UiFeedbackTone.Negative);
            Rebuild();
        }
    }

    private IEnumerator PlayRevisionTransition(
        FightOutcome revised,
        RevisionEffectKind effect)
    {
        if (_player == null) yield break;
        StopRevisionScrub();
        _player.SetRevisionReducedMotion(_reducedMotion);
        RevisionPresentationTune tune = RevisionTune();
        SfxPlayer.StopRevisionLoop();
        if (RevisionAudioEnabled())
            SfxPlayer.Play("revision_tear", SfxBus.Revision);

        _player.RenderRevisionFrame(_revisionPresentTick);
        UpdateRevisionScreenTargets();
        RevisionScreenEffect.RequestWitnessedFutureCapture();
        _player.SetRevisionFreeze(true);
        float tearSeconds = _reducedMotion ? 0.08f : tune.tearSeconds;
        yield return RevisionBeat(tearSeconds, t =>
        {
            SetRevisionPresentation(
                RevisionPresentationPhase.Tear,
                Mathf.SmoothStep(0f, 1f, t),
                "THE WITNESSED HOUR",
                "IS NO LONGER TRUE");
        });

        // The committed branch does not open at the anchor: it opens on already-witnessed ground a
        // few seconds earlier and runs INTO the split (Jake 2026-07-29). The revised battle is a
        // re-simulation of the same opening under the same seed, so every tick before BranchTick is
        // frame-identical to what the player already watched — which is exactly what makes the
        // divergence legible when it arrives. Reduced Motion keeps the old cut-to-anchor landing.
        int runUpTicks = _reducedMotion
            ? 0
            : Mathf.RoundToInt(Mathf.Max(0f, tune.runUpSeconds) * 10f);
        int runUpTick = Mathf.Max(0, _revisionBranchTick - runUpTicks);
        runUpTicks = _revisionBranchTick - runUpTick;
        float rewindSeconds = _reducedMotion
            ? tune.reducedRewindSeconds
            : Mathf.Min(
                tune.rewindMaxSeconds,
                tune.rewindBaseSeconds +
                tune.rewindPerSecond *
                Mathf.Max(0f, (_revisionPresentTick - runUpTick) / 10f - 1f));
        if (RevisionAudioEnabled())
        {
            SfxPlayer.StartRevisionLoop("revision_rewind_bed");
            SfxPlayer.Play("revision_rewind_riser", SfxBus.Revision);
        }
        yield return RevisionBeat(rewindSeconds, t =>
        {
            float eased = t * t * (3f - 2f * t);
            float clock = Mathf.Lerp(
                _revisionPresentTick, runUpTick - 1, eased);
            if (_reducedMotion)
            {
                // A cut, not a walk: reconstruct once, at the destination.
                if (t >= 1f)
                {
                    _player.RenderRevisionFrame(clock);
                    _player.SetRevisionFreeze(true);
                }
            }
            else
            {
                // The board was already torn down at the present, so the sweep back rides the cheap
                // re-fold. The dress is held from the tear and nothing here disturbs it.
                _player.ScrubRevisionFrame(clock);
                _player.SetRevisionRewindEchoes(
                    _revisionTargetIds,
                    clock,
                    _revisionPresentTick,
                    effect,
                    t);
                // R1 "Undertow": a global signal that survives a frame where nobody happens to move.
                _player.SetRevisionSand(t, -t * 1.6f);
            }
            if (RevisionAudioEnabled())
                SfxPlayer.ShapeRevisionLoop(
                    Mathf.Lerp(0.62f, 0.82f, eased),
                    Mathf.Lerp(0.92f, 1.08f, eased));
            SetRevisionPresentation(
                RevisionPresentationPhase.Rewind,
                eased,
                $"UNWRITING  −{_revisionCombat.SelectedSeconds}s",
                "SAND RUNS BACKWARD");
        });
        _player.ClearRevisionRewindEchoes();

        _player.PrepareRevisionBranch(revised.Battle, runUpTick);
        _player.SetRevisionFreeze(true);
        float vacuumSeconds = _reducedMotion ? 0.04f : tune.vacuumSeconds;
        yield return RevisionBeat(vacuumSeconds, t =>
        {
            if (RevisionAudioEnabled())
                SfxPlayer.ShapeRevisionLoop(
                    Mathf.Lerp(0.82f, 0f, t),
                    Mathf.Lerp(1.08f, 0.72f, t));
            SetRevisionPresentation(
                RevisionPresentationPhase.Vacuum, t, "", "");
        });
        SfxPlayer.StopRevisionLoop();

        float landingFrom = 1f;
        if (runUpTicks > 0)
        {
            // Re-tread the witnessed ground under a half-held dress, then hand the split itself to
            // the landing beat. The player halts one tick short of the branch so the intervention is
            // struck under the punch instead of slipping past in an ordinary frame.
            landingFrom = Mathf.Clamp01(tune.runUpDress);
            _player.LandRevisionBranch(runUpTick);
            _player.SetRevisionFreeze(landingFrom);
            _player.PlayRevisionRunUp(_revisionBranchTick);
            float guard = 0f;
            while (_player.RevisionRunUpRunning && guard < RevisionRunUpGuardSeconds)
            {
                guard += Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                float remaining = Mathf.Max(0f, (_revisionBranchTick - _player.CurrentTick) / 10f);
                SetRevisionPresentation(
                    RevisionPresentationPhase.RunUp,
                    Mathf.Clamp01(1f - remaining / (runUpTicks / 10f)),
                    "THE HOUR RUNS AGAIN",
                    $"THE SPLIT ARRIVES IN {remaining:0.0}s");
                yield return null;
            }
        }

        _revisionCombat.Mode = RevisionCombatMode.Landing;
        _player.LandRevisionBranch(_revisionBranchTick);
        // Hold ON the fork before the punch. The branch was being struck and play resumed inside a
        // third of a second, so the one moment the whole mechanic exists for read as a flicker.
        float forkSeconds = _reducedMotion ? 0.10f : Mathf.Max(0f, tune.forkHoldSeconds);
        if (forkSeconds > 0f)
            yield return RevisionBeat(forkSeconds, t =>
            {
                _player.StepRevisionPresentation(Time.unscaledDeltaTime);
                _player.SetRevisionFreeze(landingFrom);
                _player.SetRevisionFork(_revisionTargetIds, t);
                SetRevisionPresentation(
                    RevisionPresentationPhase.Landing,
                    0f,
                    "THE HOUR SPLITS",
                    effect == RevisionEffectKind.BorrowedFuture
                        ? "MANA CROSSES HERE"
                        : "THE FORMATION RETURNS HERE");
            });
        _player.ClearRevisionSand();
        float landingSeconds = _reducedMotion ? 0.18f : tune.landingSeconds;
        yield return RevisionBeat(landingSeconds, t =>
        {
            _player.StepRevisionPresentation(Time.unscaledDeltaTime);
            _player.SetRevisionFreeze(landingFrom * (1f - Mathf.SmoothStep(0f, 1f, t)));
            SetRevisionPresentation(
                RevisionPresentationPhase.Landing,
                t,
                effect == RevisionEffectKind.BorrowedFuture
                    ? "THE FUTURE ARRIVES EARLY"
                    : "FORMATION RESTORED",
                effect == RevisionEffectKind.BorrowedFuture
                    ? "MANA CROSSES THE SPLIT"
                    : "THE ENEMY RETURNS DISARMED");
        });

        _revisionCombat.Mode = RevisionCombatMode.Receipt;
        Rebuild();
        float receiptSeconds = _reducedMotion
            ? tune.reducedReceiptSeconds
            : tune.receiptSeconds + tune.receiptTailSeconds;
        yield return RevisionBeat(receiptSeconds, t =>
        {
            _player.StepRevisionPresentation(Time.unscaledDeltaTime);
            _player.SetRevisionFreeze(false);
            SetRevisionPresentation(
                RevisionPresentationPhase.Receipt,
                t,
                receipt: _revisionCombat.Receipt);
        });

        _revisionCombat.Mode = RevisionCombatMode.Revised;
        _revisionCombat.Presentation = RevisionPresentationPhase.None;
        _revisionCombat.PresentationProgress = 0f;
        _revisionCombat.FinalChance = false;
        _revisionCombat.Status = "This battle's split is spent.";
        RevisionScreenEffect.Clear();
        _player.SetRevisionFreeze(false);
        _player.ResumeRevisionBattle();
        SetRevisionCinematicLock(false);
        _revisionCeremony = null;
        Rebuild();
    }

    /// <summary>
    /// One unscaled ceremony beat. The per-frame step is wrapped because an exception thrown inside
    /// it kills the whole coroutine, and the ceremony is what releases the cinematic lock — so a
    /// decorative failure used to take the fight down with it (Jake hit exactly that: a stale VFX
    /// pool reference froze the rewind). Presentation errors are logged and the beat plays on.
    /// </summary>
    private IEnumerator RevisionBeat(float seconds, Action<float> step)
    {
        seconds = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;
        StepRevisionBeat(step, 0f);
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            StepRevisionBeat(step, Mathf.Clamp01(elapsed / seconds));
        }
    }

    private bool _revisionBeatFaulted;

    private void StepRevisionBeat(Action<float> step, float t)
    {
        if (step == null) return;
        try
        {
            step(t);
        }
        catch (Exception ex)
        {
            if (_revisionBeatFaulted) return;   // one report per ceremony, not one per frame
            _revisionBeatFaulted = true;
            Debug.LogError($"[RunShell] Revision presentation step failed: {ex}");
        }
    }

    private void SetRevisionPresentation(
        RevisionPresentationPhase phase,
        float progress,
        string title = "",
        string subtitle = "",
        RevisionReceipt receipt = null)
    {
        RevisionScreenEffect.SetPhase(phase, progress);
        _revisionCombatOverlay?.SetPresentation(
            phase, progress, title, subtitle, receipt);
    }

    private void UpdateRevisionScreenTargets()
    {
        if (_player != null)
        {
            int count = _player.GetRevisionTargetViewportPositions(
                _revisionTargetIds, out Vector4 positions);
            RevisionScreenEffect.SetTargetViewportPositions(positions, count);
            return;
        }
        RevisionScreenEffect.SetTargetViewportPositions(
            new Vector4(0.49f, 0.46f, 0.49f, 0.46f), 0);
    }

    private RevisionPresentationTune RevisionTune() =>
        _tuning?.data?.revision ?? new RevisionPresentationTune();

    private bool RevisionAudioEnabled() =>
        _tuning?.data?.audio?.enabled == true;

    private void SetRevisionCinematicLock(bool locked)
    {
        _fightSkip?.SetEnabled(!locked);
        _fightOptions?.SetEnabled(!locked);
        if (_fightSkip != null)
            _fightSkip.style.display = locked ? DisplayStyle.None : DisplayStyle.Flex;
        if (_fightOptions != null)
            _fightOptions.style.display = locked ? DisplayStyle.None : DisplayStyle.Flex;
        if (_fightHint != null)
            _fightHint.style.display = locked || _revisionCombat.Mode == RevisionCombatMode.Selecting
                ? DisplayStyle.None
                : DisplayStyle.Flex;
    }

    private void CancelRevision()
    {
        if (_revisionCombat.Mode != RevisionCombatMode.Selecting) return;
        if (_revisionCombat.FinalChance)
        {
            AcceptOriginalFate();
            return;
        }
        _revisionTargetIds.Clear();
        _revisionCombat.Targets.Clear();
        _revisionCombat.Mode = RevisionCombatMode.Ready;
        _revisionCombat.CanConfirm = false;
        _revisionCombat.FinalChance = false;
        _revisionCombat.Status = "The original future continues.";
        _revisionCombat.Presentation = RevisionPresentationPhase.None;
        _revisionCombat.PresentationProgress = 0f;
        RevisionScreenEffect.Clear();
        SfxPlayer.StopRevisionLoop();
        StopRevisionScrub();
        _revisionScrubClock = -1f;
        _player?.SetRevisionFreeze(false);
        if (_player != null && _lastBattle != null)
        {
            _player.PlayBattleFrom(_lastBattle, _revisionPresentTick + 1);
            if (_revisionFatalTick > _revisionPresentTick + 1)
                _player.ArmRevisionPauseBefore(_revisionFatalTick);
        }
        SetRevisionCinematicLock(false);
        Rebuild();
    }

    private void OnRevisionFinalChance()
    {
        if (_preparedFight == null || _lastFightOutcome == null || _lastFightOutcome.Won) return;
        OpenRevision(true);
    }

    private void AcceptOriginalFate()
    {
        if (_preparedFight == null) return;
        SfxPlayer.StopRevisionLoop();
        StopRevisionScrub();
        _revisionScrubClock = -1f;
        FightOutcome original = _run.CommitOriginal(_preparedFight);
        _lastFightOutcome = original;
        _lastBattle = original.Battle;
        FinalizeCommittedFight(original);
        _revisionCombat.Mode = RevisionCombatMode.Revised;
        _revisionCombat.Presentation = RevisionPresentationPhase.None;
        _revisionCombat.PresentationProgress = 0f;
        _revisionCombat.FinalChance = false;
        _revisionCombat.Status = "The original future was accepted.";
        RevisionScreenEffect.Clear();
        _player?.SetRevisionFreeze(false);
        if (_player != null && _revisionFatalTick > 0)
            _player.PlayBattleFrom(original.Battle, _revisionFatalTick);
        else
            OpenFightResult();
        SetRevisionCinematicLock(false);
        Rebuild();
    }

    private void FinalizeCommittedFight(FightOutcome outcome)
    {
        _lastFightOutcome = outcome;
        _lastBattle = outcome.Battle;
        _fightsCompleted++;
        LogFight(_pendingFightKind, _pendingFightBrief, outcome);
        var plan = HubFlowPlanner.Plan(
            _pendingFightBefore,
            RunMutationSnapshot.Capture(_run.State));
        RecordHubPlan(plan, navigateBlocking: false);
        BuildConclusionReceiptIfNeeded();
        _preparedFight = null;
        _pendingFightBefore = null;
        _pendingFightBrief = null;
    }

    private void ResetRevisionCombat()
    {
        if (_revisionCeremony != null)
        {
            StopCoroutine(_revisionCeremony);
            _revisionCeremony = null;
        }
        StopRevisionScrub();
        _revisionScrubClock = -1f;
        SfxPlayer.StopRevisionLoop();
        RevisionScreenEffect.Clear();
        _preparedFight = null;
        _pendingFightBefore = null;
        _pendingFightBrief = null;
        _revisionTargetIds.Clear();
        _revisionPresentTick = -1;
        _revisionBranchTick = -1;
        _revisionFatalTick = -1;
        _revisionOpenedOnce = false;
        _revisionCombat.Mode = RevisionCombatMode.Hidden;
        _revisionCombat.FinalChance = false;
        _revisionCombat.CanOpen = false;
        _revisionCombat.CanConfirm = false;
        _revisionCombat.Targets.Clear();
        _revisionCombat.Presentation = RevisionPresentationPhase.None;
        _revisionCombat.PresentationProgress = 0f;
        _revisionCombat.Receipt = null;
        _player?.SetRevisionFreeze(false);
        SetRevisionCinematicLock(false);
    }

    private void OnFightWatched()
    {
        if (_preparedFight != null)
        {
            if (_lastFightOutcome != null && !_lastFightOutcome.Won)
            {
                OnRevisionFinalChance();
                return;
            }
            FightOutcome original = _run.CommitOriginal(_preparedFight);
            FinalizeCommittedFight(original);
            _revisionCombat.Mode = RevisionCombatMode.Revised;
            _revisionCombat.Status = "This battle's future was accepted.";
        }
        OpenFightResult();
    }

    /// <summary>Where the run stands once a node is done — the run layer already decided.</summary>
    private void GoAfterNode() =>
        AdvanceFromResolvedBeat();

    private void AdvanceFromResolvedBeat()
    {
        if (_run.State.Over)
        {
            Go(RunScreen.RunOver);
            return;
        }
        if (_run.State.Phase == RunPhase.Planning)
        {
            _placement.Clear();
            SelectDefaultForTab();
        }
        _inspectorOpen = false;
        _tierChosen = false;
        OpenHallStation(_pendingHubPlan?.RecommendedStation ?? HallStation.Market);
    }

    private void OpenFightResult()
    {
        if (_lastFightOutcome == null || _lastBattle == null)
        {
            AdvanceFromResolvedBeat();
            return;
        }
        _revisionCombat.Mode = RevisionCombatMode.Hidden;
        _revisionCombat.FinalChance = false;
        RevisionScreenEffect.Clear();
        _player?.SetRevisionFreeze(false);
        _resultGateOpen = true;
        CloseFightInspector();
        Rebuild();
    }

    private void WatchFightAgain()
    {
        if (!_resultGateOpen || _lastBattle == null || _player == null) return;
        _resultGateOpen = false;
        _resultGateView?.Hide();
        ResetRevisionCombat();
        _player.PlaybackEnded -= OnFightWatched;
        _player.PlaybackEnded += OnFightWatched;
        _player.PlayBattle(_lastBattle);
        Rebuild();
    }

    private void ContinueFightResult()
    {
        if (!_resultGateOpen || _run == null) return;
        _resultGateOpen = false;
        _resultGateView?.Hide();
        _tierChosen = false;
        _placement.Clear();
        _inspectorOpen = false;

        if (_run.State.Over)
        {
            Go(RunScreen.RunOver);
            return;
        }

        HallStation destination = _pendingHubPlan?.RecommendedStation ?? HallStation.Market;
        OpenHallStation(destination);
    }

    private void BuildConclusionReceiptIfNeeded()
    {
        if (_run == null || !_run.State.Over) return;
        _conclusionReceipt = new RunConclusionReceipt
        {
            Victory = _run.State.Victory,
            ActReached = _run.State.Act,
            FightsCompleted = _fightsCompleted,
            Sand = _run.State.Sand,
            FieldedHeroes = _run.State.Field.Count,
            FinalCause = _run.State.EndlessDefeat
                ? "Beyond the Hour defeat (authored victory preserved)"
                : _run.State.Victory ? "Final boss defeated" : "Warband defeated",
        };
    }

    // ---- view plumbing -----------------------------------------------------------

    private void BuildUI()
    {
        var document = GetComponent<UIDocument>();
        _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        var theme = Resources.Load<ThemeStyleSheet>("DebugTheme");
        if (theme != null) _panelSettings.themeStyleSheet = theme;
        UiPanelProfile.ConfigureShipping(_panelSettings, 700);
        document.panelSettings = _panelSettings;
        document.sortingOrder = 700;

        _root = document.rootVisualElement;
        UiStyleCatalog.AttachShipping(_root, "RunShell");
        _safeAreaFrame = new VisualElement { name = "ui-safe-area-frame" };
        _safeAreaFrame.AddToClassList("ui-safe-area-frame");
        _root.Add(_safeAreaFrame);
        _screenHost = new VisualElement { name = "ui-screen-host" };
        _screenHost.AddToClassList("ui-screen-host");
        _safeAreaFrame.Add(_screenHost);
        _uiEnvironment = new UiEnvironment(
            _root, _safeAreaFrame, _reducedMotion);

        _runtimeTooltips = new RuntimeTooltipService(
            _root, HubPresentationConfig.Load());

        _views.Add(new MenuView(_actions));
        _views.Add(new WorkbenchView(
            _actions, _runtimeTooltips, _hallEnvironment?.Services));
        _views.Add(new WagerView(_actions));
        _views.Add(new DeployView(_actions, _runtimeTooltips));
        _views.Add(new RunOverView(_actions));
        foreach (var v in _views) _screenHost.Add(v.Root);

        BuildFightOverlay();
        _warbandBarView = new WarbandBarView(
            _actions, _safeAreaFrame, _runtimeTooltips);
        _inscriptionRail = new InscriptionRailView(_safeAreaFrame, _runtimeTooltips);
        _railIndicators = new InscriptionIndicatorLayer();
        _safeAreaFrame.Add(_railIndicators);
        // Item 9: the options modal lives in the persistent layer so one implementation serves
        // Menu, Hall and fight alike. Reduced motion re-runs the same seam the Flow Lab toggle
        // uses; battle speed re-reads tuning through the player's own hot-reload entry.
        _optionsPanel = new OptionsPanel(
            onReducedMotion: v => { _reducedMotion = v; Rebuild(); },
            onBattleSpeed: PushBattleSpeed);
        _safeAreaFrame.Add(_optionsPanel.Root);
        BuildRotationGuard();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BuildFlowLab();
#endif
        _rotationGuard.BringToFront();
    }

    private void BuildRotationGuard()
    {
        _rotationGuard = new VisualElement { name = "ui-rotation-guard" };
        _rotationGuard.AddToClassList("ui-rotation-guard");
        _rotationGuard.pickingMode = PickingMode.Position;
        var title = new Label("TURN THE DEVICE");
        title.AddToClassList("ui-rotation-guard__title");
        var copy = new Label(
            "Warband’s first playable is built for landscape. Rotate to return to the Tower.");
        copy.AddToClassList("ui-rotation-guard__copy");
        _rotationGuard.Add(title);
        _rotationGuard.Add(copy);
        _root.Add(_rotationGuard);
    }

    /// <summary>
    /// The Fight screen deliberately has no view — the board IS the screen. But that means the
    /// ONLY way out is PlaybackEnded, and if that never fires (a dropped event, a paused player
    /// loop, a zero-length fight) the player is stranded on a board with no UI at all. This is
    /// the guaranteed exit: always available, never blocking, and it also serves the impatient —
    /// the fight is already decided, so skipping costs nothing but the spectacle.
    /// </summary>
    private void BuildFightOverlay()
    {
        _fightOverlay = new VisualElement();
        _fightOverlay.AddToClassList("fight-overlay");
        _fightOverlay.pickingMode = PickingMode.Ignore;

        _fightHitSurface = new VisualElement();
        _fightHitSurface.AddToClassList("fight-hit-surface");
        _fightHitSurface.pickingMode = PickingMode.Position;
        _fightHitSurface.RegisterCallback<PointerDownEvent>(InspectFightUnit);
        _fightOverlay.Add(_fightHitSurface);

        _fightHint = new Label("Click a unit to inspect");
        _fightHint.AddToClassList("fight-inspect-hint");
        _fightHint.pickingMode = PickingMode.Ignore;
        _fightOverlay.Add(_fightHint);

        _revisionCombatOverlay = new RevisionCombatOverlay(
            OpenRevision,
            SelectRevisionSeconds,
            ShiftRevisionSeconds,
            ConfirmRevision,
            CancelRevision);
        _fightOverlay.Add(_revisionCombatOverlay.Root);

        _fightSkip = new Button(SkipFight) { text = "SKIP ▶" };
        _fightSkip.AddToClassList("btn");
        _fightSkip.AddToClassList("btn--ghost");
        _fightSkip.AddToClassList("fight-skip");
        _fightSkip.pickingMode = PickingMode.Position;
        _fightOverlay.Add(_fightSkip);

        // Item 9: mid-fight is where mute and battle speed are actually wanted; the board has no
        // other chrome to hang them off, so the overlay carries the entry beside SKIP.
        _fightOptions = new Button(() => _optionsPanel?.Open()) { text = "OPTIONS" };
        _fightOptions.AddToClassList("btn");
        _fightOptions.AddToClassList("btn--ghost");
        _fightOptions.AddToClassList("fight-options");
        _fightOptions.pickingMode = PickingMode.Position;
        _fightOverlay.Add(_fightOptions);

        // The card is a FLOATING plate, not a modal. The fight never pauses (Jake, 2026-07-29),
        // so a scrim over a running battle would hide the thing it is explaining. It picks —
        // that is what makes keyword tooltips legal on it — but it is the ONLY picking region
        // added here, so every click outside it still reaches the board through the hit surface.
        _fightCardRing = new VisualElement { name = "fight-card-ring" };
        _fightCardRing.AddToClassList("fight-card-ring");
        _fightCardRing.pickingMode = PickingMode.Ignore;
        _fightOverlay.Add(_fightCardRing);

        _fightCard = new VisualElement { name = "fight-card" };
        _fightCard.AddToClassList("fight-card");
        _fightCard.pickingMode = PickingMode.Position;
        var close = new Button(CloseFightInspector) { text = "×" };
        close.AddToClassList("btn");
        close.AddToClassList("btn--ghost");
        close.AddToClassList("fight-card__close");
        _fightInspector = new InspectorPanel(_ => { }, null, _runtimeTooltips);
        _fightInspector.Root.AddToClassList("wb-inspector--combat");
        _fightCard.Add(_fightInspector.Root);
        _fightCard.Add(close);
        close.BringToFront();
        _fightOverlay.Add(_fightCard);
        _fightOverlay.schedule.Execute(RefreshFightInspector).Every(150);
        _resultGateView = new ResultGateView(_actions);
        _fightOverlay.Add(_resultGateView.Root);
        _screenHost.Add(_fightOverlay);
        CloseFightInspector();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BuildFlowLab()
    {
        _flowLab = new VisualElement();
        _flowLab.AddToClassList("flow-lab");
        _flowLab.pickingMode = PickingMode.Position;
        _flowLab.Add(new Label("F2 · UI FLOW LAB") { name = "flow-lab-title" });

        AddFlowLabButton("MUSTER STATE", () => _actions.NewRun?.Invoke());
        AddFlowLabButton("MUSTER · NEW OFFER", () =>
        {
            if (_muster) _actions.RerollSeed?.Invoke();
        });
        AddFlowLabButton("MUSTER · SELECT", () =>
            UiPolishSignals.Preview(UiTransactionKind.MusterSelect));
        AddFlowLabButton("MUSTER · DESELECT", () =>
            UiPolishSignals.Preview(UiTransactionKind.MusterDeselect));
        AddFlowLabButton("OVERVIEW", OpenHallOverview);
        AddFlowLabButton("MARKET", () => OpenHallStation(HallStation.Market));
        AddFlowLabButton("WARBAND", () => OpenHallStation(HallStation.Warband));
        AddFlowLabButton("ARMORY", () => OpenHallStation(HallStation.Armory));
        AddFlowLabButton("HOURSTONE", () => OpenHallStation(HallStation.Hourstone));
        AddFlowLabButton("RESULT", () =>
        {
            if (_lastBattle == null || _lastFightOutcome == null) return;
            Go(RunScreen.Fight);
            OpenFightResult();
        });
        AddFlowLabButton("REDUCED MOTION", () =>
        {
            _reducedMotion = !_reducedMotion;
            PlayerPrefs.SetInt("ui.reducedMotion", _reducedMotion ? 1 : 0);
            Rebuild();
        });
        AddFlowLabButton("PHONE COMPOSITION", () =>
        {
            _debugPhoneLayout = !_debugPhoneLayout;
            Rebuild();
        });
        AddFlowLabButton("FX · REVEAL", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Reveal));
        AddFlowLabButton("FX · SELECT", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Select));
        AddFlowLabButton("FX · TOOLTIP", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.TooltipReveal));
        AddFlowLabButton("FX · PIN", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Pin));
        AddFlowLabButton("FX · DRAWER", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.DrawerExpand));
        AddFlowLabButton("FX · SOCKET", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.SocketWake));
        AddFlowLabButton("FX · PROJECT", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.ProjectedTarget));
        AddFlowLabButton("FX · COMMIT", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Purchase));
        AddFlowLabButton("FX · BUY RANK", () =>
            UiPolishSignals.Preview(UiTransactionKind.BuyRank));
        AddFlowLabButton("FX · BUY GEAR", () =>
            UiPolishSignals.Preview(UiTransactionKind.BuyWeapon));
        AddFlowLabButton("FX · BIND", () =>
            UiPolishSignals.Preview(UiTransactionKind.BindInscription));
        AddFlowLabButton("FX · EQUIP", () =>
            UiPolishSignals.Preview(UiTransactionKind.Equip));
        AddFlowLabButton("FX · FORGE", () =>
            UiPolishSignals.Preview(UiTransactionKind.Reforge));
        AddFlowLabButton("FX · ROUTE", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Route));
        AddFlowLabButton("FX · ERROR", () =>
            UiPolishSignals.Preview(UiPolishSignals.Cue.Error));
        AddFlowLabButton("VALIDATE FLOW", () =>
        {
            HubFlowContract.Validate();
            UiPresentationContract.Validate();
            MarketOfferPresentationContract.Validate(_model.Planning.MarketOffers);
            Debug.Log("[HubFlowContract] Route, UI, and Market checks passed.");
        });
        AddFlowLabButton("CLOSE", () =>
        {
            _flowLabVisible = false;
            _flowLab.style.display = DisplayStyle.None;
        });

        _flowLab.style.display = DisplayStyle.None;
        _root.Add(_flowLab);
    }

    private void AddFlowLabButton(string text, Action action)
    {
        var button = new Button(action) { text = text };
        button.AddToClassList("flow-lab__button");
        _flowLab.Add(button);
    }

#endif

    private void InspectFightUnit(PointerDownEvent evt)
    {
        if (_model.Screen != RunScreen.Fight || evt.button != 0 || _player == null) return;
        Vector2 screenPosition =
            PanelToScreen(new Vector2(evt.position.x, evt.position.y));
        PlaybackUnit picked = _revisionCombat.Mode == RevisionCombatMode.Selecting
            ? _player.PickUnit(
                screenPosition, 16f, EligibleRevisionTargetIds())
            : _player.PickUnit(screenPosition, 14f);
        if (_revisionCombat.Mode == RevisionCombatMode.Selecting)
        {
            ToggleRevisionTarget(picked);
            evt.StopPropagation();
            return;
        }
        if (_revisionCombat.Mode == RevisionCombatMode.Opening ||
            _revisionCombat.Mode == RevisionCombatMode.Rewinding ||
            _revisionCombat.Mode == RevisionCombatMode.Landing ||
            _revisionCombat.Mode == RevisionCombatMode.Receipt)
        {
            evt.StopPropagation();
            return;
        }
        if (picked == null)
        {
            CloseFightInspector();
            return;
        }
        // Clicking a DIFFERENT body swaps the subject in place — cards never stack.
        _fightInspectedUnit = picked;
        _fightCard.style.display = DisplayStyle.Flex;
        RefreshFightInspector();
        evt.StopPropagation();
    }

    private void RefreshFightInspector()
    {
        if (_model.Screen != RunScreen.Fight || _fightInspectedUnit == null ||
            _fightCard == null || _fightCard.style.display != DisplayStyle.Flex)
            return;
        // Re-read the fold rather than the captured object: a subject that dies mid-inspection
        // keeps its card open and flips to DEFEATED. Vanishing the card under the cursor because
        // the unit fell is how you lose the answer to "what just killed it".
        PlaybackUnit live = _player?.FindUnit(_fightInspectedUnit.Id) ?? _fightInspectedUnit;
        _fightInspectedUnit = live;
        _fightInspector.Bind(PlaybackInspector(live));
        PositionFightCard(live);
    }

    /// <summary>
    /// Float the card beside its subject without covering it, then clamp it inside the safe-area
    /// frame. Right of the body by default, flipped left when the right side has no room; the
    /// vertical anchor centres on the body and clamps rather than flipping, because a card that
    /// jumps above/below as a unit walks reads as a glitch.
    /// </summary>
    private void PositionFightCard(PlaybackUnit unit)
    {
        if (_fightCard == null || unit == null) return;
        float w = _fightCard.resolvedStyle.width;
        float h = _fightCard.resolvedStyle.height;
        if (float.IsNaN(w) || w < 1f) w = 388f;
        if (float.IsNaN(h) || h < 1f) h = 460f;

        float frameW = _safeAreaFrame.resolvedStyle.width;
        float frameH = _safeAreaFrame.resolvedStyle.height;
        if (float.IsNaN(frameW) || frameW < 1f) frameW = Screen.width;
        if (float.IsNaN(frameH) || frameH < 1f) frameH = Screen.height;

        const float margin = 12f;
        const float gap = 26f;
        Rect screenRect = default;
        bool located = _player != null &&
                       _player.TryGetUnitScreenRect(unit.Id, out screenRect);
        if (!located)
        {
            // No projection (off-camera, or a frozen preview): park it top-right rather than
            // leaving it wherever the last living subject put it.
            _fightCard.style.left = Mathf.Max(margin, frameW - w - margin);
            _fightCard.style.top = margin;
            SetDisplayed(_fightCardRing, false);
            return;
        }

        Vector2 bodyTop = ScreenToPanel(new Vector2(screenRect.center.x, screenRect.yMax));
        Vector2 bodyBottom = ScreenToPanel(new Vector2(screenRect.center.x, screenRect.yMin));
        float bodyH = Mathf.Abs(bodyBottom.y - bodyTop.y);
        float bodyW = screenRect.width * (frameW / Mathf.Max(1f, Screen.width));

        float left = bodyTop.x + bodyW * 0.5f + gap;
        if (left + w > frameW - margin) left = bodyTop.x - bodyW * 0.5f - gap - w;
        left = Mathf.Clamp(left, margin, Mathf.Max(margin, frameW - w - margin));

        float top = bodyTop.y + bodyH * 0.5f - h * 0.5f;
        top = Mathf.Clamp(top, margin, Mathf.Max(margin, frameH - h - margin));

        _fightCard.style.left = left;
        _fightCard.style.top = top;

        // The ring is what ties the card to a body — without it the card is just a panel that
        // appeared, and on a crowded board you cannot tell which unit it belongs to.
        float ring = Mathf.Max(34f, bodyW * 1.35f);
        SetDisplayed(_fightCardRing, true);
        _fightCardRing.style.width = ring;
        _fightCardRing.style.height = ring;
        _fightCardRing.style.left = bodyTop.x - ring * 0.5f;
        _fightCardRing.style.top = bodyTop.y + bodyH * 0.5f - ring * 0.5f;
        _fightCardRing.EnableInClassList("fight-card-ring--enemy", unit.Team != 0);
    }

    private void CloseFightInspector()
    {
        _fightInspectedUnit = null;
        if (_fightCard != null) _fightCard.style.display = DisplayStyle.None;
        if (_fightCardRing != null) SetDisplayed(_fightCardRing, false);
    }

    /// <summary>True when the card took the Escape key, so the caller stops there instead of
    /// also opening the options modal behind it.</summary>
    private bool FightCardHandledEscape()
    {
        if (_model.Screen != RunScreen.Fight || _fightInspectedUnit == null) return false;
        CloseFightInspector();
        return true;
    }

    private void SkipFight()
    {
        if (_model.Screen != RunScreen.Fight) return;
        CloseFightInspector();
        if (_preparedFight != null)
        {
            if (_lastFightOutcome != null && !_lastFightOutcome.Won)
            {
                _player?.PauseForRevision();
                OnRevisionFinalChance();
                return;
            }
            FightOutcome original = _run.CommitOriginal(_preparedFight);
            FinalizeCommittedFight(original);
            _revisionCombat.Mode = RevisionCombatMode.Revised;
            _revisionCombat.Status = "This battle's future was accepted.";
        }
        if (_player != null && _lastBattle != null)
            _player.BuildLoadedPreview(_lastBattle.EndTick);
        OpenFightResult();
    }

    /// <summary>
    /// Item 31: record only coarse run boundaries. The timestamp of the next boundary closes the
    /// previous one, so Hall, wager, deployment, combat, result, and reward dwell become
    /// measurable without logging tabs, hovers, selections, or other click-level behavior.
    /// </summary>
    private void LogPhaseIfChanged()
    {
        if (_telemetry == null || _telemetryWriter == null || _run == null) return;

        string phase = "";
        if (_model.Screen == RunScreen.Fight && _resultGateOpen)
        {
            phase = "result";
        }
        else
        {
            phase = _model.Screen switch
            {
                RunScreen.Management => _model.Planning.BeatKind switch
                {
                    PlanningBeat.RevisionUpgrade => "revisionUpgrade",
                    PlanningBeat.Interlude => "interlude",
                    PlanningBeat.BossReward => "bossReward",
                    _ => "planning",
                },
                RunScreen.Wager => "wager",
                RunScreen.Deploy => "deploy",
                RunScreen.Fight => "fight",
                _ => "",
            };
        }

        if (phase.Length == 0 || phase == _telemetryPhase) return;
        _telemetryPhase = phase;
        LogLine(() => _telemetry.PhaseLine(_run.State, DateTime.UtcNow, phase));
    }

    /// <summary>Item 19: append one line, or silently none — a telemetry failure must never
    /// surface inside a purchase, a fight, or anything else the player is doing.</summary>
    private void LogLine(Func<string> line)
    {
        if (_telemetry == null || _telemetryWriter == null || _run == null) return;
        try { _telemetryWriter.Append(line()); }
        catch (Exception) { /* fail-silent by design */ }
    }

    /// <summary>The fight line is also where the run's end is detected — fights are the only
    /// thing that can end a run, so victory/defeat logs exactly once, here, and the finished
    /// run uploads fire-and-forget.</summary>
    private void LogFight(NodeKind kind, EncounterBrief brief, FightOutcome outcome)
    {
        if (_telemetry == null || _telemetryWriter == null || outcome == null) return;
        try
        {
            FightSummary summary =
                outcome.Battle != null ? FightSummary.Build(outcome.Battle) : null;
            _telemetryWriter.Append(_telemetry.FightLine(
                _run.State, DateTime.UtcNow, kind, _tier,
                brief != null ? brief.Name : "", outcome, summary));
            if (kind == NodeKind.Boss && outcome.Won && _run.State.InEndless)
                _telemetryWriter.Append(
                    _telemetry.EndlessCycleLine(_run.State, DateTime.UtcNow));
            if (_run.State.Over)
            {
                _telemetryWriter.Append(_telemetry.EndLine(_run.State, DateTime.UtcNow));
                StartCoroutine(_telemetryWriter.Upload());
            }
        }
        catch (Exception) { /* fail-silent by design */ }
    }

    /// <summary>
    /// Item 9: a live fight re-reads its pace immediately through the player's own hot-reload
    /// entry (the F1 cockpit's proven path); off the Fight screen there is nothing to push —
    /// the next fight start reads tuning × <see cref="PlayerOptions.BattleSpeed"/> itself.
    /// </summary>
    private void PushBattleSpeed()
    {
        if (Application.isPlaying && _model.Screen == RunScreen.Fight && _player != null)
            _player.ReapplyTuning();
    }

    /// <summary>
    /// Switch screens. The board is cleared on any transition to a screen that is not about the
    /// board — only on the TRANSITION, because Idle() rebuilds the grid and doing that on every
    /// Rebuild would thrash it while the player clicks around a shop.
    /// </summary>
    private void Go(RunScreen screen)
    {
        RunScreen previous = _model.Screen;
        bool changed = previous != screen;
        if (changed && previous == RunScreen.Deploy)
            CancelDeploymentGesture(refreshBoard: false);
        _model.Screen = screen;
        if (screen != RunScreen.Fight) CloseFightInspector();
        if (changed && _player != null && screen != RunScreen.Deploy && screen != RunScreen.Fight)
            _player.Idle();
        Rebuild();
        if (screen == RunScreen.Deploy) ShowDeploymentOnBoard();
    }

    /// <summary>
    /// Adopt a run rebuilt from disk. Deliberately mirrors `BeginRun`'s shell reset rather than
    /// sharing it: every piece of transient shell state — chosen tier, deployment, result gate,
    /// last battle — is fight-scoped and must NOT be inferred from a saved run. A resumed player
    /// lands in the Hall with nothing half-committed, which is also the only state the run layer
    /// guarantees is re-enterable.
    /// </summary>
    private void AdoptResumedRun(RunController run)
    {
        _run = run;
        _muster = false;
        _pendingFirstRevision = false;
        _seed = run.State.Seed;
        // Same run id as before the save (seed + content are both in the state), so a resumed
        // run's lines append to the same trail. No start line — the run already started.
        _telemetryWriter = new RunTelemetryWriter();
        _telemetry = new RunTelemetry(run.State, Application.version);
        _telemetryPhase = "";
        _planningTab = PlanningTab.Market;
        _selectedMarketOffer = run.State.ShopOffers.FindIndex(o => o != null);
        _selectedCardKey = _selectedMarketOffer >= 0
            ? $"market:{_selectedMarketOffer}"
            : "";
        _inspectorOpen = false;
        _tier = FightTier.Fraying;
        _tierChosen = false;
        _hallOverview = false;
        _recommendedStation = HallStation.Breach;
        _hubAttention.Reset();
        _resultGateOpen = false;
        _lastBattle = null;
        _lastFightOutcome = null;
        ResetRevisionCombat();
        _pendingHubPlan = new HubSequencePlan { RecommendedStation = HallStation.Breach };
        _fightsCompleted = 0;              // display-only; the run's own act/beat is authoritative
        _conclusionReceipt = null;
        _equipNowItemInstanceId = 0;
        _equipNowOfferIndex = -1;
        _focusedWarbandHeroId = run.State.Field.Count > 0
            ? run.State.Field[0].InstanceId
            : run.State.Bench.Count > 0 ? run.State.Bench[0].InstanceId : 0;
        _selectedWarbandGearHeroId = 0;
        _selectedWarbandGearKind = -1;
        _placement.Clear();
        _deploySelected = -1;
        _selectedItem = -1;
        _selectedMarketOffer = -1;
        _savedText = RunSave.Write(run.State);   // already on disk; don't rewrite it immediately
        if (_player != null) _player.Idle();
    }

    /// <summary>
    /// Persist after every action (roadmap item 7). `Rebuild` is the shell's single choke point —
    /// every action funnels through it — so hooking here means there is no action that can change
    /// the run without the save following it, which is the only way this stays correct as actions
    /// are added.
    /// </summary>
    private void Autosave()
    {
        if (_run == null) return;
        if (_run.State.Over) { RunSaveFile.Delete(); _savedText = ""; return; }

        string text;
        try { text = RunSave.Write(_run.State); }
        catch (Exception ex)
        {
            // A run the format cannot represent is a content bug, not a player problem. Say so
            // loudly in the console and let them keep playing unsaved.
            Debug.LogError($"[save] this run cannot be serialized: {ex.Message}");
            return;
        }
        if (text == _savedText) return;
        if (RunSaveFile.Save(_run.State)) _savedText = text;
    }

    // Mobile and alt-tab: a suspended app may never get another Rebuild. Cheap insurance.
    private void OnApplicationPause(bool paused) { if (paused) Autosave(); }
    private void OnApplicationQuit() => Autosave();

    private void Rebuild()
    {
        Autosave();
        BuildModel();
        LogPhaseIfChanged();
        _hallEnvironment?.SetVisible(_model.Screen == RunScreen.Management,
            _hallOverview ? HallStation.Overview : TabStation(_planningTab), _reducedMotion);
        ApplyShellLayoutClasses();

        IRunScreenView nextView = null;
        foreach (IRunScreenView view in _views)
            if (view.Screen == _model.Screen)
            {
                nextView = view;
                break;
            }
        if (!ReferenceEquals(_activeView, nextView))
        {
            if (_activeView is IRunScreenLifecycle leaving) leaving.OnScreenExited();
            _activeView = nextView;
            if (_activeView is IRunScreenLifecycle entering) entering.OnScreenEntered();
        }

        foreach (var v in _views)
        {
            bool active = v.Screen == _model.Screen;
            v.Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (active) v.Bind(_model);
        }
        if (_fightOverlay != null)
            _fightOverlay.style.display =
                _model.Screen == RunScreen.Fight ? DisplayStyle.Flex : DisplayStyle.None;
        if (_fightHitSurface != null)
            _fightHitSurface.style.display = _resultGateOpen ? DisplayStyle.None : DisplayStyle.Flex;
        if (_fightHint != null)
            _fightHint.style.display = _resultGateOpen ? DisplayStyle.None : DisplayStyle.Flex;
        if (_fightSkip != null)
            _fightSkip.style.display = _resultGateOpen ? DisplayStyle.None : DisplayStyle.Flex;
        if (_resultGateView != null)
            _resultGateView.Bind(_model.Result, _reducedMotion);
        _revisionCombatOverlay?.Bind(_revisionCombat);
        _warbandBarView?.Bind(_model.WarbandBar);
        _inscriptionRail?.SetCombat(_model.Screen == RunScreen.Fight);
        SyncInscriptionRail();
    }

    /// <summary>Item 5b: push the run's laws into the persistent tray. Ids become words HERE —
    /// display name from content, glyph/accent from PresentationCatalog, full rule through
    /// MechanicalRulePresenter — so the tray can never disagree with the Hourstone tool.</summary>
    private void SyncInscriptionRail() =>
        SyncInscriptionRailFromIds(_run != null
            ? (IReadOnlyList<string>)_run.State.Inscriptions
            : System.Array.Empty<string>());

    private void SyncInscriptionRailFromIds(IReadOnlyList<string> ids)
    {
        if (_inscriptionRail == null) return;
        var entries = new List<InscriptionRailView.Entry>(ids.Count);
        foreach (string id in ids)
        {
            var def = _content.Inscription(id);
            var look = _presentation.Content(id);
            entries.Add(new InscriptionRailView.Entry
            {
                Key = id,
                Icon = look != null && !string.IsNullOrEmpty(look.icon) ? look.icon : "◈",
                Name = def.Name,
                Rule = MechanicalRulePresenter.Inscription(def).Full,
                Accent = look != null ? look.accent : "",
            });
        }
        _inscriptionRail.SetEntries(entries);
    }

    private void ApplyShellLayoutClasses()
    {
        if (_uiEnvironment == null) return;
        _uiEnvironment.SetReducedMotion(_reducedMotion);
        _uiEnvironment.ForcePhoneForDebug(_debugPhoneLayout);
        _uiEnvironment.Refresh();
    }

    // ---- model construction (the only place ids become words) ---------------------

    private void BuildModel()
    {
        BuildMenu();
        BuildResultGate();
        if (_muster)
        {
            BuildMuster();
        }
        else if (_run != null)
        {
            if (!_run.State.Over)
            {
                BuildPlanning();
                BuildWager();
                BuildDeploy();
            }
            BuildRunOver();
        }
        _model.WarbandBar = BuildWarbandBar();
    }

    private WarbandBarModel BuildWarbandBar()
    {
        var bar = new WarbandBarModel { Mode = WarbandBarMode.Hidden };
        if (_muster) return BuildMusterBar();
        if (_run == null) return bar;

        bar.Mode = _model.Screen switch
        {
            RunScreen.Management => WarbandBarMode.HallEditable,
            RunScreen.Wager => WarbandBarMode.WagerReadOnly,
            RunScreen.Deploy => WarbandBarMode.DeploymentSelect,
            RunScreen.Fight when _resultGateOpen => WarbandBarMode.ResultReadOnly,
            _ => WarbandBarMode.Hidden,
        };
        if (bar.Mode == WarbandBarMode.Hidden) return bar;
        bar.Compact = bar.Mode != WarbandBarMode.HallEditable &&
                      bar.Mode != WarbandBarMode.DeploymentSelect;

        RunState state = _run.State;
        bar.FieldCount = state.Field.Count;
        bar.FieldCapacity = state.FieldSlots;
        bar.MaxFieldCapacity = _cfg.MaxFieldSlots;
        bar.ReserveCount = state.Bench.Count;
        bar.ReserveCapacity = _cfg.BenchSlots;
        bar.StoredItems = state.Inventory.Count;
        bar.CanManage = bar.Mode == WarbandBarMode.HallEditable ||
                        bar.Mode == WarbandBarMode.WagerReadOnly;
        bar.CanEdit = bar.Mode == WarbandBarMode.HallEditable &&
                      state.Phase == RunPhase.Planning &&
                      state.PendingSpec == null;
        bar.ArmoryDrawerOpen = _loadoutOpen && bar.Mode == WarbandBarMode.HallEditable;
        // The rank-up modal owns the stage: the rail (outside the workbench scrim) dims.
        bar.Dimmed = bar.Mode == WarbandBarMode.HallEditable &&
                     state.PendingSpec != null;

        if (_focusedWarbandHeroId <= 0 ||
            !_run.TryFindHero(_focusedWarbandHeroId, out _, out _))
            _focusedWarbandHeroId = state.Field.Count > 0
                ? state.Field[0].InstanceId
                : state.Bench.Count > 0 ? state.Bench[0].InstanceId : 0;
        if (TryHeroAddress(_selectedCardKey, out bool selectedBench, out int selectedIndex))
        {
            List<HeroInstance> selectedZone = selectedBench ? state.Bench : state.Field;
            if (selectedIndex >= 0 && selectedIndex < selectedZone.Count)
                _focusedWarbandHeroId = selectedZone[selectedIndex].InstanceId;
        }
        bar.FocusedHeroInstanceId = _focusedWarbandHeroId;

        if (_selectedItem >= 0 && _selectedItem < state.Inventory.Count)
        {
            ItemRef selected = state.Inventory[_selectedItem];
            bar.ArmedInventoryItemInstanceId = selected.InstanceId;
            bar.ArmedInventoryKind = (int)selected.Kind;
        }

        int visibleFieldSlots = bar.Compact ? state.Field.Count : _cfg.MaxFieldSlots;
        for (int i = 0; i < visibleFieldSlots; i++)
        {
            if (i < state.Field.Count)
                bar.Field.Add(BuildWarbandHero(state.Field[i], i, reserve: false, bar));
            else
                bar.Field.Add(new WarbandHeroModel
                {
                    FieldIndex = i,
                    SlotIndex = i,
                    Empty = i < state.FieldSlots,
                    Locked = i >= state.FieldSlots,
                });
        }
        int visibleReserveSlots = bar.Compact ? 0 : _cfg.BenchSlots;
        for (int i = 0; i < visibleReserveSlots; i++)
        {
            if (i < state.Bench.Count)
                bar.Reserve.Add(BuildWarbandHero(state.Bench[i], i, reserve: true, bar));
            else
                bar.Reserve.Add(new WarbandHeroModel
                {
                    SlotIndex = i,
                    Reserve = true,
                    Empty = true,
                });
        }
        return bar;
    }

    /// <summary>Pre-run muster as a workbench state (workbench-frame): the market grid is
    /// the candidate offer, the dossier is the candidate sheet, the rail fills as picks
    /// land. No run exists yet — nothing in here may read <c>_run</c>.</summary>
    private void BuildMuster()
    {
        var p = _model.Planning;
        int capacity = _cfg.StartingFieldSlots;
        int left = RunSetup.PicksRemaining(_picked.Count, _cfg);
        p.Title = "MUSTER YOUR WARBAND";
        p.MusterMode = true;
        p.Act = "BEFORE ACT 1";
        p.Beat = "";
        p.Sand = "";
        p.Capacity = $"{_picked.Count} / {capacity}";
        p.Heading = "MUSTER YOUR WARBAND";
        p.Brief = left > 0
            ? "Choose three champions. Hover a stat or rule for exact mechanics."
            : "Your warband is ready. Begin the run.";
        p.Rule = "";
        p.BeatKind = _pendingFirstRevision
            ? PlanningBeat.StartingRevision
            : PlanningBeat.Fight;
        p.ActiveTab = PlanningTab.Market;
        p.CanReroll = !_pendingFirstRevision;
        p.RerollLabel = "Reroll the muster offer · free";
        p.RerollCost = -1;
        p.CurrencyBalance = 0;
        p.CanCommit = left == 0 && !_pendingFirstRevision;
        p.CommitLabel = left == 0
            ? "BEGIN RUN"
            : $"BEGIN RUN · {_picked.Count} / {capacity}";
        p.ShowRisk = false;
        p.Track = BuildMusterTrack();
        p.Risks = new List<TierChoiceModel>();
        p.Enemies = new List<CardModel>();
        p.Field = new List<CardModel>();
        p.Bench = new List<CardModel>();
        p.Market = new List<CardModel>();
        p.MarketOffers = new List<MarketOfferCardModel>();
        p.Armory = new List<CardModel>();
        p.Inscriptions = new List<CardModel>();
        p.Interlude = new List<InterludeChoiceModel>();
        p.SpecChoice = new SpecChoiceModel();
        p.SlotOfferOpen = false;
        p.SlotOfferText = "";
        p.SlotAffordable = false;
        p.HallOverview = false;
        p.ReducedMotion = _reducedMotion;
        p.ForcePhoneLayout = _debugPhoneLayout;
        p.InspectorOpen = false;
        p.Stations = new List<HallStationModel>();
        p.PartyShelf = new PartyShelfModel();

        if (!_selectedCardKey.StartsWith("muster:", StringComparison.Ordinal) ||
            !_offer.Contains(_selectedCardKey.Substring("muster:".Length)))
            _selectedCardKey = _offer.Count > 0 ? "muster:" + _offer[0] : "";

        foreach (string id in _offer)
        {
            CardModel detail = MusterCandidateCard(id);
            p.Market.Add(detail);
            p.MarketOffers.Add(MusterOfferCard(detail, id));
        }

        InspectorModel inspector = BuildInspector(p);
        if (inspector.Empty)
        {
            inspector.EmptyHint = p.Brief;
        }
        else
        {
            inspector.Eyebrow = "CANDIDATE · " + inspector.Eyebrow;
            inspector.Price = "";
            string selectedId = _selectedCardKey.Substring("muster:".Length);
            int slot = _picked.IndexOf(selectedId);
            bool full = slot < 0 && _picked.Count >= capacity;
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.Buy,
                Label = slot >= 0
                    ? $"RELEASE FROM SLOT {slot + 1}"
                    : $"MUSTER · SLOT {_picked.Count + 1}",
                Primary = true,
                Enabled = !full && !_pendingFirstRevision,
                DisabledReason = full ? "Release a champion first." : "",
            });
        }
        p.Inspector = inspector;

        if (_pendingFirstRevision)
        {
            p.Brief = "Bind one way to alter a battle you have already witnessed. " +
                      "This choice lasts for the run.";
            int option = 0;
            foreach (RevisionDef revision in RevisionCatalog.Starting)
                p.Interlude.Add(new InterludeChoiceModel
                {
                    Path = -1,
                    Option = option++,
                    Card = StartingRevisionCard(revision),
                });
        }
    }

    /// <summary>Act 1's shape (beats + boss) exists before the warband does, but which beat
    /// is the Interlude is seed-derived at run start — no ◆ is promised here.</summary>
    private List<PlanningTrackNodeModel> BuildMusterTrack()
    {
        var result = new List<PlanningTrackNodeModel>();
        for (int i = 0; i <= _cfg.NodesPerAct; i++)
            result.Add(new PlanningTrackNodeModel
            {
                Label = i == _cfg.NodesPerAct ? "BOSS" : (i + 1).ToString(),
                Kind = i == _cfg.NodesPerAct ? "Boss" : "Fight",
                State = i == 0 ? "current" : "future",
            });
        return result;
    }

    private CardModel MusterCandidateCard(string chassisId)
    {
        CardModel card = UnitCardFromDef(
            Loadout.Compose(_content.Chassis(chassisId)).Def,
            "muster:" + chassisId,
            MusterEyebrow(chassisId),
            "RANK C",
            false);
        card.ContentId = chassisId;
        card.PathTiers = BuildPathTiers(chassisId, null, null);
        // Protected commerce state (offer contract): candidates have no price, ever.
        card.Price = "MUSTER";
        return card;
    }

    private string MusterEyebrow(string chassisId)
    {
        PresentationCatalog.UnitPresentation unit = _presentation.Unit(chassisId);
        string role = string.IsNullOrWhiteSpace(unit.musterRole)
            ? "CHAMPION"
            : unit.musterRole;
        return (role + " · " + unit.role).ToUpperInvariant();
    }

    private MarketOfferCardModel MusterOfferCard(CardModel detail, string chassisId)
    {
        var facts = new List<StatChipModel>(detail.Stats);
        for (int i = facts.Count - 1; i >= 0; i--)
            if (string.Equals(facts[i].Label, "CRIT", StringComparison.OrdinalIgnoreCase))
                facts.RemoveAt(i);
        return new MarketOfferCardModel
        {
            Key = detail.Key,
            ContentId = chassisId,
            Kind = MarketOfferKind.Recruit,
            Classification = detail.Eyebrow,
            TierLabel = "RANK C",
            PathTiers = detail.PathTiers,
            MusterSlot = _picked.IndexOf(chassisId),
            Title = detail.Title,
            Subtitle = detail.Subtitle,
            ArtworkResource = detail.PortraitResource,
            ArtworkFallback = detail.PortraitFallback,
            Accent = detail.Accent,
            RuleLabel = detail.AbilityTrigger,
            RuleName = detail.AbilityName,
            ExactRule = detail.AbilitySummary,
            Qualifier = "",
            Price = "MUSTER",
            CurrencyCost = -1,
            CurrencyBalance = -1,
            EconomyState = "",
            Selected = detail.Selected,
            Affordable = true,
            Metrics = OfferFactProfiles.Select(MarketOfferKind.Recruit, facts),
            Detail = detail,
        };
    }

    private CardModel StartingRevisionCard(RevisionDef revision)
    {
        bool borrowed = revision.Effect == RevisionEffectKind.BorrowedFuture;
        string rule = borrowed
            ? "Pause a watched battle, return to an earlier whole second, and carry at " +
              "least 15 Mana from one living champion's future. Excess becomes Shield."
            : "Pause a watched battle, return to an earlier whole second, and send one " +
              "living enemy back to its deployment hex. Disarm it for 1.5 battle-seconds.";
        var evolution = new List<string>();
        foreach (RevisionUpgradeDef[] tier in revision.Tiers)
            evolution.Add(string.Join("  /  ", tier.Select(upgrade => upgrade.Name)));
        string copy = evolution.Count > 0
            ? rule + "\n\nEVOLVES · " + string.Join("  →  ", evolution)
            : rule;
        return new CardModel
        {
            Key = "revision:" + revision.Id,
            ContentId = revision.Id,
            Eyebrow = "REVISION · " + (borrowed ? "MANA" : "CONTROL"),
            Title = revision.Name,
            AbilitySummary = copy,
            Accent = borrowed ? "mana" : "control",
        };
    }

    private WarbandBarModel BuildMusterBar()
    {
        var bar = new WarbandBarModel
        {
            Mode = WarbandBarMode.HallEditable,
            MusterMode = true,
            FieldCount = _picked.Count,
            FieldCapacity = _cfg.StartingFieldSlots,
            MaxFieldCapacity = _cfg.MaxFieldSlots,
            ReserveCapacity = _cfg.BenchSlots,
            CanManage = false,
            CanEdit = false,
        };
        for (int i = 0; i < _cfg.MaxFieldSlots; i++)
        {
            if (i < _picked.Count)
            {
                bar.Field.Add(BuildMusterBarHero(_picked[i], i));
            }
            else if (i == _picked.Count && i < _cfg.StartingFieldSlots)
            {
                bar.Field.Add(new WarbandHeroModel
                {
                    FieldIndex = i,
                    SlotIndex = i,
                    Awaiting = true,
                    AwaitingLabel = i switch
                    {
                        0 => "1ST PICK",
                        1 => "2ND PICK",
                        _ => "3RD PICK",
                    },
                });
            }
            else
            {
                bar.Field.Add(new WarbandHeroModel
                {
                    FieldIndex = i,
                    SlotIndex = i,
                    Empty = i < _cfg.StartingFieldSlots,
                    Locked = i >= _cfg.StartingFieldSlots,
                });
            }
        }
        // No reserve at muster: nothing exists to hold back, so the group stays hidden.
        string selectedId = _selectedCardKey.StartsWith("muster:", StringComparison.Ordinal)
            ? _selectedCardKey.Substring("muster:".Length)
            : "";
        int focusSlot = _picked.IndexOf(selectedId);
        bar.FocusedHeroInstanceId = focusSlot >= 0 ? MusterHeroIdBase + focusSlot : 0;
        return bar;
    }

    private WarbandHeroModel BuildMusterBarHero(string chassisId, int index)
    {
        var lex = ContentLexicon.Chassis(chassisId);
        var presentation = _presentation.Unit(chassisId);
        ChassisDef chassis = _content.Chassis(chassisId);
        UnitDef composed = Loadout.Compose(chassis).Def;
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(composed);
        var model = new WarbandHeroModel
        {
            HeroInstanceId = MusterHeroIdBase + index,
            FieldIndex = index,
            SlotIndex = index,
            Selected = string.Equals(
                _selectedCardKey, "muster:" + chassisId, StringComparison.Ordinal),
            ClassName = lex.Name,
            Role = presentation.role,
            Rank = "C",
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(lex.Name),
            Accent = presentation.accent,
            SignatureIcon = presentation.abilityIcon,
            SignatureName = rules.SignatureName,
            SignatureRule = rules.SignatureText,
            SignatureMana = composed.ManaMax > 0 ? composed.ManaMax : -1,
        };
        model.Weapon = new WarbandEquipmentModel
        {
            Kind = (int)ItemKind.Weapon,
            Icon = "⚔",
            Name = chassis.StarterWeapon.Name,
            Tier = WeaponTier.Worn.ToString().Substring(0, 1).ToUpperInvariant(),
            Rule = WeaponSummary(chassis.StarterWeapon, WeaponTier.Worn),
            Facts = WeaponStats(chassis.StarterWeapon, WeaponTier.Worn),
            Starter = true,
        };
        model.Trinket = new WarbandEquipmentModel
        {
            Kind = (int)ItemKind.Trinket,
            Icon = "◇",
            Name = "Empty trinket socket",
            Empty = true,
        };
        return model;
    }

    private WarbandHeroModel BuildWarbandHero(HeroInstance hero, int index, bool reserve,
                                              WarbandBarModel bar)
    {
        var lex = ContentLexicon.Chassis(hero.ChassisId);
        var presentation = _presentation.Unit(hero.ChassisId);
        WeaponDef weapon = hero.WeaponId == null
            ? _content.Chassis(hero.ChassisId).StarterWeapon
            : _content.Weapon(hero.WeaponId);
        string trinketId = hero.TrinketIds.Count > 0 ? hero.TrinketIds[0] : "";
        UnitDef composed = ComposeHero(hero);
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(composed);
        var model = new WarbandHeroModel
        {
            HeroInstanceId = hero.InstanceId,
            FieldIndex = reserve ? -1 : index,
            SlotIndex = index,
            Reserve = reserve,
            Selected = bar.Mode == WarbandBarMode.DeploymentSelect
                ? !reserve && index == _deploySelected
                : hero.InstanceId == _focusedWarbandHeroId,
            Placed = !reserve && _placement.ContainsKey(index),
            Locked = bar.Mode == WarbandBarMode.DeploymentSelect && reserve,
            ClassName = lex.Name,
            Role = presentation.role,
            Rank = hero.Rank.ToString(),
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(lex.Name),
            Accent = presentation.accent,
            SignatureIcon = presentation.abilityIcon,
            SignatureName = rules.SignatureName,
            SignatureRule = rules.SignatureText,
            SignatureMana = composed.ManaMax > 0 ? composed.ManaMax : -1,
        };

        model.Weapon = new WarbandEquipmentModel
        {
            Kind = (int)ItemKind.Weapon,
            ItemInstanceId = hero.WeaponInstanceId,
            Icon = "⚔",
            Name = weapon.Name,
            Tier = hero.WeaponTier.ToString().Substring(0, 1).ToUpperInvariant(),
            Rule = WeaponSummary(weapon, hero.WeaponTier),
            Facts = WeaponStats(weapon, hero.WeaponTier),
            Starter = hero.WeaponId == null,
            Transferable = bar.CanEdit && hero.WeaponId != null,
            Selected = _selectedWarbandGearHeroId == hero.InstanceId &&
                       _selectedWarbandGearKind == (int)ItemKind.Weapon,
            ValidTarget = bar.CanEdit &&
                ((bar.ArmedInventoryItemInstanceId > 0 &&
                  bar.ArmedInventoryKind == (int)ItemKind.Weapon) ||
                 (_selectedWarbandGearHeroId > 0 &&
                  _selectedWarbandGearHeroId != hero.InstanceId &&
                  _selectedWarbandGearKind == (int)ItemKind.Weapon)),
        };

        if (string.IsNullOrEmpty(trinketId))
        {
            model.Trinket = new WarbandEquipmentModel
            {
                Kind = (int)ItemKind.Trinket,
                Icon = "◇",
                Name = "Empty trinket socket",
                Empty = true,
                ValidTarget = bar.CanEdit &&
                    ((bar.ArmedInventoryItemInstanceId > 0 &&
                      bar.ArmedInventoryKind == (int)ItemKind.Trinket) ||
                     (_selectedWarbandGearHeroId > 0 &&
                      _selectedWarbandGearHeroId != hero.InstanceId &&
                      _selectedWarbandGearKind == (int)ItemKind.Trinket)),
            };
        }
        else
        {
            TrinketDef trinket = _content.Trinket(trinketId);
            var trinketPresentation = _presentation.Content(trinketId);
            model.Trinket = new WarbandEquipmentModel
            {
                Kind = (int)ItemKind.Trinket,
                ItemInstanceId = hero.TrinketInstanceId,
                Icon = trinketPresentation.icon,
                Name = trinket.Name,
                Rule = MechanicalRulePresenter.Trinket(trinket).Full,
                Facts = TrinketCard(trinket, "").Stats,
                Transferable = bar.CanEdit,
                Selected = _selectedWarbandGearHeroId == hero.InstanceId &&
                           _selectedWarbandGearKind == (int)ItemKind.Trinket,
                ValidTarget = bar.CanEdit &&
                    ((bar.ArmedInventoryItemInstanceId > 0 &&
                      bar.ArmedInventoryKind == (int)ItemKind.Trinket) ||
                     (_selectedWarbandGearHeroId > 0 &&
                      _selectedWarbandGearHeroId != hero.InstanceId &&
                      _selectedWarbandGearKind == (int)ItemKind.Trinket)),
            };
        }

        model.Specs.AddRange(BuildSpecBadges(hero.ChassisId, hero.SpecNodeIds));
        return model;
    }

    private List<WarbandSpecBadgeModel> BuildSpecBadges(
        string chassisId, IReadOnlyList<string> nodeIds)
    {
        var result = new List<WarbandSpecBadgeModel>();
        if (nodeIds == null) return result;
        ChassisDef chassis = _content.Chassis(chassisId);
        var selected = new List<SpecNode>();
        UnitDef before = Loadout.Compose(chassis).Def;
        for (int i = 0; i < nodeIds.Count; i++)
        {
            string nodeId = nodeIds[i];
            SpecNode node = _content.Node(nodeId);
            selected.Add(node);
            UnitDef after = Loadout.Compose(chassis, nodes: selected).Def;
            SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
                chassisId, nodeId, before, after);
            result.Add(new WarbandSpecBadgeModel
            {
                Rank = rule.Rank.ToString(),
                Icon = SpecGlyph(rule.Kind),
                Name = rule.Name,
                Summary = rule.Choice,
                Rule = rule.Full,
                Accent = SpecAccent(rule.Kind),
                Context = SpecContext(node),
            });
            before = after;
        }
        return result;
    }

    private static SpecRuleContext SpecContext(SpecNode node)
    {
        if (node.SignatureOverride != null || node.SignaturePatch != null)
            return SpecRuleContext.Signature;
        if (node.CleaveBonusPct != 0 || node.StatRules.Count > 0 ||
            node.Triggers.Count > 0 &&
            node.Triggers.All(trigger =>
                trigger.On == EventKind.Attack ||
                trigger.On == EventKind.DamageDealt))
            return SpecRuleContext.BasicAttack;
        return SpecRuleContext.Passive;
    }

    private static string SpecGlyph(LexKind kind) =>
        kind switch
        {
            LexKind.Tempo => "ϟ",
            LexKind.Control => "⬢",
            LexKind.Power => "◆",
            LexKind.Precision => "◈",
            LexKind.Affliction => "♨",
            LexKind.Ward => "⬡",
            LexKind.Mending => "✦",
            LexKind.Evasion => "◌",
            LexKind.Reaction => "↶",
            LexKind.Mark => "◎",
            _ => "⚑",
        };

    private static string SpecAccent(LexKind kind) =>
        kind switch
        {
            LexKind.Tempo => "tempo",
            LexKind.Control => "control",
            LexKind.Power => "power",
            LexKind.Precision => "precision",
            LexKind.Affliction => "affliction",
            LexKind.Ward => "ward",
            LexKind.Mending => "mending",
            LexKind.Evasion => "evasion",
            LexKind.Reaction => "reaction",
            LexKind.Mark => "precision",
            _ => "utility",
        };

    private void BuildResultGate()
    {
        var result = _model.Result;
        result.Open = _resultGateOpen && _lastFightOutcome != null && _lastBattle != null;
        result.Stats = new List<ResultStatModel>();
        result.Deaths = new List<string>();
        result.Recap = null;
        // One place, every frame: while the blocking gate is up the board's own end-of-fight
        // banner/readout/clock stand down, so the two post-fight surfaces never overlap.
        if (_player != null) _player.EndReadoutSuppressed = result.Open;
        if (!result.Open) return;

        var outcome = _lastFightOutcome;
        var summary = FightSummary.Build(_lastBattle);
        result.Recap = CombatRecap.Build(summary, team: 0);
        bool bankedChoice = _run != null &&
                            _run.State.Phase == RunPhase.VictoryChoice;
        bool preservedEndless = _run != null && _run.State.EndlessDefeat;
        result.Victory = outcome.Won || preservedEndless;
        result.Eyebrow = bankedChoice ? "VICTORY BANKED" :
                          preservedEndless ? "BEYOND THE HOUR · RUN CONCLUSION" :
                          _run != null && _run.State.Over
                              ? "RUN CONCLUSION"
                              : _run != null && _run.State.InEndless
                                  ? $"BEYOND THE HOUR · CYCLE {_run.State.EndlessCycles + 1}"
                                  : $"ACT {_run.State.Act} · FIGHT RESOLVED";
        result.Heading = bankedChoice ? "THE HOUR HELD" :
                         preservedEndless ? "THE HOUR FINALLY BROKE" :
                         outcome.Won ? "VICTORY" : "THE WARBAND BREAKS";
        result.Summary = bankedChoice
            ? "The authored run is won. Read the final Crown receipt, then choose your horizon."
            : preservedEndless
                ? "This warband's deeper Hour ends here. The authored victory remains preserved."
                : outcome.Won
                    ? "The battlefield is frozen. Read the receipt, replay the same fight, or continue."
                    : "The run ends here. The replay remains available before you view the final record.";
        result.CanWatchAgain = _player != null;

        result.Stats.Add(new ResultStatModel
        {
            Label = "SAND EARNED",
            Value = outcome.SandEarned > 0 ? $"+{outcome.SandEarned}" : "0",
            Tone = "sand",
        });
        result.Stats.Add(new ResultStatModel
        {
            Label = "ENEMIES FELLED",
            Value = $"{outcome.EnemiesKilled} / {outcome.EnemyCount}",
            Tone = outcome.EnemiesKilled == outcome.EnemyCount ? "good" : "",
        });

        // TOP DAMAGE (one name, one number) used to live here. The contribution chart in the
        // recap says the same thing about every hero instead of the best one, so the stat row
        // keeps only what the chart does NOT cover.

        // The three "X fell to Y · Cause · 12.4s" labels used to live here. The recap's timeline
        // carries the same deaths as marks on the fight's clock plus a one-line "Lost:" caption,
        // and the ~90px those labels cost is height this panel provably does not have — stacking
        // them under the charts is what pushed the gate past its max-height and squashed it.

        if (bankedChoice)
        {
            result.ContinueLabel = "CHOOSE YOUR HORIZON  ›";
            result.Recommendation =
                "Victory is banked. Retire now or carry this warband Beyond the Hour.";
        }
        else if (_run.State.Over)
        {
            result.ContinueLabel = preservedEndless
                ? "VIEW PRESERVED VICTORY  ›"
                : "VIEW RUN RESULT  ›";
            result.Recommendation = preservedEndless
                ? "The authored victory stands. Review the warband's Beyond the Hour score."
                : _run.State.Victory
                    ? "The last boss has fallen. Review the completed run."
                    : "No management actions remain. Review the final warband.";
        }
        else if (_run.State.PendingSpec != null)
        {
            result.ContinueLabel = "CHOOSE SPECIALIZATION  ›";
            result.Recommendation = "Next: Warband · a rank choice blocks every other transaction.";
        }
        else if (_run.State.Phase == RunPhase.Reward)
        {
            result.ContinueLabel = "CLAIM BOSS REWARD  ›";
            result.Recommendation = "Next: Hourstone · bind one Inscription before the next act.";
        }
        else
        {
            HallStation next = _pendingHubPlan?.RecommendedStation ?? HallStation.Market;
            result.ContinueLabel = next == HallStation.Armory ? "VISIT ARMORY  ›" :
                                   next == HallStation.Hourstone ? "VISIT HOURSTONE  ›" :
                                   next == HallStation.Warband ? "VISIT WARBAND  ›" :
                                   "VISIT MARKET  ›";
            result.Recommendation = $"Next: {StationDisplayName(next)} · review what changed before the next wager.";
        }
    }

    private static string StationDisplayName(HallStation station) =>
        station == HallStation.Warband ? "Warband" :
        station == HallStation.Armory ? "Armory" :
        station == HallStation.Hourstone ? "Hourstone" :
        station == HallStation.Breach ? "Breach" : "Market";

    private static string StationTarget(HallStation station) =>
        "station-" + station.ToString().ToLowerInvariant();

    private static string AnchorTarget(HallStation station) =>
        "anchor-" + station.ToString().ToLowerInvariant();

    private string TransactionTarget(PurchaseResult purchase)
    {
        if (purchase.Outcome == PurchaseOutcome.Recruit ||
            purchase.Outcome == PurchaseOutcome.RankUp)
        {
            int fieldIndex = _run.State.Field.FindIndex(hero =>
                hero.ChassisId == purchase.ContentId);
            if (fieldIndex >= 0) return $"hero:field:{fieldIndex}";

            int reserveIndex = _run.State.Bench.FindIndex(hero =>
                hero.ChassisId == purchase.ContentId);
            if (reserveIndex >= 0) return $"hero:bench:{reserveIndex}";
            return "warband-shelf";
        }

        return purchase.Outcome switch
        {
            PurchaseOutcome.Weapon => "shelf-armory",
            PurchaseOutcome.Trinket => "shelf-armory",
            PurchaseOutcome.Inscription => StationTarget(HallStation.Hourstone),
            PurchaseOutcome.Capacity => "warband-shelf",
            _ => StationTarget(HallStation.Market),
        };
    }

    private void BuildMenu()
    {
        _model.Menu.Title = "WARBAND";
        _model.Menu.Tagline = "Bind champions from incompatible eras to one shared Hour, and see how far it carries you.";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _model.Menu.SeedLabel = $"SEED {_seed % 100000}";
#else
        _model.Menu.SeedLabel = "";
#endif
        // CONTINUE now means "a run exists", in memory OR on disk. Before item 7 it could only ever
        // mean the first, so quitting the app silently destroyed the run.
        // BuildMenu runs on every Rebuild, so only touch the disk when there is no live run to
        // answer the question — a live run makes the file check redundant anyway.
        _model.Menu.CanContinue = _run != null
            ? !_run.State.Over
            : RunSaveFile.Exists();
        _model.Menu.VersionLine =
            $"First playable · {_cfg.Acts} acts × {_cfg.NodesPerAct + 1} beats · one loss ends the run";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The content fingerprint, visible only in dev builds. Without it, "my save refused to
        // load" is unfalsifiable; with it you can compare the message's stamp against the build's.
        _model.Menu.SeedLabel += $"   ·   CONTENT {_content.ContentVersion}";
#endif
    }

    private void BuildPlanning()
    {
        var p = _model.Planning;
        var s = _run.State;
        p.Title = "WORKBENCH";
        p.MusterMode = false;
        p.Act = s.Phase == RunPhase.VictoryChoice
            ? "VICTORY BANKED"
            : s.InEndless
                ? $"BEYOND THE HOUR · CYCLE {s.EndlessCycles + 1}"
                : $"ACT {s.Act} / {_cfg.Acts}";
        p.Beat = s.Phase == RunPhase.VictoryChoice
            ? "THE WANING CROWN HAS FALLEN"
            : s.Phase == RunPhase.Reward
                ? "ACT BOSS CLEARED"
                : s.InEndless
                    ? (_run.AtBoss
                        ? "THE WANING CROWN"
                        : $"BEAT {s.EndlessBeat + 1} / {_cfg.EndlessFightsPerCycle + 1}")
                    : $"BEAT {s.NodeIndex + 1} / {_cfg.NodesPerAct + 1}";
        p.Sand = s.Sand.ToString();
        p.Capacity = $"{s.Field.Count} / {s.FieldSlots}";
        p.ActiveTab = _planningTab;
        p.HallOverview = _hallOverview;
        p.ActiveStation = TabStation(_planningTab);
        p.ReducedMotion = _reducedMotion;
        p.ForcePhoneLayout = _debugPhoneLayout;
        p.RerollLabel = "REROLL";
        p.RerollCost = _cfg.RerollCost;
        p.CurrencyBalance = s.Sand;
        p.CanReroll = s.Phase == RunPhase.Planning &&
                      s.PendingSpec == null &&
                      s.Sand >= _cfg.RerollCost;
        p.CommitLabel = _run.AtBoss ? "PREPARE FOR THE BOSS" : "CHOOSE NEXT WAGER";
        p.CanCommit = s.Phase == RunPhase.Planning &&
                      _run.CurrentNodeKind != NodeKind.Event &&
                      s.Field.Count > 0;
        p.InspectorOpen = _inspectorOpen;
        p.ShowRisk = false;
        p.Track = BuildPlanningTrack();
        p.Risks = new List<TierChoiceModel>();
        p.Enemies = new List<CardModel>();
        p.Field = new List<CardModel>();
        p.Bench = new List<CardModel>();
        p.Market = new List<CardModel>();
        p.MarketOffers = new List<MarketOfferCardModel>();
        p.Armory = new List<CardModel>();
        p.Inscriptions = new List<CardModel>();
        p.Interlude = new List<InterludeChoiceModel>();

        for (int i = 0; i < s.Field.Count; i++)
            p.Field.Add(HeroPlanningCard(s.Field[i], i, false));
        for (int i = 0; i < s.Bench.Count; i++)
            p.Bench.Add(HeroPlanningCard(s.Bench[i], i, true));
        for (int i = 0; i < s.ShopOffers.Count; i++)
        {
            CardModel detail = MarketCard(s.ShopOffers[i], i);
            p.Market.Add(detail);
            p.MarketOffers.Add(MarketOfferCard(s.ShopOffers[i], detail));
        }
        if (_run.SlotOfferOpen)
        {
            CardModel detail = SlotCard();
            p.Market.Add(detail);
            p.MarketOffers.Add(MarketCapacityCard(detail));
        }
        for (int i = 0; i < s.Inventory.Count; i++)
            p.Armory.Add(ItemCard(s.Inventory[i], i));
        for (int i = 0; i < s.Inscriptions.Count; i++)
            p.Inscriptions.Add(InscriptionCard(s.Inscriptions[i], i, false));

        p.SlotOfferOpen = _run.SlotOfferOpen;
        p.SlotOfferText = p.SlotOfferOpen
            ? $"A field place is unlocked: capacity {s.FieldSlots + 1} is now available."
            : "";
        p.SlotAffordable = p.SlotOfferOpen && s.Sand >= _run.SlotOfferCost;

        if (s.Phase == RunPhase.VictoryChoice)
        {
            BuildEndlessChoiceBeat(p);
        }
        else if (s.Phase == RunPhase.Reward)
        {
            p.BeatKind = PlanningBeat.BossReward;
            p.Heading = "THE HOUR ANSWERS";
            p.Brief = $"The Hourstone answers. Bind one Inscription before Act {s.Act + 1}.";
            p.Rule = "Boss rewards are visible, exclusive, and permanent for this run.";
            p.CommitLabel = "";
            p.CanCommit = false;
            int i = 0;
            foreach (var id in _run.PreviewBossRewards())
            {
                p.Interlude.Add(new InterludeChoiceModel
                {
                    Path = -1,
                    Option = i,
                    Card = InscriptionCard(id, i++, true),
                });
            }
        }
        else
        {
            var kind = _run.CurrentNodeKind;
            if (kind == NodeKind.Event)
            {
                if (s.Revision.UpgradeIds.Count < s.Act)
                    BuildRevisionUpgradeBeat(p);
                else
                    BuildInterludeBeat(p);
            }
            else
            {
                BuildFightBeat(p, kind == NodeKind.Boss);
                BuildManagementHeading(p);
            }
        }

        p.SpecChoice = BuildSpecChoice();
        if (p.SpecChoice.Pending) _recommendedStation = HallStation.Warband;
        else if (s.Phase == RunPhase.Reward) _recommendedStation = HallStation.Hourstone;
        p.RecommendedStation = _recommendedStation;
        p.Stations = BuildHallStations(p);
        EnsurePlanningSelection(p);
        p.Inspector = BuildInspector(p);
        p.PartyShelf = BuildPartyShelf(p);
    }

    private void BuildEndlessChoiceBeat(PlanningModel model)
    {
        model.BeatKind = PlanningBeat.EndlessChoice;
        model.Heading = "THE HOUR HELD";
        model.Brief =
            "The authored run is won. Leave with the victory, or carry this exact warband " +
            "into escalating cycles until a deeper Hour finally breaks it.";
        model.Rule = "Victory is already preserved.";
        model.CommitLabel = "";
        model.CanCommit = false;
        model.CanReroll = false;
        model.Interlude.Add(new InterludeChoiceModel
        {
            Path = -1,
            Option = 0,
            ActionLabel = "RETIRE WITH VICTORY",
            Facts = new List<string> { "3 ACTS CLEARED", "VICTORY PRESERVED" },
            Card = new CardModel
            {
                Eyebrow = "SEAL THE HOUR",
                Title = "Retire with victory",
                AbilitySummary =
                    "End the expedition here. The completed run and final warband become the record.",
                Accent = "sand",
            },
        });
        model.Interlude.Add(new InterludeChoiceModel
        {
            Path = -1,
            Option = 1,
            ActionLabel = "CONTINUE BEYOND THE HOUR",
            Facts = new List<string>
            {
                $"CYCLE {_run.State.EndlessCycles + 1}",
                "ACT 3 POOL",
                "CROWN +25%",
            },
            Card = new CardModel
            {
                Eyebrow = "BEYOND THE HOUR",
                Title = "Continue with this warband",
                AbilitySummary =
                    "Enter three escalating fights and face the Waning Crown again. " +
                    "Defeat cannot erase the victory already earned.",
                Accent = "tempo",
            },
        });
    }

    private PartyShelfModel BuildPartyShelf(PlanningModel planning)
    {
        var state = _run.State;
        var shelf = new PartyShelfModel
        {
            FieldCapacity = state.FieldSlots,
            FieldCount = state.Field.Count,
            MaxFieldCapacity = _cfg.MaxFieldSlots,
            ReserveCount = state.Bench.Count,
            ReserveCapacity = _cfg.BenchSlots,
            Expanded = _loadoutOpen,
            FocusedHeroKey = TryHeroAddress(_selectedCardKey, out _, out _)
                ? _selectedCardKey
                : "",
        };

        for (int i = 0; i < _cfg.MaxFieldSlots; i++)
        {
            if (i < state.Field.Count)
                shelf.Field.Add(PartySlot(state.Field[i], i, reserve: false));
            else
                shelf.Field.Add(new PartySlotModel
                {
                    Key = $"shelf:field:{i}",
                    Index = i,
                    State = i < state.FieldSlots ? PartySlotState.Empty : PartySlotState.Locked,
                    Focused = false,
                });
        }

        for (int i = 0; i < _cfg.BenchSlots; i++)
        {
            if (i < state.Bench.Count)
                shelf.Reserve.Add(PartySlot(state.Bench[i], i, reserve: true));
            else
                shelf.Reserve.Add(new PartySlotModel
                {
                    Key = $"shelf:reserve:{i}",
                    Index = i,
                    Reserve = true,
                    State = PartySlotState.Empty,
                });
        }

        if (planning.Inspector.EquipmentPreview == null)
            foreach (PartySlotModel slot in shelf.Field)
                slot.Previewed = false;

        for (int i = 0; i < state.Inventory.Count; i++)
        {
            ItemRef item = state.Inventory[i];
            bool weapon = item.Kind == ItemKind.Weapon;
            shelf.StoredItems.Add(new StoredItemSummaryModel
            {
                Key = $"item:{i}",
                Name = weapon ? _content.Weapon(item.Id).Name : _content.Trinket(item.Id).Name,
                Kind = weapon ? item.Tier + " WEAPON" : "TRINKET",
                Icon = weapon ? "⚔" : "◇",
                Accent = weapon ? "power" : "utility",
                Selected = i == _selectedItem,
            });
        }

        shelf.LoadoutInventory = planning.Armory.Select(card =>
        {
            card.Selected = TrySimpleIndex(card.Key, "item", out var index) &&
                            index == _selectedItem;
            return card;
        }).ToList();
        if (_loadoutOpen)
        {
            EnsureLoadoutHeroSelection();
            shelf.FocusedHeroKey = _selectedCardKey;
            shelf.LoadoutInspector = BuildInspector(planning);
            // The drawer-open dossier is ALWAYS compact — the body above the drawer has no
            // height for a full kit, and the task there is equipping, not studying. Rules
            // defer to hover rows; stat chips yield (the deltas carry the numbers when an
            // item is armed, and the full dossier is one drawer-close away).
            if (shelf.LoadoutInspector != null)
            {
                shelf.LoadoutInspector.Stats.Clear();
                foreach (InspectorSectionModel section in shelf.LoadoutInspector.Sections)
                    if (section.Kind == InspectorSectionKind.Rule)
                        section.Role = InspectorSectionRole.Deferred;
            }
        }
        return shelf;
    }

    private PartySlotModel PartySlot(HeroInstance hero, int index, bool reserve)
    {
        CardModel card = HeroPlanningCard(hero, index, reserve);
        string trinket = hero.TrinketIds.Count > 0
            ? _content.Trinket(hero.TrinketIds[0]).Name
            : "";
        return new PartySlotModel
        {
            Key = card.Key,
            Index = index,
            Reserve = reserve,
            State = PartySlotState.Occupied,
            Name = card.Title,
            Rank = hero.Rank.ToString(),
            Role = _presentation.Unit(hero.ChassisId).role,
            PortraitResource = card.PortraitResource,
            PortraitFallback = card.PortraitFallback,
            Accent = card.Accent,
            Weapon = card.Weapon,
            Trinket = trinket,
            Focused = card.Key == _selectedCardKey,
            Previewed = card.Key == _comparisonTargetHeroKey,
        };
    }

    private void EnsureLoadoutHeroSelection()
    {
        if (TryHeroAddress(_selectedCardKey, out var bench, out var index))
        {
            int count = bench ? _run.State.Bench.Count : _run.State.Field.Count;
            if (index >= 0 && index < count) return;
        }
        _selectedCardKey = _run.State.Field.Count > 0
            ? "hero:field:0"
            : _run.State.Bench.Count > 0 ? "hero:bench:0" : "";
    }

    private List<HallStationModel> BuildHallStations(PlanningModel model)
    {
        var s = _run.State;
        int liveOffers = 0;
        foreach (var offer in s.ShopOffers)
            if (offer != null) liveOffers++;

        string breachStatus;
        string breachAction;
        if (s.Phase == RunPhase.VictoryChoice) breachStatus = "Choose your horizon first";
        else if (s.Phase == RunPhase.Reward) breachStatus = "Claim the boss reward first";
        else if (s.PendingSpec != null) breachStatus = "Choose a specialization first";
        else if (_run.CurrentNodeKind == NodeKind.Boss) breachStatus = "The act boss waits";
        else if (_run.CurrentNodeKind == NodeKind.Event) breachStatus = "An Interlude lies ahead";
        else breachStatus = "Set the next wager";
        if (s.Phase == RunPhase.VictoryChoice) breachAction = "CHOOSE YOUR HORIZON";
        else if (s.Phase == RunPhase.Reward) breachAction = "CLAIM REWARD";
        else if (s.PendingSpec != null) breachAction = "CHOOSE SPECIALIZATION";
        else if (_run.CurrentNodeKind == NodeKind.Boss) breachAction = "PREPARE FOR BOSS";
        else if (_run.CurrentNodeKind == NodeKind.Event) breachAction = "ENTER INTERLUDE";
        else breachAction = "SET WAGER";

        return new List<HallStationModel>
        {
            new HallStationModel
            {
                Station = HallStation.Breach,
                Eyebrow = "NEXT BEAT",
                Name = "BREACH",
                Status = breachStatus,
                Action = breachAction,
                Sigil = "⚔",
                Attention = _recommendedStation == HallStation.Breach,
                Enabled = s.Phase == RunPhase.Planning && s.PendingSpec == null,
            },
            new HallStationModel
            {
                Station = HallStation.Market,
                Eyebrow = "ECONOMY",
                Name = "MARKET",
                Status = $"{liveOffers} live offer{(liveOffers == 1 ? "" : "s")} · {s.Sand} Sand",
                Sigil = "◇",
                Attention = _hubAttention.Has(HallStation.Market),
            },
            new HallStationModel
            {
                Station = HallStation.Warband,
                Eyebrow = "ROSTER",
                Name = "WARBAND",
                Status = $"{s.Field.Count} fielded · {s.Bench.Count} reserve",
                Sigil = "♜",
                Attention = _hubAttention.Has(HallStation.Warband) || s.PendingSpec != null,
            },
            new HallStationModel
            {
                Station = HallStation.Armory,
                Eyebrow = "LOADOUT",
                Name = "ARMORY",
                Status = $"{s.Inventory.Count} stored item{(s.Inventory.Count == 1 ? "" : "s")}",
                Sigil = "⚒",
                Attention = _hubAttention.Has(HallStation.Armory),
            },
            new HallStationModel
            {
                Station = HallStation.Hourstone,
                Eyebrow = "RUN-WIDE LAWS",
                Name = "HOURSTONE",
                Status = $"{s.Inscriptions.Count} Inscription{(s.Inscriptions.Count == 1 ? "" : "s")} bound",
                Sigil = "⌛",
                Attention = _hubAttention.Has(HallStation.Hourstone) ||
                            s.Phase == RunPhase.Reward,
            },
        };
    }

    private List<PlanningTrackNodeModel> BuildPlanningTrack()
    {
        var s = _run.State;
        var result = new List<PlanningTrackNodeModel>();
        int last = s.InEndless ? _cfg.EndlessFightsPerCycle : _cfg.NodesPerAct;
        for (int i = 0; i <= last; i++)
        {
            string kind = i == last
                ? "Boss"
                : s.InEndless
                    ? "Fight"
                    : s.ActMaps[s.Act - 1][i] == NodeKind.Event ? "Interlude" : "Fight";
            bool choiceCleared = s.Phase == RunPhase.VictoryChoice &&
                                 i == s.NodeIndex;
            result.Add(new PlanningTrackNodeModel
            {
                Label = kind == "Boss" ? "BOSS" : (i + 1).ToString(),
                Kind = kind,
                State = i < s.NodeIndex || choiceCleared
                    ? "past"
                    : i == s.NodeIndex ? "current" : "future",
            });
        }
        return result;
    }

    private void BuildManagementHeading(PlanningModel model)
    {
        switch (model.ActiveTab)
        {
            case PlanningTab.Market:
                model.Heading = "THE MARKET";
                model.Brief = "Select any visible offer for its full rules, exact price, and purchase action.";
                break;
            case PlanningTab.Armory:
                model.Heading = "THE ARMORY";
                model.Brief = "Review stored equipment, compare its exact attack profile, and assign it to a champion.";
                break;
            case PlanningTab.Hourstone:
                model.Heading = "THE HOURSTONE";
                model.Brief = "Every bound Inscription changes the whole warband for the rest of this run.";
                break;
            default:
                model.Heading = "THE WARBAND";
                model.Brief = "Read the warband as cards. Open any champion for exact attacks, Signature, Passive, and management actions.";
                break;
        }
        model.Rule = "";
    }

    private void BuildWager()
    {
        var w = _model.Wager;
        var s = _run.State;
        w.Act = $"ACT {s.Act} / {_cfg.Acts}";
        w.Beat = $"BEAT {s.NodeIndex + 1} / {_cfg.NodesPerAct + 1}";
        w.Sand = s.Sand.ToString();
        w.Heading = "SET THE WAGER";
        w.Brief = "Commit to the pressure before seeing the opposing roster. The stakes are public; the exact formation is not.";
        w.Disclosure = "Enemy identities and placement are revealed after the wager is locked, before you deploy your warband.";
        w.Track = BuildPlanningTrack();
        w.Risks = new[]
        {
            (FightTier.Stable, "STABLE", "MEASURED PRESSURE\nThe safest enemy strength."),
            (FightTier.Fraying, "FRAYING", "SEVERE PRESSURE\nA sharper opposing warband."),
            (FightTier.Collapsing, "COLLAPSING", "EXTREME PRESSURE\nThe act's hardest wager."),
        }.Select(entry => new TierChoiceModel
        {
            Index = (int)entry.Item1,
            Name = entry.Item2,
            Risk = entry.Item3,
            CurrencyReward = _cfg.FightReward(s.Act, entry.Item1),
            Reward = $"+{_cfg.FightReward(s.Act, entry.Item1)} ON VICTORY",
            Selected = _tierChosen && entry.Item1 == _tier,
        }).ToList();
        w.CanContinue = _tierChosen;
        w.ContinueLabel = _tierChosen
            ? $"LOCK {_tier.ToString().ToUpperInvariant()} WAGER  ›"
            : "CHOOSE A WAGER";
    }

    private void BuildFightBeat(PlanningModel p, bool boss)
    {
        var s = _run.State;
        // "Know the rules, not the result" (pve-encounters.md) reaches the screen HERE or nowhere.
        // This used to hardcode "THE LAST OATH" for every boss and disclose nothing at all for the
        // four authored node encounters; the rule now comes off RunController.PreviewBrief, which
        // is derived from the same private salt as the spawn, so the brief cannot describe a fight
        // the player will not get.
        var brief = BriefForCurrentNode();
        p.BeatKind = boss ? PlanningBeat.Boss : PlanningBeat.Fight;
        p.Heading = brief == null
            ? (boss ? "THE ACT BOSS" : "A CONTESTED HOUR")
            : brief.Name.ToUpperInvariant();
        string stakes = boss
            ? "The act closes here. A loss ends the run."
            : "A loss ends the run and pays no reward.";
        p.Brief = brief == null || string.IsNullOrEmpty(brief.Pressure)
            ? stakes
            : $"{brief.Pressure}  {stakes}";
        // The rule box is the disclosure contract: the encounter's own rule, verbatim, every fight.
        p.Rule = brief == null || string.IsNullOrEmpty(brief.RuleText)
            ? "Formation and combat rules are final before you commit."
            : $"{brief.RuleName} — {brief.RuleText}";
        p.ShowRisk = !boss;
        if (!boss)
        {
            p.Risks = new[]
            {
                (FightTier.Stable, "STABLE", "More forgiving"),
                (FightTier.Fraying, "FRAYING", "Sharper opposition"),
                (FightTier.Collapsing, "COLLAPSING", "Maximum pressure"),
            }.Select(entry => new TierChoiceModel
            {
                Index = (int)entry.Item1,
                Name = entry.Item2,
                Risk = entry.Item3,
                Reward = $"+{_cfg.FightReward(s.Act, entry.Item1)} SAND",
                Selected = entry.Item1 == _tier,
            }).ToList();
        }

        if (brief != null && brief.Units.Count > 0)
        {
            for (int i = 0; i < brief.Units.Count; i++)
                p.Enemies.Add(EnemyCard(brief.Units[i], i));
        }
        else
        {
            // The brief is only unavailable outside Planning; keep the old shape rather than
            // showing an empty enemy board.
            var enemies = EnemiesForCurrentNode();
            for (int i = 0; i < enemies.Count; i++)
                p.Enemies.Add(UnitCardFromDef(
                    enemies[i].Def,
                    $"enemy:{i}",
                    $"ENEMY · ROW {enemies[i].Pos.Row + 1}",
                    "",
                    false));
        }
    }

    /// <summary>The disclosure for the node the player is standing on, or null when there is none
    /// to give (event nodes, or any phase outside Planning).</summary>
    private EncounterBrief BriefForCurrentNode()
    {
        try { return _run.PreviewBrief(_tier); }
        catch { return null; }
    }

    /// <summary>
    /// An enemy card built from the BRIEF, not from a UnitDef.
    ///
    /// This is the fix for a real shipped lie: <see cref="UnitCardFromDef"/> titles a card from
    /// `ContentLexicon.Chassis(def.ChassisId)` and fills its ability/passive copy from the hero
    /// presentation for that id — but every authored monster sets ChassisId purely as a RENDER KEY
    /// for the silhouette it borrows. So an Hourling previewed as "Shade" with the Shade's
    /// signature text, an Ashen Colossus as "Bulwark", an Hour-Scribe as "Pyromancer" reading out
    /// Inferno. The player was being shown a different unit than the one that spawns.
    ///
    /// Monsters therefore get their authored name, their encounter role, their real numbers, and
    /// their one-line behavior note. No portrait: the chassis portrait is a named champion's face,
    /// and a hero's face on a monster is the same lie in a different channel. Bespoke enemy art is
    /// roadmap item 2③; initials are honest until it exists.
    /// </summary>
    private CardModel EnemyCard(EncounterUnitBrief u, int index)
    {
        string key = $"enemy:{index}";
        float seconds = u.AttackIntervalTicks / 10f;
        string attack = u.Attack <= 0
            ? "No basic attack."
            : $"{u.Attack} damage every {seconds:0.0}s at reach {u.Range}.";
        var card = new CardModel
        {
            Key = key,
            ContentId = "",                    // never a hero id: nothing may resolve hero copy here
            Eyebrow = string.IsNullOrEmpty(u.Role)
                ? $"ENEMY · ROW {u.Row + 1}"
                : $"{u.Role} · ROW {u.Row + 1}",
            Title = u.Name,
            InspectorSubtitle = attack,
            PortraitFallback = Initials(u.Name),
            RoleIcon = "✖",
            Accent = string.IsNullOrEmpty(u.Accent) ? "utility" : u.Accent,
            Weapon = u.WeaponName,
            WeaponSummary = attack,
            AbilityIcon = "✦",
            AbilityTrigger = "BEHAVIOR",
            AbilityName = "HOW IT FIGHTS",
            AbilitySummary = u.Behavior,
            InspectorAbilitySummary = u.Behavior,
            Stats = EnemyStatChips(u),
            Selected = key == _selectedCardKey,
        };
        card.UnitSheet = EnemyUnitSheet(
            card.Stats, u.WeaponName, u.Behavior);
        return card;
    }

    private static UnitSheetModel EnemyUnitSheet(
        IReadOnlyList<StatChipModel> facts, string weaponName, string behavior)
    {
        var sheet = new UnitSheetModel
        {
            Enemy = true,
            CoreFacts = facts == null
                ? new List<StatChipModel>()
                : facts.Where(fact => fact.Id == PresentationFactId.Hp ||
                                      fact.Id == PresentationFactId.Protection ||
                                      fact.Id == PresentationFactId.ManaThreshold).ToList(),
            WeaponName = string.IsNullOrWhiteSpace(weaponName)
                ? "NO BASIC ATTACK"
                : weaponName,
            WeaponFacts = facts == null
                ? new List<StatChipModel>()
                : facts.Where(IsWeaponFact).ToList(),
            PassivesLabel = "BEHAVIOR",
        };
        if (!string.IsNullOrWhiteSpace(behavior))
            sheet.Passives.Add(UnitRule(
                "BEHAVIOR", "◆", "HOW IT FIGHTS", behavior));
        return sheet;
    }

    /// <summary>Same fact vocabulary the champion cards use, minus the ones a monster has no
    /// honest answer for (mana per swing, signature threshold — an authored enemy has no weapon
    /// cadence axis and its clock is described in the behavior line instead).</summary>
    private static List<StatChipModel> EnemyStatChips(EncounterUnitBrief u)
    {
        var chips = new List<StatChipModel>
        {
            new StatChipModel("HP", u.MaxHp.ToString(), "",
                PresentationFactId.Hp, "Maximum combat HP."),
        };
        if (u.Attack > 0)
        {
            chips.Add(new StatChipModel("POWER", u.Attack.ToString(), "",
                PresentationFactId.BasicPower, "Damage per basic swing."));
            chips.Add(new StatChipModel("REACH", u.Range.ToString(), "",
                PresentationFactId.Reach, "Maximum basic attack reach in hexes."));
            chips.Add(new StatChipModel("CADENCE", $"{u.AttackIntervalTicks / 10f:0.0}s", "",
                PresentationFactId.Cadence, "Time between basic attacks."));
        }
        return chips;
    }

    private void BuildInterludeBeat(PlanningModel p)
    {
        p.BeatKind = PlanningBeat.Interlude;
        p.Heading = "INTERLUDE";
        p.Brief = "No battle here. Choose certainty, equipment, or a run-wide rule.";
        p.Rule = "Every offered reward is shown before you choose. The choice also unlocks the next field capacity.";
        p.CommitLabel = "";
        p.CanCommit = false;

        var preview = _run.PreviewInterlude();
        p.Interlude.Add(new InterludeChoiceModel
        {
            Path = (int)InterludePath.Treasury,
            Option = 0,
            Card = new CardModel
            {
                Key = "interlude:treasury",
                Eyebrow = "TREASURY",
                Title = $"+{preview.TreasurySand} SAND",
                PortraitFallback = "◇",
                RoleIcon = "◇",
                Accent = "utility",
                AbilityName = "CERTAIN RESERVE",
                AbilityIcon = "◆",
                AbilitySummary = "Take a fixed reserve with no equipment choice.",
            },
        });
        for (int i = 0; i < preview.Armory.Count; i++)
        {
            var card = RewardCard(preview.Armory[i], $"interlude:armory:{i}");
            card.Eyebrow = "ARMORY";
            p.Interlude.Add(new InterludeChoiceModel
            {
                Path = (int)InterludePath.Armory,
                Option = i,
                Card = card,
            });
        }
        for (int i = 0; i < preview.Hourstone.Count; i++)
        {
            var card = InscriptionCard(preview.Hourstone[i].Id, i, true);
            card.Key = $"interlude:hourstone:{i}";
            card.Eyebrow = "HOURSTONE";
            p.Interlude.Add(new InterludeChoiceModel
            {
                Path = (int)InterludePath.Hourstone,
                Option = i,
                Card = card,
            });
        }
    }

    private void BuildRevisionUpgradeBeat(PlanningModel p)
    {
        RevisionDef revision = RevisionCatalog.Get(_run.State.Revision.RevisionId);
        int tier = _run.State.Revision.UpgradeIds.Count + 1;
        p.BeatKind = PlanningBeat.RevisionUpgrade;
        p.Heading = "THE REVISION DEEPENS";
        p.Brief =
            $"{revision.Name} has reached a new expression. Choose its Act {tier} evolution before taking this Interlude's reward.";
        p.Rule = "Revision growth is authored, run-bound, and separate from the Workbench economy.";
        p.CommitLabel = "";
        p.CanCommit = false;

        IReadOnlyList<RevisionUpgradeDef> options = _run.PreviewRevisionUpgrades();
        for (int i = 0; i < options.Count; i++)
        {
            RevisionUpgradeDef upgrade = options[i];
            p.Interlude.Add(new InterludeChoiceModel
            {
                Path = -2,
                Option = i,
                Card = new CardModel
                {
                    Key = $"revision-upgrade:{upgrade.Id}",
                    Eyebrow = $"ACT {tier} REVISION",
                    Title = upgrade.Name,
                    PortraitFallback = "⌛",
                    RoleIcon = "⌛",
                    Accent = revision.Effect == RevisionEffectKind.BorrowedFuture
                        ? "utility"
                        : "control",
                    AbilityName = revision.Name,
                    AbilityIcon = "◈",
                    AbilitySummary = upgrade.Summary,
                },
            });
        }
    }

    private SpecChoiceModel BuildSpecChoice()
    {
        var pending = _run.State.PendingSpec;
        if (pending == null) return new SpecChoiceModel();
        HeroInstance hero = HeroAt(pending);
        UnitDef current = ComposeHero(hero);
        var presentation = _presentation.Unit(hero.ChassisId);
        ChassisDef chassis = _content.Chassis(hero.ChassisId);
        var model = new SpecChoiceModel
        {
            Pending = true,
            HeroName = ContentLexicon.Chassis(hero.ChassisId).Name,
            RankLabel = $"RANK {pending.ForRank}",
            // The fork is the rank where the path itself is chosen (ADR 0009).
            Fork = _content.ForkRank(hero.ChassisId) == pending.ForRank,
            FromRank = ((Rank)Mathf.Max(0, (int)pending.ForRank - 1)).ToString(),
            ToRank = pending.ForRank.ToString(),
            // The flat bump landed at purchase — this line reports what was just gained.
            BumpText = $"+{chassis.RankHp} HEALTH · +{chassis.RankAttack} POWER — " +
                       "THEN BIND THE PATH",
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(ContentLexicon.Chassis(hero.ChassisId).Name),
            Accent = presentation.accent,
            SignatureIcon = presentation.abilityIcon,
            WeaponFilled = true,
            TrinketFilled = hero.TrinketIds.Count > 0,
        };
        // The B/A/S ladder with the pending rank flipped to the awaiting state.
        model.PathTiers = BuildPathTiers(
            hero.ChassisId, hero.PathId,
            BuildSpecBadges(hero.ChassisId, hero.SpecNodeIds));
        foreach (RankTierSlotModel tier in model.PathTiers)
            if (tier.State == RankTierSlotState.Locked &&
                string.Equals(tier.Rank, pending.ForRank.ToString(), StringComparison.Ordinal))
                tier.State = RankTierSlotState.Pending;
        foreach (string nodeId in pending.Options)
        {
            HeroInstance choice = hero.Clone();
            choice.SpecNodeIds.Add(nodeId);
            SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
                hero.ChassisId, nodeId, current, ComposeHero(choice));
            model.Options.Add(new SpecOptionModel
            {
                Name = rule.Name,
                Text = rule.Choice,
                Change = rule.Change.ToString().ToUpperInvariant(),
                Icon = SpecGlyph(rule.Kind),
                Comparisons = ChangedFacts(current, ComposeHero(choice)),
            });
        }
        return model;
    }

    private CardModel HeroPlanningCard(HeroInstance hero, int index, bool inBench)
    {
        var card = UnitCardFromDef(
            ComposeHero(hero),
            $"hero:{(inBench ? "bench" : "field")}:{index}",
            (inBench ? "RESERVE" : "FIELD") + " · " + _presentation.Unit(hero.ChassisId).role,
            $"RANK {hero.Rank}",
            true);
        card.ContentId = hero.ChassisId;
        card.Traits = BuildSpecBadges(hero.ChassisId, hero.SpecNodeIds);
        card.PathTiers = BuildPathTiers(hero.ChassisId, hero.PathId, card.Traits);
        card.Selected = card.Key == _selectedCardKey;
        return card;
    }

    /// <summary>
    /// The champion's whole ladder: chosen picks read from the badges, everything above
    /// reads as a visible promise ("AWAKENS AT RANK X", the fork rank named). Recruits pass
    /// their pre-selected picks the same way, so later-run pre-specced offers need no new UI.
    /// </summary>
    private List<RankTierSlotModel> BuildPathTiers(
        string chassisId,
        string pathId,
        IReadOnlyList<WarbandSpecBadgeModel> selected)
    {
        var tiers = new List<RankTierSlotModel>();
        foreach (SpecializationTierProjection authored in
                 PlayerRuleProjection.Tiers(chassisId, pathId))
        {
            string rank = authored.Rank.ToString();
            WarbandSpecBadgeModel chosen = selected?.FirstOrDefault(
                trait => string.Equals(trait.Rank, rank, StringComparison.Ordinal));
            int count = authored.OptionIds.Count;
            tiers.Add(new RankTierSlotModel
            {
                Rank = rank,
                State = chosen != null
                    ? RankTierSlotState.Selected
                    : RankTierSlotState.Locked,
                Icon = chosen?.Icon ?? "◇",
                Name = chosen?.Name ??
                    $"AWAKENS AT RANK {rank}{(authored.IsFork ? " · THE FORK" : "")}",
                Summary = chosen?.Summary ?? "",
                Rule = chosen?.Rule ?? (authored.NeedsPath
                    ? $"Choose the Rank {_content.ForkRank(chassisId)} path to reveal this tier."
                    : authored.IsFork
                        ? $"At Rank {rank}, choose 1 of {count} authored paths."
                        : $"At Rank {rank}, choose 1 of {count} authored specializations."),
                Accent = chosen?.Accent ?? "",
            });
        }
        return tiers;
    }

    private CardModel UnitCardFromDef(UnitDef def, string key, string eyebrow, string rank,
                                      bool owned)
    {
        if (!string.IsNullOrWhiteSpace(def.RoleId))
        {
            string behavior = Enemies.Behavior(def.Name);
            var enemy = new CardModel
            {
                Key = key,
                ContentId = "",
                Eyebrow = string.IsNullOrWhiteSpace(eyebrow)
                    ? "ENEMY · " + Enemies.RoleLabel(def.RoleId)
                    : eyebrow,
                Title = def.Name,
                InspectorSubtitle = BasicAttackSummary(def),
                PortraitFallback = EnemyRoleGlyph(def.RoleId),
                RoleIcon = EnemyRoleGlyph(def.RoleId),
                Accent = Enemies.RoleAccent(def.RoleId),
                Weapon = def.WeaponName,
                WeaponSummary = BasicAttackSummary(def),
                Stats = StatChips(def),
                Selected = key == _selectedCardKey,
            };
            enemy.UnitSheet = EnemyUnitSheet(
                enemy.Stats, def.WeaponName, behavior);
            return enemy;
        }

        var id = string.IsNullOrEmpty(def.ChassisId) ? "" : def.ChassisId;
        var presentation = _presentation.Unit(id);
        string title = string.IsNullOrEmpty(id) ? def.Name : ContentLexicon.Chassis(id).Name;
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
        var card = new CardModel
        {
            Key = key,
            ContentId = id,
            Eyebrow = eyebrow,
            Title = title,
            Subtitle = "",
            InspectorSubtitle =
                def.HealAutos ? "Basic attacks restore allies" : "Basic attacks damage enemies",
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(title),
            RoleIcon = presentation.roleIcon,
            Accent = presentation.accent,
            Rank = rank,
            Weapon = def.WeaponName,
            WeaponSummary = BasicAttackSummary(def),
            WeaponProperty = BuildWeaponProperty(def),
            AbilityIcon = presentation.abilityIcon,
            AbilityTrigger = SignatureTrigger(def),
            AbilityName = rules.SignatureName,
            AbilitySummary = rules.SignatureText,
            InspectorAbilitySummary = rules.SignatureText,
            AbilityManaCost = def.ManaMax > 0 ? def.ManaMax : -1,
            PassiveIcon = presentation.passiveIcon,
            PassiveTrigger = "PASSIVE",
            PassiveName = rules.PassiveName,
            PassiveSummary = rules.PassiveText,
            KeywordNotes = PlayerRuleProjection.Keywords(def).ToList(),
            Stats = StatChips(def),
            Selected = key == _selectedCardKey,
        };
        card.UnitSheet = UnitSheetFromDef(def, presentation, rules);
        return card;
    }

    private UnitSheetModel UnitSheetFromDef(
        UnitDef def,
        PresentationCatalog.UnitPresentation presentation,
        ChampionRuleProjection rules)
    {
        List<StatChipModel> facts = StatChips(def);
        var sheet = new UnitSheetModel
        {
            CoreFacts = facts.Where(fact => fact.Id == PresentationFactId.Hp).ToList(),
            WeaponName = def.WeaponName,
            WeaponFacts = facts.Where(IsWeaponFact).ToList(),
        };
        RuleDeltaModel property = BuildWeaponProperty(def);
        if (property != null) sheet.WeaponProperties.Add(property);
        if (def.ManaMax > 0 && !string.IsNullOrWhiteSpace(rules.SignatureText))
            sheet.Signature = UnitRule(
                "SIGNATURE", presentation.abilityIcon, rules.SignatureName,
                rules.SignatureText, def.ManaMax);
        if (!string.IsNullOrWhiteSpace(rules.PassiveText))
            sheet.Passives.Add(UnitRule(
                "PASSIVE", presentation.passiveIcon,
                rules.PassiveName, rules.PassiveText));
        return sheet;
    }

    private static bool IsWeaponFact(StatChipModel fact) =>
        fact != null &&
        (fact.Id == PresentationFactId.BasicPower ||
         fact.Id == PresentationFactId.Restoration ||
         fact.Id == PresentationFactId.Cadence ||
         fact.Id == PresentationFactId.Reach ||
         fact.Id == PresentationFactId.ManaPerSwing ||
         fact.Id == PresentationFactId.CritChance ||
         fact.Id == PresentationFactId.Cleave);

    private static InspectorSectionModel UnitRule(
        string label, string icon, string name, string summary, int manaCost = -1) =>
        new InspectorSectionModel
        {
            Kind = InspectorSectionKind.Rule,
            Role = InspectorSectionRole.Primary,
            Label = label,
            Icon = icon,
            Name = name,
            Summary = summary,
            LabelGlyph = manaCost >= 0 ? UiGlyphId.Mana : UiGlyphId.Unknown,
            LabelValue = manaCost >= 0 ? manaCost.ToString() : "",
        };

    private CardModel MarketCard(ShopOffer offer, int index)
    {
        string key = $"market:{index}";
        if (offer == null)
        {
            if (index == _equipNowOfferIndex)
            {
                int itemIndex = _run.IndexOfItem(_equipNowItemInstanceId);
                if (itemIndex >= 0)
                {
                    CardModel acquired = ItemCard(_run.State.Inventory[itemIndex], itemIndex);
                    acquired.Key = key;
                    acquired.Eyebrow = "ACQUIRED · ARMORY";
                    acquired.Subtitle = "Ready to equip now or later";
                    acquired.Selected = key == _selectedCardKey;
                    return acquired;
                }
            }
            return new CardModel
            {
                Key = key,
                Eyebrow = "MARKET",
                Title = "SOLD",
                PortraitFallback = "—",
                Sold = true,
            };
        }

        CardModel card;
        switch (offer.Kind)
        {
            case OfferKind.Hero:
                if (TryOwnedHero(offer.Id, out HeroInstance owned))
                {
                    card = RankUpCard(owned, key);
                    card.PathTiers = BuildPathTiers(
                        offer.Id, owned.PathId, card.Traits);
                }
                else
                {
                    card = UnitCardFromDef(
                        Loadout.Compose(_content.Chassis(offer.Id)).Def,
                        key,
                        "RECRUIT",
                        "",
                        false);
                    // A recruit's dossier shows the whole promise: three empty tiers, the
                    // fork named. Pre-specced later-run recruits fill these the day the run
                    // layer sells them — no new UI.
                    card.PathTiers = BuildPathTiers(offer.Id, null, null);
                }
                card.ContentId = offer.Id;
                break;
            case OfferKind.Weapon:
                card = WeaponCard(_content.Weapon(offer.Id), offer.Tier, key);
                card.Eyebrow = "WORKSHOP · WEAPON";
                break;
            case OfferKind.Trinket:
                card = TrinketCard(_content.Trinket(offer.Id), key);
                card.Eyebrow = "WORKSHOP · TRINKET";
                break;
            default:
                card = InscriptionCard(offer.Id, index, true);
                card.Key = key;
                card.Eyebrow = "WORKSHOP · INSCRIPTION";
                break;
        }
        card.ContentId = offer.Id;
        card.CurrencyCost = offer.Price;
        card.Frozen = offer.Frozen;
        // Affordability disables BUY, never inspection. A card is disabled only when its own
        // state is structurally unavailable (for example, a following rank while the fork choice
        // is still pending).
        card.Selected = key == _selectedCardKey;
        return card;
    }

    private MarketOfferCardModel MarketOfferCard(ShopOffer offer, CardModel detail)
    {
        if (offer == null)
        {
            if (!detail.Sold && detail.Key == $"market:{_equipNowOfferIndex}")
            {
                int itemIndex = _run.IndexOfItem(_equipNowItemInstanceId);
                ItemKind itemKind = itemIndex >= 0
                    ? _run.State.Inventory[itemIndex].Kind
                    : ItemKind.Trinket;
                MarketOfferKind acquiredKind = itemKind == ItemKind.Weapon
                    ? MarketOfferKind.Weapon
                    : MarketOfferKind.Trinket;
                return new MarketOfferCardModel
                {
                    Key = detail.Key,
                    ContentId = detail.ContentId,
                    Kind = acquiredKind,
                    Classification = "ACQUIRED · SENT TO ARMORY",
                    Title = detail.Title,
                    ArtworkFallback = detail.PortraitFallback,
                    Accent = detail.Accent,
                    RuleLabel = detail.AbilityTrigger,
                    RuleName = detail.AbilityName,
                    ExactRule = detail.AbilitySummary,
                    Price = "ACQUIRED",
                    EconomyState = "EQUIP NOW",
                    Selected = detail.Selected,
                    Affordable = true,
                    Disabled = false,
                    Metrics = OfferFactProfiles.Select(acquiredKind, detail.Stats),
                    Detail = detail,
                };
            }
            return new MarketOfferCardModel
            {
                Key = detail.Key,
                Kind = MarketOfferKind.Sold,
                Classification = "MARKET RECEIPT",
                Title = "SOLD",
                ArtworkFallback = "—",
                Accent = "utility",
                EconomyState = "CLAIMED",
                Sold = true,
                Detail = detail,
            };
        }

        MarketOfferKind kind;
        string classification;
        switch (offer.Kind)
        {
            case OfferKind.Hero:
                bool rankUp = TryOwnedHero(offer.Id, out _);
                kind = rankUp ? MarketOfferKind.RankUp : MarketOfferKind.Recruit;
                classification = rankUp
                    ? "RANK UP"
                    : "RECRUIT · " + _presentation.Unit(offer.Id).role.ToUpperInvariant();
                break;
            case OfferKind.Weapon:
                kind = MarketOfferKind.Weapon;
                classification = $"{offer.Tier.ToString().ToUpperInvariant()} WEAPON";
                break;
            case OfferKind.Trinket:
                kind = MarketOfferKind.Trinket;
                classification = "TRINKET · EQUIPMENT";
                break;
            default:
                kind = MarketOfferKind.Inscription;
                classification = "INSCRIPTION · RUN LAW";
                break;
        }

        var facts = new List<StatChipModel>(detail.Stats);
        string qualifier = "";
        for (int i = facts.Count - 1; i >= 0; i--)
        {
            StatChipModel metric = facts[i];
            if (string.Equals(metric.Label, "CRIT", StringComparison.OrdinalIgnoreCase))
            {
                qualifier = metric.Value + " CRIT";
                facts.RemoveAt(i);
            }
        }
        if (kind == MarketOfferKind.Inscription)
        {
            facts.Add(new StatChipModel("SCOPE", "WARBAND", "",
                PresentationFactId.Scope));
            facts.Add(new StatChipModel("DURATION", "THIS RUN", "warn",
                PresentationFactId.Duration));
        }
        List<StatChipModel> metrics = OfferFactProfiles.Select(kind, facts);
        bool showMastery = kind == MarketOfferKind.Weapon &&
                           !string.IsNullOrEmpty(detail.PassiveSummary);

        // Unit offers disclose the tier being sold + its path state on the card itself —
        // the same diamond language as the rail cards (workbench-refactor approval).
        string tierLabel = "";
        List<RankTierSlotModel> tierPath = new List<RankTierSlotModel>();
        if (kind == MarketOfferKind.RankUp && detail.RankUpDetail != null)
        {
            tierLabel =
                $"{detail.RankUpDetail.CurrentRank} → {detail.RankUpDetail.NextRank}";
            tierPath = detail.RankUpDetail.Tiers;
        }
        else if (kind == MarketOfferKind.Recruit)
        {
            tierLabel = "RANK C";
            tierPath = detail.PathTiers;
        }

        int shortfall = Mathf.Max(0, offer.Price - _run.State.Sand);
        return new MarketOfferCardModel
        {
            Key = detail.Key,
            ContentId = detail.ContentId,
            Kind = kind,
            Classification = classification,
            TierLabel = tierLabel,
            PathTiers = tierPath,
            Title = detail.Title,
            Subtitle = detail.Subtitle,
            ArtworkResource = kind == MarketOfferKind.Recruit ||
                              kind == MarketOfferKind.RankUp
                ? detail.PortraitResource
                : "",
            ArtworkFallback = detail.PortraitFallback,
            Accent = detail.Accent,
            RuleLabel = showMastery ? detail.PassiveTrigger : detail.AbilityTrigger,
            RuleName = showMastery ? detail.PassiveName : detail.AbilityName,
            ExactRule = showMastery ? detail.PassiveSummary : detail.AbilitySummary,
            Qualifier = qualifier,
            CurrencyCost = offer.Price,
            CurrencyBalance = _run.State.Sand,
            EconomyState = "",
            Selected = detail.Selected,
            Affordable = shortfall == 0,
            Frozen = offer.Frozen,
            Disabled = detail.Disabled,
            Metrics = metrics,
            Detail = detail,
        };
    }

    private bool TryOwnedHero(string chassisId, out HeroInstance hero)
    {
        hero = _run.State.Field.Concat(_run.State.Bench)
            .FirstOrDefault(candidate => candidate.ChassisId == chassisId);
        return hero != null;
    }

    private CardModel RankUpCard(HeroInstance hero, string key)
    {
        UnitDef current = ComposeHero(hero);
        if (hero.Rank >= Rank.S)
            return DeferredRankUpCard(
                hero, current, key, "MAXIMUM RANK",
                "This champion has already reached Rank S.");

        HeroInstance guaranteedHero = hero.Clone();
        guaranteedHero.Rank++;
        UnitDef guaranteed = ComposeHero(guaranteedHero);
        // The DRAWN offer, not the authored pool: a non-fork rank shows a subset, so previewing
        // the pool here would advertise a card the rank-up then refuses to show.
        if (!_run.TryPeekSpecOffer(guaranteedHero, out var options))
            return DeferredRankUpCard(
                hero, current, key, "SPECIALIZATION PENDING",
                $"Choose {ContentLexicon.Chassis(hero.ChassisId).Name}'s Rank {hero.Rank} " +
                "specialization before previewing the next rank.");
        var previews = new List<ChoicePreviewModel>();
        foreach (string nodeId in options) previews.Add(RankChoicePreview(guaranteedHero, nodeId));
        string choiceLabel = $"1 OF {options.Count}";
        string optionNames = string.Join("  OR  ",
            options.Select(id => ContentLexicon.Node(id).Name));
        var presentation = _presentation.Unit(hero.ChassisId);
        var card = new CardModel
        {
            Key = key,
            ContentId = hero.ChassisId,
            Eyebrow = "RANK UP",
            Title = ContentLexicon.Chassis(hero.ChassisId).Name,
            Subtitle = $"{hero.Rank} → {guaranteedHero.Rank} · CHOOSE {choiceLabel}",
            InspectorSubtitle = "The rank and chassis gains lock immediately; then choose one specialization.",
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(ContentLexicon.Chassis(hero.ChassisId).Name),
            RoleIcon = presentation.roleIcon,
            Accent = presentation.accent,
            Rank = $"RANK {hero.Rank} → {guaranteedHero.Rank}",
            AbilityIcon = "▲",
            AbilityTrigger = "RANK UP · IMMEDIATE",
            AbilityName = $"{hero.Rank} → {guaranteedHero.Rank}",
            AbilitySummary =
                $"Gain {Signed(guaranteed.MaxHp - current.MaxHp)} HP and " +
                $"{Signed(guaranteed.Attack - current.Attack)} basic power. Then choose {choiceLabel.ToLowerInvariant()} specializations.",
            PassiveIcon = "◇",
            PassiveTrigger = $"SPECIALIZATION · CHOOSE {choiceLabel}",
            PassiveName = optionNames,
            PassiveSummary = "Both exact paths are previewed in the dossier before purchase.",
            Traits = BuildSpecBadges(hero.ChassisId, hero.SpecNodeIds),
            Stats = new List<StatChipModel>
            {
                new StatChipModel("RANK", $"{hero.Rank} → {guaranteedHero.Rank}", "warn",
                    PresentationFactId.Rank, "Current and resulting rank."),
                new StatChipModel("HP", Signed(guaranteed.MaxHp - current.MaxHp), "good",
                    PresentationFactId.Hp, "Guaranteed maximum HP gained before the choice."),
                new StatChipModel("POWER", Signed(guaranteed.Attack - current.Attack), "good",
                    PresentationFactId.BasicPower,
                    "Guaranteed basic attack power gained before the choice."),
                new StatChipModel("CHOICE", choiceLabel, "warn",
                    PresentationFactId.ChoiceCount,
                    "A blocking specialization choice follows the purchase."),
            },
            ComparisonTitle = "GUARANTEED RANK GAIN",
            Comparisons = ChangedFacts(current, guaranteed),
            ChoicePreviews = previews,
            Selected = key == _selectedCardKey,
        };
        card.RankUpDetail = BuildRankUpDetail(
            hero.Rank, guaranteedHero.Rank, card.Traits, previews);
        return card;
    }

    private CardModel DeferredRankUpCard(
        HeroInstance hero, UnitDef current, string key, string state, string explanation)
    {
        var presentation = _presentation.Unit(hero.ChassisId);
        string title = ContentLexicon.Chassis(hero.ChassisId).Name;
        return new CardModel
        {
            Key = key,
            ContentId = hero.ChassisId,
            Eyebrow = "RANK UP · WAITING",
            Title = title,
            Subtitle = $"RANK {hero.Rank} · {state}",
            InspectorSubtitle = explanation,
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(title),
            RoleIcon = presentation.roleIcon,
            Accent = presentation.accent,
            Rank = $"RANK {hero.Rank}",
            AbilityIcon = "◇",
            AbilityTrigger = "RANK UP · WAITING",
            AbilityName = state,
            AbilitySummary = explanation,
            Traits = BuildSpecBadges(hero.ChassisId, hero.SpecNodeIds),
            // A waiting rank offer is still a RANK offer, and MarketOfferPresentationContract holds
            // every RankUp card to disclosing the rank. Without this the contract fires the moment a
            // recruit is bought and its Rank C specialization is still unchosen — the deferred card
            // is exactly the state you land in after buying.
            Stats = WithRankFact(StatChips(current), $"RANK {hero.Rank}"),
            Disabled = true,
            Selected = key == _selectedCardKey,
        };
    }

    /// <summary>Front-load the rank fact so the offer profile can always find one.</summary>
    private static List<StatChipModel> WithRankFact(List<StatChipModel> stats, string value)
    {
        var facts = new List<StatChipModel>(stats ?? new List<StatChipModel>());
        for (int i = 0; i < facts.Count; i++)
            if (facts[i].Id == PresentationFactId.Rank) return facts;
        facts.Insert(0, new StatChipModel("RANK", value, "warn",
            PresentationFactId.Rank, "This champion's current rank."));
        return facts;
    }

    private static RankUpDetailModel BuildRankUpDetail(
        Rank currentRank, Rank nextRank,
        IReadOnlyList<WarbandSpecBadgeModel> selected,
        IReadOnlyList<ChoicePreviewModel> options)
    {
        var detail = new RankUpDetailModel
        {
            CurrentRank = currentRank.ToString(),
            NextRank = nextRank.ToString(),
            Options = new List<ChoicePreviewModel>(options),
        };
        for (int value = (int)Rank.B; value <= (int)Rank.S; value++)
        {
            string rank = ((Rank)value).ToString();
            WarbandSpecBadgeModel chosen = selected?.FirstOrDefault(
                trait => string.Equals(trait.Rank, rank, StringComparison.Ordinal));
            bool pending = value == (int)nextRank;
            detail.Tiers.Add(new RankTierSlotModel
            {
                Rank = rank,
                State = chosen != null
                    ? RankTierSlotState.Selected
                    : pending
                        ? RankTierSlotState.Pending
                        : RankTierSlotState.Locked,
                Icon = chosen?.Icon ?? (pending ? "▲" : "◇"),
                Name = chosen?.Name ?? (pending ? "CHOOSE 1 OF 2" : "LOCKED"),
                Rule = chosen?.Rule ?? (pending
                    ? $"Purchase the rank-up, then choose the Rank {rank} specialization."
                    : $"Reach Rank {rank} to unlock this specialization tier."),
                // The --pending state class styles this slot; "choice" is not an accent.
                Accent = chosen?.Accent ?? "",
            });
        }
        return detail;
    }

    private ChoicePreviewModel RankChoicePreview(HeroInstance guaranteedHero, string nodeId)
    {
        UnitDef before = ComposeHero(guaranteedHero);
        HeroInstance chosen = guaranteedHero.Clone();
        chosen.SpecNodeIds.Add(nodeId);
        UnitDef after = ComposeHero(chosen);
        SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
            guaranteedHero.ChassisId, nodeId, before, after);
        return new ChoicePreviewModel
        {
            Change = rule.Change.ToString().ToUpperInvariant(),
            Name = rule.Name,
            Summary = rule.Choice,
            Rule = rule.Full,
            Accent = SpecAccent(rule.Kind),
            Comparisons = ChangedFacts(before, after),
        };
    }

    private CardModel WeaponCard(WeaponDef weapon, WeaponTier tier, string key)
    {
        MechanicalRule mastery = MechanicalRulePresenter.WeaponMastery(weapon);
        string specialists = string.Join(", ", Kits.Chassis
            .Where(pair => pair.Value.Specializations.Contains(weapon.Category))
            .Select(pair => ContentLexicon.Chassis(pair.Key).Name));
        return new CardModel
        {
            Key = key,
            Title = weapon.Name,
            Subtitle = $"{tier} · {weapon.Category.ToUpperInvariant()}",
            PortraitFallback = "⚔",
            RoleIcon = "⚔",
            Accent = "power",
            AbilityIcon = weapon.HealAutos ? "✚" : "⚔",
            AbilityTrigger = "BASIC ATTACK · ALWAYS",
            AbilityName = weapon.HealAutos ? "RESTORING ATTACK" :
                              weapon.CleavePct > 0 ? "CLEAVING ATTACK" : "BASIC ATTACK",
            AbilitySummary = WeaponSummary(weapon, tier),
            PassiveIcon = "◇",
            PassiveTrigger = tier == WeaponTier.Relic
                ? "MASTERY · ALL WIELDERS · DOUBLED FOR SPECIALISTS"
                : "MASTERY · SPECIALISTS",
            PassiveName = weapon.Category.ToUpperInvariant() + " MASTERY",
            PassiveSummary = mastery.Full,
            Weapon = weapon.Name,
            WeaponSummary = WeaponSummary(weapon, tier),
            Stats = WeaponStats(weapon, tier),
            Tags = new List<string>
            {
                "WORN → HONED → RELIC",
                string.IsNullOrEmpty(specialists)
                    ? "NO NATURAL SPECIALIST"
                    : "MASTERED BY " + specialists.ToUpperInvariant(),
            },
            Selected = key == _selectedCardKey,
        };
    }

    private CardModel TrinketCard(TrinketDef trinket, string key)
    {
        MechanicalRule rules = MechanicalRulePresenter.Trinket(trinket);
        var stats = new List<StatChipModel>();
        if (trinket.HpBonus != 0)
            stats.Add(new StatChipModel("HP", Signed(trinket.HpBonus), "good",
                PresentationFactId.Hp, "Maximum HP change."));
        if (trinket.ManaMaxDelta != 0)
            stats.Add(new StatChipModel("MANA", Signed(trinket.ManaMaxDelta), "",
                PresentationFactId.ManaThreshold,
                "Change to the Mana required to cast the signature."));
        foreach (var rule in trinket.StatRules)
            if (rule.Stat == StatKind.AttackFlat && rule.When.Count == 0 &&
                rule.ScaleBy == StatScale.None)
                stats.Add(new StatChipModel("POWER", Signed(rule.Amount), "",
                    PresentationFactId.BasicPower, "Basic attack power change."));
        return new CardModel
        {
            Key = key,
            Eyebrow = "TRINKET",
            Title = trinket.Name,
            Subtitle = "One equipped trinket per champion",
            PortraitFallback = "◈",
            RoleIcon = "◈",
            Accent = "utility",
            AbilityIcon = "◇",
            AbilityTrigger = "EQUIPMENT · PASSIVE",
            AbilityName = rules.Change.ToString().ToUpperInvariant() + " RULE",
            AbilitySummary = rules.Full,
            InspectorAbilitySummary = rules.Full,
            Stats = stats,
            Selected = key == _selectedCardKey,
        };
    }

    private CardModel ItemCard(ItemRef item, int index)
    {
        string key = $"item:{index}";
        var card = item.Kind == ItemKind.Weapon
            ? WeaponCard(_content.Weapon(item.Id), item.Tier, key)
            : TrinketCard(_content.Trinket(item.Id), key);
        card.ItemInstanceId = item.InstanceId;
        card.EquipmentKind = (int)item.Kind;
        card.Eyebrow = item.Kind == ItemKind.Weapon ? "ARMORY · WEAPON" : "ARMORY · TRINKET";
        card.Selected = key == _selectedCardKey;
        card.Pinned = index == _selectedItem;
        return card;
    }

    private CardModel InscriptionCard(string id, int index, bool offered)
    {
        string key = offered ? $"reward:{index}" : $"inscription:{index}";
        var presentation = _presentation.Content(id);
        MechanicalRule rules = MechanicalRulePresenter.Inscription(_content.Inscription(id));
        return new CardModel
        {
            Key = key,
            ContentId = id,
            Eyebrow = offered ? "INSCRIPTION" : "BOUND INSCRIPTION",
            Title = _content.Inscription(id).Name,
            Subtitle = "Run-wide rule",
            PortraitFallback = "⌛",
            RoleIcon = presentation.icon,
            Accent = presentation.accent,
            AbilityIcon = presentation.icon,
            AbilityTrigger = "HOURSTONE · ALWAYS",
            AbilityName = "HOURSTONE LAW",
            AbilitySummary = rules.Full,
            InspectorAbilitySummary = rules.Full,
            Selected = key == _selectedCardKey,
        };
    }

    private CardModel RewardCard(RewardOffer reward, string key)
    {
        CardModel card = reward.Kind == OfferKind.Weapon
            ? WeaponCard(_content.Weapon(reward.Id), WeaponTier.Worn, key)
            : TrinketCard(_content.Trinket(reward.Id), key);
        card.Key = key;
        return card;
    }

    private CardModel SlotCard()
    {
        int next = _run.State.FieldSlots + 1;
        return new CardModel
        {
            Key = "slot",
            Eyebrow = "CAPACITY",
            Title = $"UNLOCK FIELD {next}",
            Subtitle = $"Capacity {next} of {_cfg.MaxFieldSlots}",
            PortraitFallback = "+",
            RoleIcon = "+",
            Accent = "mending",
            CurrencyCost = _run.SlotOfferCost,
            AbilityIcon = "⬡",
            AbilityTrigger = "MANAGEMENT · PERMANENT",
            AbilityName = "EXPAND THE MUSTER",
            AbilitySummary = "Permanently adds one active field place for this run.",
            Stats = new List<StatChipModel>
            {
                new StatChipModel("FIELD", $"{_run.State.FieldSlots} → {next}", "good",
                    PresentationFactId.FieldCapacity),
                new StatChipModel("GAIN", "+1", "good", PresentationFactId.RankDelta),
                new StatChipModel("CAP", _cfg.MaxFieldSlots.ToString()),
            },
            // Like ordinary stock, an unaffordable expansion remains inspectable.
            Disabled = false,
            Selected = _selectedCardKey == "slot",
        };
    }

    private MarketOfferCardModel MarketCapacityCard(CardModel detail)
    {
        int next = _run.State.FieldSlots + 1;
        int shortfall = Mathf.Max(0, _run.SlotOfferCost - _run.State.Sand);
        return new MarketOfferCardModel
        {
            Key = detail.Key,
            Kind = MarketOfferKind.Capacity,
            Classification = "CAPACITY · RUN-WIDE",
            Title = detail.Title,
            Subtitle = detail.Subtitle,
            ArtworkFallback = "+",
            Accent = detail.Accent,
            RuleLabel = detail.AbilityTrigger,
            RuleName = detail.AbilityName,
            ExactRule = detail.AbilitySummary,
            CurrencyCost = _run.SlotOfferCost,
            CurrencyBalance = _run.State.Sand,
            EconomyState = "",
            Selected = detail.Selected,
            Affordable = shortfall == 0,
            Metrics = new List<StatChipModel>
            {
                new StatChipModel("FIELD", $"{_run.State.FieldSlots} → {next}", "good",
                    PresentationFactId.FieldCapacity,
                    "Current and resulting active field capacity."),
                new StatChipModel("GAIN", "+1", "good",
                    PresentationFactId.RankDelta,
                    "One permanent active field place for this run."),
                new StatChipModel("CAP", _cfg.MaxFieldSlots.ToString(), "",
                    PresentationFactId.Unknown, "Maximum field capacity."),
            },
            Detail = detail,
        };
    }

    private void EnsurePlanningSelection(PlanningModel p)
    {
        var local = ActivePlanningCards(p);
        var all = AllPlanningCards(p);
        CardModel selected = local.FirstOrDefault(c => c.Key == _selectedCardKey);
        if (selected == null)
        {
            selected = local.FirstOrDefault();
            _selectedCardKey = selected?.Key ?? "";
        }
        foreach (var card in all) card.Selected = card.Key == _selectedCardKey;
        foreach (var offer in p.MarketOffers)
            offer.Selected = offer.Key == _selectedCardKey;
    }

    private List<CardModel> ActivePlanningCards(PlanningModel p)
    {
        var cards = new List<CardModel>();
        switch (_planningTab)
        {
            case PlanningTab.Market:
                cards.AddRange(p.Market);
                break;
            case PlanningTab.Armory:
                cards.AddRange(p.Armory);
                cards.AddRange(p.Field);
                cards.AddRange(p.Bench);
                break;
            case PlanningTab.Hourstone:
                cards.AddRange(p.Inscriptions);
                break;
            default:
                cards.AddRange(p.Field);
                cards.AddRange(p.Bench);
                break;
        }
        return cards;
    }

    private static List<CardModel> AllPlanningCards(PlanningModel p)
    {
        var all = new List<CardModel>();
        all.AddRange(p.Field);
        all.AddRange(p.Bench);
        all.AddRange(p.Market);
        all.AddRange(p.Armory);
        all.AddRange(p.Inscriptions);
        all.AddRange(p.Enemies);
        return all;
    }

    /// <summary>The highest SELECTED tier on the B/A/S ladder. No selections means the chassis is
    /// still at its floor, which is rank C. An empty ladder (an item, an Inscription) has no rank
    /// at all and returns blank, which hides the badge.</summary>
    private static string RankFromPath(List<RankTierSlotModel> tiers)
    {
        if (tiers == null || tiers.Count == 0) return "";
        string best = "C";
        foreach (RankTierSlotModel tier in tiers)
        {
            if (tier.State != RankTierSlotState.Selected) continue;
            if (tier.Rank == "S") return "S";
            if (tier.Rank == "A") best = "A";
            else if (tier.Rank == "B" && best == "C") best = "B";
        }
        return best;
    }

    private static UnitSheetModel CopyUnitSheet(
        UnitSheetModel source, IReadOnlyList<RankTierSlotModel> specs = null)
    {
        if (source == null) return null;
        return new UnitSheetModel
        {
            Combat = source.Combat,
            Enemy = source.Enemy,
            CoreFacts = new List<StatChipModel>(source.CoreFacts),
            WeaponIcon = source.WeaponIcon,
            WeaponName = source.WeaponName,
            WeaponFacts = new List<StatChipModel>(source.WeaponFacts),
            WeaponProperties = new List<RuleDeltaModel>(source.WeaponProperties),
            Signature = source.Signature,
            PassivesLabel = source.PassivesLabel,
            Passives = new List<InspectorSectionModel>(source.Passives),
            Specs = specs == null
                ? new List<RankTierSlotModel>(source.Specs)
                : new List<RankTierSlotModel>(specs),
            Targeting = source.Targeting,
            Statuses = new List<UnitStatusModel>(source.Statuses),
        };
    }

    private InspectorModel BuildInspector(PlanningModel p)
    {
        var card = AllPlanningCards(p).FirstOrDefault(c => c.Key == _selectedCardKey);
        if (card == null) return new InspectorModel { Empty = true };

        var inspector = new InspectorModel
        {
            Key = card.Key,
            Kind = DetailKindFor(p, card),
            Eyebrow = card.Eyebrow,
            Title = card.Title,
            Subtitle = InspectorSubtitle(card),
            PortraitResource = card.PortraitResource,
            PortraitFallback = card.PortraitFallback,
            Accent = card.Accent,
            AbilityIcon = card.AbilityIcon,
            AbilityTrigger = card.AbilityTrigger,
            AbilityName = card.AbilityName,
            AbilitySummary = string.IsNullOrEmpty(card.InspectorAbilitySummary)
                ? card.AbilitySummary
                : card.InspectorAbilitySummary,
            AbilityManaCost = card.AbilityManaCost,
            Price = card.Price,
            CurrencyCost = card.CurrencyCost,
            CurrencyBalance = card.CurrencyCost >= 0 ? _run.State.Sand : -1,
            Stats = new List<StatChipModel>(card.Stats),
            Tags = new List<string>(card.Tags),
            Traits = new List<WarbandSpecBadgeModel>(card.Traits),
            KeywordNotes = new List<string>(card.KeywordNotes),
            WeaponName = card.Weapon,
            WeaponSummary = string.IsNullOrEmpty(card.WeaponSummary)
                ? WeaponInspectorSummary(card)
                : card.WeaponSummary,
            WeaponProperty = card.WeaponProperty,
            ComparisonTitle = card.ComparisonTitle,
            Comparisons = new List<StatComparisonModel>(card.Comparisons),
            ChoicePreviews = new List<ChoicePreviewModel>(card.ChoicePreviews),
            RankUpDetail = card.RankUpDetail,
            PathTiers = new List<RankTierSlotModel>(card.PathTiers),
            UnitSheet = CopyUnitSheet(card.UnitSheet, card.PathTiers),
            // Rank is DERIVED from the ladder rather than carried separately: the highest
            // selected tier IS the hero's rank, and PathTiers is already on this model. One fact,
            // one channel — a second rank field would be a second thing to keep in sync.
            Rank = RankFromPath(card.PathTiers),
        };

        // No synthetic passive back-fill: a card without a passive simply has no passive
        // section. "CARD TYPE / DETAIL" filler was noise on every item dossier
        // (Design/workbench-dossier.md, law 5).
        if (!string.IsNullOrEmpty(card.PassiveName))
        {
            inspector.PassiveIcon = card.PassiveIcon;
            inspector.PassiveTrigger = card.PassiveTrigger;
            inspector.PassiveName = card.PassiveName;
            inspector.PassiveSummary = card.PassiveSummary;
        }

        if (TrySimpleIndex(card.Key, "market", out var offerIndex))
        {
            var offer = _run.State.ShopOffers[offerIndex];
            if (offer != null)
            {
                int shortfall = Mathf.Max(0, offer.Price - _run.State.Sand);
                bool affordable = shortfall == 0;
                bool actionable = affordable &&
                                  !card.Disabled &&
                                  _run.State.PendingSpec == null;
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Buy,
                    Label = "BUY",
                    CurrencyCost = offer.Price,
                    CurrencyBalance = _run.State.Sand,
                    Primary = true,
                    Enabled = actionable,
                    DisabledReason = !affordable
                        ? "Not enough Hourstone."
                        : card.Disabled || _run.State.PendingSpec != null
                            ? "Choose the pending specialization first."
                            : "",
                });
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Freeze,
                    Label = offer.Frozen ? "RELEASE STOCK" : "HOLD STOCK",
                });
            }
            else if (offerIndex == _equipNowOfferIndex &&
                     _run.IndexOfItem(_equipNowItemInstanceId) >= 0)
            {
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.EquipNow,
                    Label = "EQUIP NOW  ›",
                    Primary = true,
                });
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.KeepShopping,
                    Label = "KEEP SHOPPING",
                });
            }
        }
        else if (card.Key == "slot")
        {
            int shortfall = Mathf.Max(0, _run.SlotOfferCost - _run.State.Sand);
            bool affordable = shortfall == 0;
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.BuySlot,
                Label = "UNLOCK FIELD",
                CurrencyCost = _run.SlotOfferCost,
                CurrencyBalance = _run.State.Sand,
                Primary = true,
                Enabled = affordable,
                DisabledReason = affordable
                    ? ""
                    : "Not enough Hourstone.",
            });
        }
        else if (TryHeroAddress(card.Key, out var inBench, out var heroIndex))
        {
            if (_selectedItem >= 0 && _selectedItem < _run.State.Inventory.Count)
            {
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Equip,
                    Label = "EQUIP SELECTED",
                    Primary = true,
                });
                BuildEquipmentComparison(inspector,
                    (inBench ? _run.State.Bench : _run.State.Field)[heroIndex],
                    _run.State.Inventory[_selectedItem]);
            }

            var hero = (inBench ? _run.State.Bench : _run.State.Field)[heroIndex];
            if (hero.WeaponId != null)
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Unequip,
                    Label = "UNEQUIP WEAPON",
                });

            WeaponTier ceiling = _cfg.TierCeiling(_run.State.Act);
            bool canTemper = hero.WeaponTier < ceiling;
            int forgeCost = canTemper ? _cfg.ReforgeCosts[(int)hero.WeaponTier] : 0;
            bool canAffordForge = canTemper && _run.State.Sand >= forgeCost;
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.Reforge,
                Label = canTemper
                    ? $"FORGE · {hero.WeaponTier.ToString().ToUpperInvariant()} → " +
                      $"{(hero.WeaponTier + 1).ToString().ToUpperInvariant()}"
                    : $"FORGE CEILING · {ceiling.ToString().ToUpperInvariant()}",
                CurrencyCost = canTemper ? forgeCost : -1,
                CurrencyBalance = canTemper ? _run.State.Sand : -1,
                Enabled = canAffordForge,
                DisabledReason = !canTemper
                    ? $"Act {_run.State.Act} forge ceiling is {ceiling}."
                    : $"Balance {_run.State.Sand}; cost {forgeCost}.",
            });

            bool canMove = inBench
                ? _run.State.Field.Count < _run.State.FieldSlots
                : _run.State.Bench.Count < _cfg.BenchSlots;
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.Move,
                Label = inBench ? "MOVE TO FIELD" : "MOVE TO RESERVE",
                Enabled = canMove,
                DisabledReason = canMove ? "" :
                    inBench ? "The field is at capacity." : "The reserve is full.",
            });
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.SellHero,
                Label = hero.GoldSpent == 0
                    ? "DISMISS · NO REFUND"
                    : "DISMISS · REFUND",
                CurrencyCost = hero.GoldSpent == 0
                    ? -1
                    : hero.GoldSpent * _cfg.SellPct / 100,
                CurrencyGain = hero.GoldSpent > 0,
            });
        }
        else if (TrySimpleIndex(card.Key, "item", out var itemIndex))
        {
            var item = _run.State.Inventory[itemIndex];
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.SellItem,
                Label = "SELL · REFUND",
                CurrencyCost = item.SandInvested * _cfg.SellPct / 100,
                CurrencyGain = true,
            });
        }
        if (TrySimpleIndex(card.Key, "market", out int previewOfferIndex) &&
            previewOfferIndex >= 0 &&
            previewOfferIndex < _run.State.ShopOffers.Count)
        {
            ShopOffer previewOffer = _run.State.ShopOffers[previewOfferIndex];
            if (previewOffer != null &&
                (previewOffer.Kind == OfferKind.Weapon ||
                 previewOffer.Kind == OfferKind.Trinket))
                inspector.EquipmentPreview = BuildMarketEquipmentPreview(previewOffer);
        }
        BuildInspectorSections(inspector);
        return inspector;
    }

    private DecisionDetailKind DetailKindFor(PlanningModel planning, CardModel card)
    {
        if (card.Key == "slot") return DecisionDetailKind.Capacity;
        if (TryHeroAddress(card.Key, out _, out _)) return DecisionDetailKind.Champion;
        if (card.Key.StartsWith("inscription:", StringComparison.Ordinal) ||
            card.Key.StartsWith("reward:", StringComparison.Ordinal))
            return DecisionDetailKind.Inscription;
        if (TrySimpleIndex(card.Key, "item", out var itemIndex) &&
            itemIndex >= 0 && itemIndex < _run.State.Inventory.Count)
            return _run.State.Inventory[itemIndex].Kind == ItemKind.Weapon
                ? DecisionDetailKind.Weapon
                : DecisionDetailKind.Trinket;
        MarketOfferCardModel market = planning.MarketOffers.FirstOrDefault(
            offer => offer.Key == card.Key);
        return market?.Kind switch
        {
            MarketOfferKind.RankUp => DecisionDetailKind.RankUp,
            MarketOfferKind.Weapon => DecisionDetailKind.Weapon,
            MarketOfferKind.Trinket => DecisionDetailKind.Trinket,
            MarketOfferKind.Inscription => DecisionDetailKind.Inscription,
            MarketOfferKind.Capacity => DecisionDetailKind.Capacity,
            _ => DecisionDetailKind.Recruit,
        };
    }

    private void BuildInspectorSections(InspectorModel inspector)
    {
        inspector.Sections.Clear();
        if (inspector.UnitSheet != null) return;
        if (inspector.Kind == DecisionDetailKind.RankUp &&
            inspector.RankUpDetail != null)
            return;

        void Rule(string label, string icon, string name, string summary,
                  InspectorSectionRole role = InspectorSectionRole.Primary,
                  UiGlyphId labelGlyph = UiGlyphId.Unknown, string labelValue = "")
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(summary)) return;
            inspector.Sections.Add(new InspectorSectionModel
            {
                Kind = InspectorSectionKind.Rule,
                Role = role,
                Label = label,
                Icon = icon,
                Name = name,
                Summary = summary,
                LabelGlyph = labelGlyph,
                LabelValue = labelValue,
            });
        }

        // Section role and order per kind (Design/workbench-dossier.md): the signature leads a
        // hero dossier because it IS the hero's identity; the passive defers to one compact
        // line. The in-fight Combatant card keeps everything primary — mid-combat there is no
        // second look.
        switch (inspector.Kind)
        {
            case DecisionDetailKind.Champion:
            case DecisionDetailKind.Recruit:
                Rule("SIGNATURE", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary, InspectorSectionRole.Primary,
                    inspector.AbilityManaCost >= 0 ? UiGlyphId.Mana : UiGlyphId.Unknown,
                    inspector.AbilityManaCost >= 0
                        ? inspector.AbilityManaCost.ToString()
                        : "");
                Rule("WEAPON", inspector.WeaponIcon, inspector.WeaponName,
                    inspector.WeaponSummary);
                Rule(PassiveSectionLabel(inspector.PassiveTrigger),
                    inspector.PassiveIcon, inspector.PassiveName,
                    inspector.PassiveSummary, InspectorSectionRole.Deferred);
                break;
            case DecisionDetailKind.Combatant:
                // Order follows the question a spectator asks: what is it about to cast, what is
                // it swinging, what does it do back. SPECS come last and render through the PATH
                // block below (BindPath), which already carries hover + focus per row.
                Rule("SIGNATURE", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary, InspectorSectionRole.Primary,
                    inspector.AbilityManaCost >= 0 ? UiGlyphId.Mana : UiGlyphId.Unknown,
                    inspector.AbilityManaCost >= 0
                        ? inspector.AbilityManaCost.ToString()
                        : "");
                Rule("WEAPON", inspector.WeaponIcon, inspector.WeaponName,
                    inspector.WeaponSummary);
                Rule(PassiveSectionLabel(inspector.PassiveTrigger),
                    inspector.PassiveIcon, inspector.PassiveName,
                    inspector.PassiveSummary);
                break;
            case DecisionDetailKind.Weapon:
                Rule("WEAPON PROFILE", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary);
                Rule(inspector.PassiveTrigger, inspector.PassiveIcon, inspector.PassiveName,
                    inspector.PassiveSummary, InspectorSectionRole.Deferred);
                break;
            case DecisionDetailKind.Trinket:
                Rule("EQUIPPED RULE", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary);
                break;
            case DecisionDetailKind.Inscription:
                Rule("RUN-WIDE LAW", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary);
                break;
            case DecisionDetailKind.RankUp:
                Rule("GUARANTEED RANK GAIN", inspector.AbilityIcon, inspector.AbilityName,
                    inspector.AbilitySummary);
                break;
            case DecisionDetailKind.Capacity:
                inspector.Sections.Add(new InspectorSectionModel
                {
                    Kind = InspectorSectionKind.Capacity,
                    Label = "FIELD CAPACITY · PERMANENT",
                    Name = inspector.AbilityName,
                    Summary = inspector.AbilitySummary,
                    CapacityBefore = _run.State.FieldSlots,
                    CapacityAfter = Mathf.Min(_cfg.MaxFieldSlots, _run.State.FieldSlots + 1),
                    CapacityMax = _cfg.MaxFieldSlots,
                });
                break;
        }

        // With an equip preview attached, the preview IS the decision — every rule section
        // drops to a compact deferred line so the comparison owns the space
        // (Design/workbench-dossier.md: weapon/trinket primary content).
        if (inspector.EquipmentPreview != null)
            foreach (InspectorSectionModel section in inspector.Sections)
                if (section.Kind == InspectorSectionKind.Rule)
                    section.Role = InspectorSectionRole.Deferred;

        if (inspector.Comparisons.Count > 0 && inspector.EquipmentPreview == null)
            inspector.Sections.Add(new InspectorSectionModel
            {
                Kind = InspectorSectionKind.Comparison,
                Label = string.IsNullOrEmpty(inspector.ComparisonTitle)
                    ? "EXACT CHANGE"
                    : inspector.ComparisonTitle,
                Comparisons = new List<StatComparisonModel>(inspector.Comparisons),
            });
        if (inspector.ChoicePreviews.Count > 0)
            inspector.Sections.Add(new InspectorSectionModel
            {
                Kind = InspectorSectionKind.Choices,
                Role = inspector.Kind == DecisionDetailKind.Recruit
                    ? InspectorSectionRole.Deferred
                    : InspectorSectionRole.Primary,
                Label = "SPECIALIZATION PREVIEW · CHOOSE 1 OF 2 AFTER RANK-UP",
                Choices = new List<ChoicePreviewModel>(inspector.ChoicePreviews),
            });
    }

    private static string PassiveSectionLabel(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return "PASSIVE";
        const string always = " · ALWAYS";
        return trigger.EndsWith(always, StringComparison.OrdinalIgnoreCase)
            ? trigger.Substring(0, trigger.Length - always.Length)
            : trigger;
    }

    private EquipmentPreviewModel BuildMarketEquipmentPreview(ShopOffer offer)
    {
        var model = new EquipmentPreviewModel();
        if (offer == null || _run.State.Field.Count == 0) return model;

        var item = new ItemRef
        {
            Kind = offer.Kind == OfferKind.Weapon ? ItemKind.Weapon : ItemKind.Trinket,
            Id = offer.Id,
            Tier = offer.Tier,
        };

        int selectedIndex = -1;
        if (TryHeroAddress(_comparisonTargetHeroKey, out bool inBench, out int targetIndex) &&
            !inBench && targetIndex >= 0 && targetIndex < _run.State.Field.Count)
            selectedIndex = targetIndex;
        if (selectedIndex < 0 && _focusedWarbandHeroId > 0)
            selectedIndex = _run.State.Field.FindIndex(
                hero => hero.InstanceId == _focusedWarbandHeroId);
        if (selectedIndex < 0) selectedIndex = 0;

        _comparisonTargetHeroKey = HeroKey(RosterZone.Field, selectedIndex);
        HeroInstance selectedHero = _run.State.Field[selectedIndex];
        for (int i = 0; i < _run.State.Field.Count; i++)
        {
            HeroInstance hero = _run.State.Field[i];
            string heroKey = HeroKey(RosterZone.Field, i);
            var presentation = _presentation.Unit(hero.ChassisId);
            model.Recipients.Add(new RecipientPreviewModel
            {
                HeroKey = heroKey,
                DisplayName = ContentLexicon.Chassis(hero.ChassisId).Name,
                PortraitResource = presentation.portrait,
                PortraitFallback = Initials(ContentLexicon.Chassis(hero.ChassisId).Name),
                RankText = "RANK " + hero.Rank,
                CurrentItemName = EquippedItemName(hero, item.Kind),
                IsEligible = true,
                IsSelected = heroKey == _comparisonTargetHeroKey,
            });
        }

        model.SelectedRecipientHeroKey = _comparisonTargetHeroKey;
        model.CurrentItemName = EquippedItemName(selectedHero, item.Kind);
        model.OfferedItemName = item.Kind == ItemKind.Weapon
            ? _content.Weapon(item.Id).Name
            : _content.Trinket(item.Id).Name;
        model.StatDeltas = BuildEquipmentComparisons(selectedHero, item);

        if (item.Kind == ItemKind.Weapon)
        {
            WeaponDef current = selectedHero.WeaponId == null
                ? _content.Chassis(selectedHero.ChassisId).StarterWeapon
                : _content.Weapon(selectedHero.WeaponId);
            model.LostRule = WeaponRuleDelta(
                selectedHero, current, selectedHero.WeaponTier);
            model.GainedRule = WeaponRuleDelta(
                selectedHero, _content.Weapon(item.Id), item.Tier);
        }
        else
        {
            if (selectedHero.TrinketIds.Count > 0)
                model.LostRule = TrinketRuleDelta(
                    _content.Trinket(selectedHero.TrinketIds[0]));
            model.GainedRule = TrinketRuleDelta(_content.Trinket(item.Id));
        }
        return model;
    }

    private string EquippedItemName(HeroInstance hero, ItemKind kind)
    {
        if (kind == ItemKind.Weapon)
            return hero.WeaponId == null
                ? _content.Chassis(hero.ChassisId).StarterWeapon.Name
                : _content.Weapon(hero.WeaponId).Name;
        return hero.TrinketIds.Count == 0
            ? "Empty trinket socket"
            : _content.Trinket(hero.TrinketIds[0]).Name;
    }

    private RuleDeltaModel WeaponRuleDelta(HeroInstance hero, WeaponDef weapon,
                                           WeaponTier tier)
    {
        bool specialist = _content.Chassis(hero.ChassisId)
            .Specializations.Contains(weapon.Category);
        bool applies = specialist || tier == WeaponTier.Relic;
        SkirmishProof.MasteryCopy.TryGetValue(
            weapon.Category, out WeaponMasteryCopy copy);
        string fallbackName = weapon.Category.ToUpperInvariant() + " MASTERY";
        string full = MechanicalRulePresenter.WeaponMastery(weapon).Full;
        return new RuleDeltaModel
        {
            RuleName = copy?.Name ?? fallbackName,
            ShortSummary = copy?.Text ?? full,
            FullDescription = applies
                ? full
                : $"{full} Inactive: {ContentLexicon.Chassis(hero.ChassisId).Name} " +
                  $"is not a {weapon.Category} specialist and this weapon is not Relic.",
            Icon = "◇",
            Applies = applies,
        };
    }

    /// <summary>
    /// The unit dossier's Weapon property row. `UnitDef` already carries the composed Weapon
    /// identity and temper, so this derives presentation without adding another run-domain fact.
    /// The concise line is for scanning; the generated rule remains the exact tooltip contract.
    /// </summary>
    private RuleDeltaModel BuildWeaponProperty(UnitDef def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.WeaponName)) return null;
        WeaponDef weapon = Weapons.All.Values.FirstOrDefault(
            candidate => string.Equals(
                candidate.Name, def.WeaponName, StringComparison.Ordinal));
        if (weapon == null) return null;

        bool specialist = !string.IsNullOrWhiteSpace(def.ChassisId) &&
                          _content.Chassis(def.ChassisId)
                              .Specializations.Contains(weapon.Category);
        bool applies = specialist || def.WeaponTier == WeaponTier.Relic;
        SkirmishProof.MasteryCopy.TryGetValue(
            weapon.Category, out WeaponMasteryCopy copy);
        string full = MechanicalRulePresenter.WeaponMastery(weapon).Full;
        string owner = string.IsNullOrWhiteSpace(def.ChassisId)
            ? def.Name
            : ContentLexicon.Chassis(def.ChassisId).Name;
        return new RuleDeltaModel
        {
            RuleName = copy?.Name ?? weapon.Category.ToUpperInvariant() + " MASTERY",
            DisplayName = copy?.Name ?? weapon.Category.ToUpperInvariant() + " MASTERY",
            ShortSummary = copy?.Text ?? full,
            FullDescription = applies
                ? full
                : $"{full} Inactive: {owner} is not a {weapon.Category} specialist and " +
                  "this weapon is not Relic.",
            Icon = "◆",
            Applies = applies,
        };
    }

    private RuleDeltaModel BuildWeaponProperty(PlaybackUnit unit)
    {
        if (unit == null || !string.IsNullOrWhiteSpace(unit.RoleId)) return null;
        return BuildWeaponProperty(new UnitDef
        {
            Name = unit.Name,
            ChassisId = unit.ChassisId,
            WeaponName = unit.WeaponName,
            WeaponTier = unit.WeaponTier,
        });
    }

    private static RuleDeltaModel TrinketRuleDelta(TrinketDef trinket)
    {
        MechanicalRule rule = MechanicalRulePresenter.Trinket(trinket);
        return new RuleDeltaModel
        {
            RuleName = trinket.Name,
            ShortSummary = rule.Full,
            FullDescription = rule.Full,
            Icon = "◇",
        };
    }

    private void BuildEquipmentComparison(InspectorModel inspector, HeroInstance hero, ItemRef item)
    {
        inspector.Comparisons.AddRange(BuildEquipmentComparisons(hero, item));
        string itemName = item.Kind == ItemKind.Weapon
            ? _content.Weapon(item.Id).Name
            : _content.Trinket(item.Id).Name;
        inspector.ComparisonTitle = "EQUIP PREVIEW · " + itemName.ToUpperInvariant();
    }

    private List<StatComparisonModel> BuildEquipmentComparisons(
        HeroInstance hero, ItemRef item)
    {
        UnitDef before = ComposeHero(hero);
        HeroInstance preview = hero.Clone();
        if (item.Kind == ItemKind.Weapon)
        {
            preview.WeaponId = item.Id;
            preview.WeaponTier = item.Tier;
        }
        else
        {
            preview.TrinketIds.Clear();
            preview.TrinketIds.Add(item.Id);
        }
        UnitDef after = ComposeHero(preview);

        // A weapon decision compares weapon-facing combat stats. Hero durability and signature
        // threshold are not part of the offered weapon profile, and showing unchanged copies of
        // them obscures the tradeoff the player is actually making.
        if (item.Kind != ItemKind.Weapon)
            return ChangedFacts(before, after);

        var result = new List<StatComparisonModel>();
        AddComparison(result, "BASIC POWER",
            before.HealAutos ? $"{before.Attack} HEAL" : $"{before.Attack} DMG",
            after.HealAutos ? $"{after.Attack} HEAL" : $"{after.Attack} DMG",
            after.Attack > before.Attack);
        AddComparison(result, "REACH", before.Range.ToString(), after.Range.ToString(),
            after.Range > before.Range);
        AddComparison(result, "CADENCE",
            $"{before.AttackInterval / 10f:0.0}s",
            $"{after.AttackInterval / 10f:0.0}s",
            after.AttackInterval < before.AttackInterval);
        AddComparison(result, "MANA / SWING",
            before.ManaPerSwing.ToString(), after.ManaPerSwing.ToString(),
            after.ManaPerSwing > before.ManaPerSwing);
        if (before.CritChance > 0 || after.CritChance > 0)
            AddComparison(result, "CRIT", $"{before.CritChance}%", $"{after.CritChance}%",
                after.CritChance > before.CritChance);
        if (before.CleavePct > 0 || after.CleavePct > 0)
            AddComparison(result, "CLEAVE", $"{before.CleavePct}%", $"{after.CleavePct}%",
                after.CleavePct > before.CleavePct);
        return result;
    }

    private static void AddComparison(List<StatComparisonModel> result, string label,
                                      string before, string after, bool improved)
    {
        result.Add(new StatComparisonModel
        {
            Label = label,
            Before = before,
            After = after,
            Tone = before == after ? "" : improved ? "good" : "bad",
            Direction = before == after
                ? DeltaDirection.Neutral
                : improved ? DeltaDirection.Positive : DeltaDirection.Negative,
        });
    }

    private static List<StatComparisonModel> ChangedFacts(UnitDef before, UnitDef after)
    {
        var result = new List<StatComparisonModel>();
        AddChanged(result, "HP", before.MaxHp, after.MaxHp, after.MaxHp > before.MaxHp);
        AddChanged(result, before.HealAutos || after.HealAutos ? "BASIC HEAL" : "BASIC POWER",
            before.Attack, after.Attack, after.Attack > before.Attack);
        AddChanged(result, "REACH", before.Range, after.Range, after.Range > before.Range);
        if (before.AttackInterval != after.AttackInterval)
            result.Add(new StatComparisonModel
            {
                Label = "CADENCE",
                Before = $"{before.AttackInterval / 10f:0.0}s",
                After = $"{after.AttackInterval / 10f:0.0}s",
                Tone = after.AttackInterval < before.AttackInterval ? "good" : "",
                Direction = after.AttackInterval < before.AttackInterval
                    ? DeltaDirection.Positive
                    : DeltaDirection.Negative,
            });
        AddChanged(result, "MANA / SWING", before.ManaPerSwing, after.ManaPerSwing,
            after.ManaPerSwing > before.ManaPerSwing);
        AddChanged(result, "SIGNATURE MANA", before.ManaMax, after.ManaMax,
            after.ManaMax < before.ManaMax);
        if (before.CritChance != after.CritChance)
            result.Add(new StatComparisonModel
            {
                Label = "CRIT",
                Before = before.CritChance + "%",
                After = after.CritChance + "%",
                Tone = after.CritChance > before.CritChance ? "good" : "",
                Direction = after.CritChance > before.CritChance
                    ? DeltaDirection.Positive
                    : DeltaDirection.Negative,
            });
        if (before.CleavePct != after.CleavePct)
            result.Add(new StatComparisonModel
            {
                Label = "CLEAVE",
                Before = before.CleavePct + "%",
                After = after.CleavePct + "%",
                Tone = after.CleavePct > before.CleavePct ? "good" : "",
                Direction = after.CleavePct > before.CleavePct
                    ? DeltaDirection.Positive
                    : DeltaDirection.Negative,
            });
        return result;
    }

    private static void AddChanged(List<StatComparisonModel> result, string label,
                                   int before, int after, bool improved)
    {
        if (before == after) return;
        result.Add(new StatComparisonModel
        {
            Label = label,
            Before = before.ToString(),
            After = after.ToString(),
            Tone = improved ? "good" : "bad",
            Direction = improved ? DeltaDirection.Positive : DeltaDirection.Negative,
        });
    }

    private string WeaponInspectorSummary(CardModel card) =>
        string.IsNullOrEmpty(card.Weapon)
            ? "This selection does not make a basic attack."
            : card.Stats.Count >= 4
                ? $"Uses {card.Weapon}. Damage, reach, and cadence above are the composed values that enter combat."
                : card.AbilitySummary;

    private static string InspectorSubtitle(CardModel card)
    {
        string detail = string.IsNullOrEmpty(card.InspectorSubtitle)
            ? card.Subtitle
            : card.InspectorSubtitle;
        if (string.IsNullOrEmpty(card.Rank)) return detail;
        return card.Rank + (string.IsNullOrEmpty(detail) ? "" : " · " + detail);
    }

    private static List<StatChipModel> WeaponStats(WeaponDef w, WeaponTier tier)
    {
        int pct = tier == WeaponTier.Relic ? 150 : tier == WeaponTier.Honed ? 125 : 100;
        var stats = new List<StatChipModel>
        {
            new StatChipModel(w.HealAutos ? "HEAL" : "POWER",
                (w.Damage * pct / 100).ToString(), "",
                PresentationFactId.BasicPower,
                w.HealAutos ? "Healing per basic swing." : "Damage per basic swing."),
            new StatChipModel("REACH", w.Range.ToString(), "",
                PresentationFactId.Reach, "Maximum basic attack reach in hexes."),
            new StatChipModel("CADENCE", $"{w.Interval / 10f:0.0}s", "",
                PresentationFactId.Cadence, "Time between basic attacks."),
            new StatChipModel("MANA/HIT", w.ManaPerSwing.ToString(), "warn",
                PresentationFactId.ManaPerSwing,
                "Mana gained whenever this weapon completes a basic swing."),
        };
        if (w.CritChance > 0)
            stats.Add(new StatChipModel("CRIT", $"{w.CritChance}%", "",
                PresentationFactId.CritChance, "Base critical-hit chance."));
        if (w.CleavePct > 0)
            stats.Add(new StatChipModel("CLEAVE", $"{w.CleavePct}%", "",
                PresentationFactId.Cleave,
                "Share of basic damage dealt to enemies adjacent to the target."));
        return stats;
    }

    private static string WeaponSummary(WeaponDef w, WeaponTier tier)
    {
        int pct = tier == WeaponTier.Relic ? 150 : tier == WeaponTier.Honed ? 125 : 100;
        string verb = w.HealAutos ? "heals the most wounded ally for" : "deals";
        string extra = w.CleavePct > 0 ? $" and cleaves for {w.CleavePct}%" : "";
        return $"{verb} {w.Damage * pct / 100} every {w.Interval / 10f:0.0}s at reach {w.Range}{extra}. " +
               $"Gains {w.ManaPerSwing} Mana per completed swing.";
    }

    private static string SignatureTrigger(UnitDef def) =>
        def.ManaMax > 0 ? $"SIGNATURE · AT {def.ManaMax} MANA" : "SIGNATURE";

    private static string BasicAttackSummary(UnitDef def)
    {
        string result = def.HealAutos
            ? $"Heals the lowest-HP ally for {def.Attack}"
            : $"Deals {def.Attack} damage";
        result += $" every {def.AttackInterval / 10f:0.0}s at reach {def.Range}.";
        if (def.CritChance > 0) result += $" {def.CritChance}% critical chance.";
        return result;
    }

    private static string BasicAttackSummary(PlaybackUnit unit)
    {
        string result = unit.HealAutos
            ? $"Heals the lowest-HP ally for {unit.Attack}"
            : $"Deals {unit.Attack} damage";
        result += $" every {unit.AttackInterval / 10f:0.0}s at reach {unit.Range}.";
        if (unit.CritChance > 0) result += $" {unit.CritChance}% critical chance.";
        return result;
    }

    private static string Initials(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        var words = value.Split(new[] { ' ', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 1
            ? words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpperInvariant()
            : (words[0][0].ToString() + words[words.Length - 1][0]).ToUpperInvariant();
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private void BuildMap()
    {
        var m = _model.Map;
        var s = _run.State;
        m.ActLabel = $"ACT {s.Act} / {_cfg.Acts}";
        m.Gold = s.Gold.ToString();
        m.Warband = s.Field.Select(HeroCard).ToList();
        m.EnemyPreview.Clear();
        m.EncounterRule = "";
        m.ShowTiers = false;
        m.Tiers.Clear();

        bool atNode = s.Phase == RunPhase.Node;
        m.NodeLabel = atNode && _run.AtBoss ? "ACT BOSS" : $"NODE {s.NodeIndex + 1} / {_cfg.NodesPerAct}";

        m.Track = Enumerable.Range(0, _cfg.NodesPerAct + 1).Select(i =>
        {
            bool boss = i == _cfg.NodesPerAct;
            string kind = boss ? "Boss" : s.ActMaps[s.Act - 1][i].ToString();
            return new MapNodeModel
            {
                Label = boss ? "BOSS" : $"{i + 1}",
                Kind = kind,
                IsCurrent = atNode && i == s.NodeIndex,
                IsPast = i < s.NodeIndex,
            };
        }).ToList();

        if (!atNode)
        {
            m.NodeHeading = "";
            m.NodeBlurb = "";
            m.PrimaryText = "CONTINUE";
            m.PrimaryEnabled = false;
            return;
        }

        var nodeKind = _run.CurrentNodeKind;
        m.NodeKind = nodeKind.ToString();
        switch (nodeKind)
        {
            case NodeKind.Event:
                // This node leads into BuildInterludeBeat's three-way decision — Treasury (certainty),
                // Armory (equipment), or Hourstone (a run-wide rule) — which ALSO unlocks the next
                // field capacity. The previous copy ("Take the coin and move on" / TRAVEL ON) described
                // a beat that has not existed since ADR 0019 gave the Interlude real choices, and it
                // told the player to skip the decision the game was about to hand them.
                m.NodeHeading = "AN INTERLUDE";
                m.NodeBlurb = "No one contests the road. Take certainty, equipment, or a run-wide rule — and the field slot that comes with it.";
                m.PrimaryText = "TAKE THE INTERLUDE";
                break;
            case NodeKind.Boss:
            {
                // Per-act bosses (ADR 0024): the heading, rule and roster all come off the brief.
                // This previously hardcoded "THE LAST OATH" and Encounters.BondedPair(), which was
                // only ever true because every act fielded the same boss.
                var bossBrief = BriefForCurrentNode();
                m.NodeHeading = bossBrief == null ? "THE ACT BOSS" : bossBrief.Name.ToUpperInvariant();
                m.NodeBlurb = "The act will not let you past without this.";
                m.PrimaryText = "FACE THE BOSS";
                if (bossBrief != null)
                {
                    m.EncounterRule = $"{bossBrief.RuleName} — {bossBrief.RuleText}";
                    foreach (var u in bossBrief.Units)
                        m.EnemyPreview.Add($"{u.Name} · {u.Role} · {u.MaxHp} HP · reach {u.Range}");
                }
                break;
            }
            default:
                m.NodeHeading = "A CONTESTED CROSSING";
                m.NodeBlurb = "Set your wager. Greater risk pays more, and pays it per body you put down.";
                m.PrimaryText = "COMMIT THE WARBAND";
                m.ShowTiers = true;
                m.Tiers = new[] { FightTier.Safe, FightTier.Even, FightTier.Greedy }.Select(t =>
                    new TierChoiceModel
                    {
                        Index = (int)t,
                        Name = t.ToString().ToUpperInvariant(),
                        Risk = t == FightTier.Safe ? "Low stakes" : t == FightTier.Even ? "Fair stakes" : "High stakes",
                        Reward = $"pot {_cfg.Pot(s.Act, t)}",
                        Selected = t == _tier,
                    }).ToList();
                break;
        }
        m.PrimaryEnabled = true;
    }

    private void BuildShop()
    {
        var sh = _model.Shop;
        var s = _run.State;
        sh.Heading = $"THE FORGE FOLLOWS THE FRONT — ACT {s.Act}";
        sh.Gold = s.Gold.ToString();
        sh.RerollCost = _cfg.RerollCost.ToString();
        sh.CanReroll = s.Phase == RunPhase.Shop && s.PendingSpec == null && s.Gold >= _cfg.RerollCost;
        sh.ContinueText = _run.AtBoss ? "MARCH ON" : "LEAVE THE CAMP";

        sh.Offers = new List<ShopOfferModel>();
        for (int i = 0; i < s.ShopOffers.Count; i++)
        {
            var o = s.ShopOffers[i];
            if (o == null)
            {
                sh.Offers.Add(new ShopOfferModel { Index = i, Kind = "", Name = "Sold", Sold = true });
                continue;
            }
            sh.Offers.Add(new ShopOfferModel
            {
                Index = i,
                Kind = o.Kind.ToString().ToUpperInvariant(),
                Name = OfferName(o),
                Detail = OfferDetail(o),
                Price = o.Price.ToString(),
                Affordable = s.Gold >= o.Price,
                Frozen = o.Frozen,
            });
        }

        sh.Field = s.Field.Select((h, i) => RosterCard(h, i, inBench: false)).ToList();
        sh.Bench = s.Bench.Select((h, i) => RosterCard(h, i, inBench: true)).ToList();

        sh.SelectedItemIndex = _selectedItem;
        sh.Inventory = new List<InventoryItemModel>();
        for (int i = 0; i < s.Inventory.Count; i++)
        {
            var it = s.Inventory[i];
            bool weapon = it.Kind == ItemKind.Weapon;
            sh.Inventory.Add(new InventoryItemModel
            {
                Index = i,
                Kind = weapon ? "WEAPON" : "TRINKET",
                Name = weapon ? _content.Weapon(it.Id).Name : _content.Trinket(it.Id).Name,
                Detail = weapon ? WeaponLine(_content.Weapon(it.Id), it.Tier) : "Trinket",
                SellLabel = "SELL",
                Selected = i == _selectedItem,
            });
        }
        sh.InventoryHint = s.Inventory.Count == 0
            ? ""
            : _selectedItem >= 0
                ? "Now choose a champion to equip it to."
                : "Select an item, then a champion.";

        sh.SlotOfferOpen = _run.SlotOfferOpen;
        if (sh.SlotOfferOpen)
        {
            sh.SlotOfferText = $"Room for another banner: field slot {s.FieldSlots + 1} for {_run.SlotOfferCost} gold.";
            sh.SlotAffordable = s.Gold >= _run.SlotOfferCost;
        }

        var pending = s.PendingSpec;
        if (pending == null) { sh.SpecChoice = new SpecChoiceModel(); return; }

        sh.SpecChoice = new SpecChoiceModel
        {
            Pending = true,
            HeroName = ContentLexicon.Chassis(HeroAt(pending).ChassisId).Name,
            RankLabel = $"RANK {pending.ForRank}",
        };
        HeroInstance pendingHero = HeroAt(pending);
        UnitDef pendingBefore = ComposeHero(pendingHero);
        foreach (string nodeId in pending.Options)
        {
            HeroInstance chosen = pendingHero.Clone();
            chosen.SpecNodeIds.Add(nodeId);
            SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
                pendingHero.ChassisId, nodeId, pendingBefore, ComposeHero(chosen));
            sh.SpecChoice.Options.Add(new SpecOptionModel
            {
                Name = rule.Name,
                Text = rule.Choice,
            });
        }
    }

    private HeroInstance HeroAt(PendingSpec p) =>
        (p.Zone == RosterZone.Field ? _run.State.Field : _run.State.Bench)[p.Index];

    private void BuildDeploy()
    {
        var d = _model.Deploy;
        var s = _run.State;
        bool boss = s.Phase == RunPhase.Planning && _run.AtBoss;

        d.Heading = boss ? "DEPLOY — ACT BOSS" : "DEPLOY";
        d.Total = s.Field.Count;
        d.Placed = _placement.Count;
        d.CanCommit = _placement.Count == s.Field.Count && s.Field.Count > 0;
        d.PrimaryText = "LOCK IN";

        d.Instruction = _deploySelected >= 0
            ? "Click a hex to place · or drag a champion directly. Occupied hexes swap."
            : d.CanCommit
                ? "Formation set · drag any champion to refine it, or lock it in."
                : "Select a champion, then click a hex · placed champions can be dragged.";

        d.Roster = new List<HeroCardModel>();
        for (int i = 0; i < s.Field.Count; i++)
        {
            var card = HeroCard(s.Field[i]);
            card.Index = i;
            card.InBench = false;
            card.Selected = i == _deploySelected;
            card.Interactable = !_placement.ContainsKey(i);   // false = already on the board
            d.Roster.Add(card);
        }

        d.Enemies = new List<DeployEnemyRowModel>();
        d.EncounterRuleName = "";
        d.EncounterRule = "";
        d.SelectedEnemy = null;
        if (s.Phase == RunPhase.Planning)
        {
            // Every fight discloses its rule, not just the boss, and the row names the authored
            // monster rather than the hero silhouette it borrows. Structured rows, not a
            // dot-separated string: a CSV is not a card, and the row now carries exactly what the
            // unit card will show when it is selected.
            var brief = BriefForCurrentNode();
            if (brief != null)
            {
                d.EncounterRuleName = brief.RuleName ?? "";
                d.EncounterRule = brief.RuleText ?? "";
                for (int i = 0; i < brief.Units.Count; i++)
                {
                    var u = brief.Units[i];
                    d.Enemies.Add(new DeployEnemyRowModel
                    {
                        Key = $"enemy:{i}",
                        Name = u.Name,
                        Role = u.Role,
                        Row = u.Row,
                        MaxHp = u.MaxHp,
                        Attack = u.Attack,
                        Range = u.Range,
                        AttackIntervalTicks = u.AttackIntervalTicks,
                        WeaponName = u.WeaponName,
                        Behavior = u.Behavior,
                        Accent = u.Accent,
                        Selected = _deployEnemyKey == $"enemy:{i}",
                    });
                }
            }
            else
            {
                int i = 0;
                foreach (var e in EnemiesForCurrentNode())
                {
                    d.Enemies.Add(new DeployEnemyRowModel
                    {
                        Key = $"enemy:{i}",
                        Name = e.Def.Name,
                        Row = e.Pos.Row,
                        MaxHp = e.Def.MaxHp,
                        Attack = e.Def.Attack,
                        Range = e.Def.Range,
                        AttackIntervalTicks = e.Def.AttackInterval,
                        ManaMax = e.Def.ManaMax,
                        ManaPerSwing = e.Def.ManaPerSwing,
                        CritChance = e.Def.CritChance,
                        CleavePct = e.Def.CleavePct,
                        HealAutos = e.Def.HealAutos,
                        WeaponName = e.Def.WeaponName,
                        Role = Enemies.RoleLabel(e.Def.RoleId),
                        Behavior = Enemies.Behavior(e.Def.Name),
                        Accent = Enemies.RoleAccent(e.Def.RoleId),
                        Selected = _deployEnemyKey == $"enemy:{i}",
                    });
                    i++;
                }
            }

            foreach (var enemy in d.Enemies)
                if (enemy.Selected) { d.SelectedEnemy = EnemyInspector(enemy); break; }
        }
    }

    /// <summary>
    /// The shared unit card, bound to a deployment-preview enemy. Same component and same section
    /// grammar the fight uses — the only difference is that nothing here is live, so there are no
    /// current/max readings and no LIVE statuses.
    /// </summary>
    private static InspectorModel EnemyInspector(DeployEnemyRowModel enemy)
    {
        var stats = new List<StatChipModel>
        {
            new StatChipModel("HP", enemy.MaxHp.ToString(), "", PresentationFactId.Hp),
        };
        if (enemy.Attack > 0)
        {
            stats.Add(new StatChipModel("POWER", enemy.Attack.ToString(), "",
                PresentationFactId.BasicPower));
            stats.Add(new StatChipModel("REACH", enemy.Range.ToString(), "",
                PresentationFactId.Reach));
            stats.Add(new StatChipModel("CADENCE", $"{enemy.AttackIntervalTicks / 10f:0.0}s", "",
                PresentationFactId.Cadence));
            if (enemy.ManaPerSwing > 0)
                stats.Add(new StatChipModel("MANA/HIT", enemy.ManaPerSwing.ToString(), "",
                    PresentationFactId.ManaPerSwing));
            if (enemy.CritChance > 0)
                stats.Add(new StatChipModel("CRIT", $"{enemy.CritChance}%", "",
                    PresentationFactId.CritChance));
            if (enemy.CleavePct > 0)
                stats.Add(new StatChipModel("CLEAVE", $"{enemy.CleavePct}%", "",
                    PresentationFactId.Cleave));
        }

        string attack = enemy.Attack <= 0
            ? "Never swings."
            : $"{enemy.Attack} damage every {enemy.AttackIntervalTicks / 10f:0.0}s at reach {enemy.Range}.";

        var inspector = new InspectorModel
        {
            Kind = DecisionDetailKind.Combatant,
            Key = enemy.Key,
            Eyebrow = string.IsNullOrEmpty(enemy.Role)
                ? $"ENEMY · ROW {enemy.Row + 1}"
                : $"{enemy.Role.ToUpperInvariant()} · ROW {enemy.Row + 1}",
            Title = enemy.Name,
            // No portrait: a hero's face on an authored monster is a lie in a different channel
            // (see EnemyCard). Initials stand in until the role crests exist.
            PortraitFallback = Initials(enemy.Name),
            Accent = string.IsNullOrEmpty(enemy.Accent) ? "utility" : enemy.Accent,
            WeaponName = enemy.WeaponName,
            WeaponSummary = attack,
            PassiveTrigger = "BEHAVIOR",
            PassiveName = "HOW IT FIGHTS",
            PassiveSummary = enemy.Behavior,
            Stats = stats,
        };
        inspector.UnitSheet = EnemyUnitSheet(
            stats, enemy.WeaponName, enemy.Behavior);
        return inspector;
    }

    private void BuildRunOver()
    {
        var o = _model.RunOver;
        var s = _run.State;
        bool won = s.Victory;
        o.Tone = won ? RunOverTone.Victory : RunOverTone.Defeat;
        if (s.EndlessDefeat)
        {
            o.Heading = "THE HOUR FINALLY BROKE";
            o.Summary =
                $"The authored victory stands. Beyond the Hour ended in cycle " +
                $"{s.EndlessCycles + 1}, beat {s.EndlessBeat + 1}.";
            o.Stats = new List<StatChipModel>
            {
                new StatChipModel("CYCLES", s.EndlessCycles.ToString(), "good"),
                new StatChipModel("BEAT REACHED",
                    $"{s.EndlessBeat + 1} / {_cfg.EndlessFightsPerCycle + 1}"),
                new StatChipModel("CROWNS", s.BossWins.ToString()),
                new StatChipModel("WARBAND", s.Field.Count.ToString()),
            };
        }
        else
        {
            o.Heading = won ? "THE HOUR HELD" : "THE HOUR BROKE";
            o.Summary = won
                ? "Every act answered. The warband walks out of the Tower intact."
                : $"The warband fell in act {s.Act}. The Hourstone fractures, and the run ends here.";
            o.Stats = new List<StatChipModel>
            {
                new StatChipModel("ACT", $"{s.Act} / {_cfg.Acts}", won ? "good" : "bad"),
                new StatChipModel("BOSSES", s.BossWins.ToString()),
                new StatChipModel("SAND", s.Sand.ToString()),
                new StatChipModel("WARBAND", s.Field.Count.ToString()),
            };
        }
        o.FinalWarband = s.Field.Select(HeroCard).ToList();
    }

    // ---- hydration helpers -------------------------------------------------------

    private InspectorModel PlaybackInspector(PlaybackUnit unit)
    {
        bool enemy = !string.IsNullOrWhiteSpace(unit.RoleId);
        string id = enemy ? "" : unit.ChassisId ?? "";
        PresentationCatalog.UnitPresentation presentation =
            enemy ? null : _presentation.Unit(id);
        string role = enemy
            ? Enemies.RoleLabel(unit.RoleId)
            : string.IsNullOrEmpty(id) ? "COMBATANT" : presentation.role;

        var coreFacts = new List<StatChipModel>
        {
            new StatChipModel("HP", $"{unit.Hp} / {unit.MaxHp}",
                unit.Hp * 3 <= unit.MaxHp ? "bad" : "",
                PresentationFactId.Hp, "Current and maximum Health."),
        };
        if (unit.Shield > 0)
            coreFacts.Add(new StatChipModel("SHIELD", unit.Shield.ToString(), "good",
                PresentationFactId.Protection, "Damage this Shield can still absorb."));
        if (unit.ManaMax > 0)
            coreFacts.Add(new StatChipModel("MANA", $"{unit.Mana} / {unit.ManaMax}", "",
                PresentationFactId.ManaThreshold,
                "Current Mana and the amount required to cast the Signature."));

        var weaponFacts = new List<StatChipModel>();
        if (unit.Attack > 0)
        {
            weaponFacts.Add(new StatChipModel(
                unit.HealAutos ? "HEAL" : "POWER", unit.Attack.ToString(),
                unit.HealAutos ? "good" : "",
                unit.HealAutos
                    ? PresentationFactId.Restoration
                    : PresentationFactId.BasicPower,
                unit.HealAutos ? "Healing per basic swing." : "Damage per basic swing."));
            weaponFacts.Add(new StatChipModel(
                "CADENCE", $"{unit.AttackInterval / 10f:0.0}s", "",
                PresentationFactId.Cadence, "Time between basic attacks."));
            weaponFacts.Add(new StatChipModel(
                "REACH", unit.Range.ToString(), unit.Range >= 3 ? "good" : "",
                PresentationFactId.Reach, "Maximum basic attack reach in hexes."));
            if (unit.ManaPerSwing > 0)
                weaponFacts.Add(new StatChipModel(
                    "MANA/HIT", unit.ManaPerSwing.ToString(), "",
                    PresentationFactId.ManaPerSwing,
                    "Mana gained whenever a basic swing completes."));
        }
        if (unit.CritChance > 0)
            weaponFacts.Add(new StatChipModel("CRIT", $"{unit.CritChance}%", "warn",
                PresentationFactId.CritChance, "Critical-hit chance."));
        if (unit.CleavePct > 0)
            weaponFacts.Add(new StatChipModel("CLEAVE", $"{unit.CleavePct}%", "",
                PresentationFactId.Cleave,
                "Share of basic damage dealt to enemies adjacent to the target."));

        var notes = new List<string>();
        var statuses = new List<UnitStatusModel>();
        int currentTick = _player?.CurrentTick ?? 0;
        foreach (var status in unit.Statuses)
        {
            var lex = Lexicon.Of(status.Kind);
            string magnitude = status.Mag == 0 ? "" : $" {status.Mag}";
            string remaining = status.ExpiryTick > currentTick
                ? $" · {(status.ExpiryTick - currentTick) / 10f:0.0}s"
                : "";
            string tooltip = status.ExpiryTick > currentTick
                ? $"{lex.Text} {(status.ExpiryTick - currentTick) / 10f:0.0}s remaining."
                : lex.Text;
            statuses.Add(new UnitStatusModel
            {
                Label = $"{lex.Name.ToUpperInvariant()}{magnitude}{remaining}",
                Tooltip = tooltip,
            });
            notes.Add($"{lex.Name.ToUpperInvariant()}{magnitude} · {lex.Text}");
        }

        var specs = new List<RankTierSlotModel>();
        ChampionRuleProjection championRules = null;
        if (!enemy)
        {
            ChassisDef chassis = _content.Chassis(unit.ChassisId);
            var selectedNodes = new List<SpecNode>();
            UnitDef before = Loadout.Compose(chassis).Def;
            foreach (string value in unit.Traits)
            {
                if (!ContentLexicon.Nodes.ContainsKey(value)) continue;
                SpecNode node = _content.Node(value);
                selectedNodes.Add(node);
                UnitDef after = Loadout.Compose(chassis, nodes: selectedNodes).Def;
                SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
                    unit.ChassisId, value, before, after);
                specs.Add(new RankTierSlotModel
                {
                    Rank = rule.Rank.ToString(),
                    Icon = SpecGlyph(rule.Kind),
                    Name = rule.Name,
                    Summary = rule.Choice,
                    Rule = rule.Full,
                    Accent = SpecAccent(rule.Kind),
                    State = RankTierSlotState.Selected,
                });
                before = after;
            }
            championRules = PlayerRuleProjection.Champion(before);
            notes.AddRange(PlayerRuleProjection.Keywords(before));
        }

        var sheet = new UnitSheetModel
        {
            Combat = true,
            Enemy = enemy,
            CoreFacts = coreFacts,
            WeaponName = string.IsNullOrWhiteSpace(unit.WeaponName)
                ? "NO BASIC ATTACK"
                : unit.WeaponName,
            WeaponFacts = weaponFacts,
            Specs = specs,
            Targeting = TargetingLine(unit),
            Statuses = statuses,
            PassivesLabel = enemy ? "BEHAVIOR" : "PASSIVES",
        };
        RuleDeltaModel property = BuildWeaponProperty(unit);
        if (property != null) sheet.WeaponProperties.Add(property);
        if (!enemy && unit.ManaMax > 0 &&
            !string.IsNullOrWhiteSpace(championRules.SignatureText))
            sheet.Signature = UnitRule(
                "SIGNATURE", presentation.abilityIcon, championRules.SignatureName,
                championRules.SignatureText, unit.ManaMax);
        if (enemy)
        {
            string behavior = Enemies.Behavior(unit.Name);
            if (!string.IsNullOrWhiteSpace(behavior))
                sheet.Passives.Add(UnitRule(
                    "BEHAVIOR", EnemyRoleGlyph(unit.RoleId), "HOW IT FIGHTS", behavior));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(championRules.PassiveText))
                sheet.Passives.Add(UnitRule(
                    "PASSIVE", presentation.passiveIcon,
                    championRules.PassiveName, championRules.PassiveText));
        }

        var allFacts = new List<StatChipModel>(coreFacts);
        allFacts.AddRange(weaponFacts);
        return new InspectorModel
        {
            Kind = DecisionDetailKind.Combatant,
            Eyebrow = (unit.Team == 0 ? "ALLY" : "ENEMY") + " · " +
                      role.ToUpperInvariant(),
            Title = string.IsNullOrEmpty(unit.Name) ? "Unknown Combatant" : unit.Name,
            Subtitle = unit.Dead ? "DEFEATED" : "",
            PortraitResource = presentation?.portrait ?? "",
            PortraitFallback = enemy ? EnemyRoleGlyph(unit.RoleId) : Initials(unit.Name),
            Accent = enemy ? Enemies.RoleAccent(unit.RoleId) : presentation.accent,
            WeaponName = unit.WeaponName,
            WeaponSummary = BasicAttackSummary(unit),
            Stats = allFacts,
            Targeting = TargetingLine(unit),
            // A selected node's rank comes from its authored offer row, not mutable run state.
            PathTiers = specs,
            KeywordNotes = notes,
            UnitSheet = sheet,
        };
    }

    private static string EnemyRoleGlyph(string roleId) =>
        roleId switch
        {
            Enemies.Swarm => "✣",
            Enemies.Anchor => "⬢",
            Enemies.Artillery => "◎",
            Enemies.Ritualist => "Ω",
            Enemies.Diver => "◆",
            Enemies.Siege => "✹",
            Enemies.Hour => "◈",
            _ => "✖",
        };

    /// <summary>
    /// The targeting rule as one display line. The label is the subject ("TARGETS"), so the value
    /// needs no verb — the old copy read "Acquires the FARTHEST enemy, holds 5 hexes", which spent
    /// four words on grammar and left "holds 5 hexes" ambiguous between holding THEM and holding
    /// AT that distance.
    /// </summary>
    private static string TargetingLine(PlaybackUnit unit)
    {
        string acquire = unit.TargetPref switch
        {
            TargetPref.Farthest => "Farthest",
            TargetPref.LowestHp => "Weakest",
            TargetPref.HighestHp => "Strongest",
            _ => "Nearest",
        };
        return unit.Standoff > 0
            ? $"{acquire}, held at {unit.Standoff} hexes"
            : acquire;
    }

    private HeroCardModel ChassisCard(string chassisId)
    {
        var lex = ContentLexicon.Chassis(chassisId);
        var presentation = _presentation.Unit(chassisId);
        var def = Loadout.Compose(_content.Chassis(chassisId)).Def;
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
        return new HeroCardModel
        {
            Id = chassisId,
            Name = lex.Name,
            Role = presentation.role,
            Description = lex.Text,
            WeaponName = def.WeaponName,
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(lex.Name),
            RoleIcon = presentation.roleIcon,
            Accent = presentation.accent,
            AbilityIcon = presentation.abilityIcon,
            AbilityTrigger = SignatureTrigger(def),
            AbilityName = rules.SignatureName,
            AbilitySummary = rules.SignatureText,
            PassiveIcon = presentation.passiveIcon,
            PassiveTrigger = "PASSIVE",
            PassiveName = rules.PassiveName,
            PassiveSummary = rules.PassiveText,
            WeaponSummary = BasicAttackSummary(def),
            KeywordNotes = PlayerRuleProjection.Keywords(def).ToList(),
            Stats = StatChips(def),
        };
    }

    private HeroCardModel HeroCard(HeroInstance hero)
    {
        var card = ChassisCard(hero.ChassisId);
        card.RankLabel = $"RANK {hero.Rank}";
        card.Traits = hero.SpecNodeIds.Select(id => ContentLexicon.Node(id).Name).ToList();

        var def = ComposeHero(hero);
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
        card.WeaponName = def.WeaponName;
        card.WeaponSummary = BasicAttackSummary(def);
        card.AbilityTrigger = SignatureTrigger(def);
        card.AbilityName = rules.SignatureName;
        card.AbilitySummary = rules.SignatureText;
        card.PassiveName = rules.PassiveName;
        card.PassiveSummary = rules.PassiveText;
        card.Stats = StatChips(def);
        return card;
    }

    /// <summary>
    /// A roster hero with its shop affordances resolved. Legality is decided HERE so the view can
    /// simply not render an action that would throw — a button that always errors is worse than
    /// no button.
    /// </summary>
    private HeroCardModel RosterCard(HeroInstance hero, int index, bool inBench)
    {
        var card = HeroCard(hero);
        var s = _run.State;
        card.Index = index;
        card.InBench = inBench;

        bool actionable = s.Phase == RunPhase.Shop && s.PendingSpec == null;
        card.CanEquip = actionable && _selectedItem >= 0;
        card.CanUnequip = actionable && hero.WeaponId != null;

        bool canTemper = hero.WeaponTier < _cfg.TierCeiling(s.Act);
        int reforgeCost = canTemper ? _cfg.ReforgeCosts[(int)hero.WeaponTier] : 0;
        card.CanReforge = actionable && canTemper && s.Gold >= reforgeCost;
        card.ReforgeLabel = canTemper ? $"REFORGE {reforgeCost}" : "";

        if (actionable)
        {
            bool roomOnField = s.Field.Count < s.FieldSlots;
            bool roomOnBench = s.Bench.Count < _cfg.BenchSlots;
            if (inBench && roomOnField) card.MoveLabel = "TO FIELD";
            else if (!inBench && roomOnBench) card.MoveLabel = "TO BENCH";
            card.SellLabel = $"SELL {hero.GoldSpent * _cfg.SellPct / 100}";
        }
        return card;
    }

    private static string WeaponLine(WeaponDef w, WeaponTier tier)
    {
        string temper = tier == WeaponTier.Worn ? "" : $"{tier} · ";
        return $"{temper}{w.Damage} dmg · reach {w.Range} · {w.Interval / 10f:0.0}s";
    }

    /// <summary>The hero as the sim will actually see them — the one definition, so a deployment
    /// preview and a shop card can never disagree about what a unit is.</summary>
    private UnitDef ComposeHero(HeroInstance hero)
    {
        var chassis = _content.Chassis(hero.ChassisId);
        var weapon = hero.WeaponId == null ? chassis.StarterWeapon : _content.Weapon(hero.WeaponId);
        return Loadout.Compose(
            chassis, weapon,
            hero.TrinketIds.Select(_content.Trinket),
            hero.SpecNodeIds.Select(_content.Node),
            tier: hero.WeaponTier,
            mastered: chassis.Specializations.Contains(weapon.Category),
            rankSteps: (int)hero.Rank).Def;
    }

    /// <summary>
    /// The four numbers that actually decide a placement. Attack interval is inverted into a
    /// "speed" reading because "swings every 1.0s" is a fact a player can use, while "interval
    /// 10" is an implementation detail.
    /// </summary>
    private static List<StatChipModel> StatChips(UnitDef def)
    {
        int? baselineHits = MechanicalRulePresenter.BasicAttacksToSignature(def);
        var chips = new List<StatChipModel>
        {
            new StatChipModel("HP", def.MaxHp.ToString(), "",
                PresentationFactId.Hp, "Maximum combat HP."),
            new StatChipModel(def.HealAutos ? "HEAL" : "POWER", def.Attack.ToString(),
                "", def.HealAutos
                    ? PresentationFactId.Restoration
                    : PresentationFactId.BasicPower,
                def.HealAutos ? "Healing per basic swing." : "Damage per basic swing."),
            new StatChipModel("REACH", def.Range.ToString(), "",
                PresentationFactId.Reach, "Maximum basic attack reach in hexes."),
            new StatChipModel("CADENCE", $"{def.AttackInterval / 10f:0.0}s", "",
                PresentationFactId.Cadence, "Time between basic attacks."),
            new StatChipModel("MANA/SWING", def.ManaPerSwing.ToString(), "",
                PresentationFactId.ManaPerSwing,
                "Mana gained whenever a basic swing completes."),
            new StatChipModel("SIGNATURE", def.ManaMax.ToString(), "",
                PresentationFactId.ManaThreshold, "Mana required to cast the signature."),
        };
        if (def.ManaMax > 0 && def.ManaPerSwing > 0 && baselineHits.HasValue)
        {
            string calculation =
                $"{def.ManaMax} Mana ÷ {def.ManaPerSwing} Mana per basic hit = " +
                $"{baselineHits.Value} baseline hits before modifiers.";
            chips[4].AdvancedTooltip = calculation;
            chips[5].AdvancedTooltip = calculation;
        }
        if (def.CritChance > 0)
            chips.Add(new StatChipModel("CRIT", $"{def.CritChance}%", "",
                PresentationFactId.CritChance, "Critical-hit chance."));
        if (def.CleavePct > 0)
            chips.Add(new StatChipModel("CLEAVE", $"{def.CleavePct}%", "",
                PresentationFactId.Cleave,
                "Share of basic damage dealt to enemies adjacent to the target."));
        return chips;
    }

    private string OfferName(ShopOffer o)
    {
        switch (o.Kind)
        {
            case OfferKind.Hero: return ContentLexicon.Chassis(o.Id).Name;
            case OfferKind.Weapon: return _content.Weapon(o.Id).Name;
            case OfferKind.Trinket: return _content.Trinket(o.Id).Name;
            default: return _content.Inscription(o.Id).Name;
        }
    }

    private string OfferDetail(ShopOffer o)
    {
        switch (o.Kind)
        {
            case OfferKind.Hero:
                return ContentLexicon.Chassis(o.Id).Text;
            case OfferKind.Weapon:
            {
                var w = _content.Weapon(o.Id);
                string temper = o.Tier == WeaponTier.Worn ? "" : $"{o.Tier} · ";
                return $"{temper}{w.Damage} dmg · reach {w.Range} · {w.Interval / 10f:0.0}s";
            }
            case OfferKind.Trinket:
                return "Trinket";
            default:
                return "A rule for the whole warband, for the whole run.";
        }
    }

#if UNITY_EDITOR
    // Stable Play Mode seams for MCP verification. These call the same intents as the UI; they
    // do not reach into RunController behind the shell's back.
    /// <summary>
    /// Editor automation can observe the shell GameObject before Unity invokes Start, especially
    /// while Boot hands off to Game. Fixture tools must wait for the model dependencies and
    /// retained view tree rather than calling Rebuild on that half-created component.
    /// </summary>
    public bool EditorReadyForFixtures =>
        _content != null && _cfg != null && _root != null && _views.Count > 0;

    public bool EditorEnsureReadyForFixtures()
    {
        if (!_started) Start();
        return EditorReadyForFixtures;
    }

    public void EditorNewRun() => _actions.NewRun?.Invoke();

    /// <summary>
    /// Bind the five-card opening offer directly to the retained Recruit view. The fixed roster
    /// matches the screenshot-review fixture and never mutates a player run or autosave.
    /// </summary>


        /// <summary>
    /// Bind a named presentation fixture directly to the retained Workbench and permanent rail.
    /// This is intentionally isolated from RunController and Autosave: visual QA must be
    /// deterministic without manufacturing impossible authoritative run state.
    /// </summary>
    public bool EditorLoadWorkbenchFixture(
        string id, bool expandedText = false, bool reducedMotion = false)
    {
        if (_root == null || _views == null) return false;
        _optionsPanel?.Close();   // a leftover open modal must not haunt the next capture
        WorkbenchFixtures.Fixture fixture =
            WorkbenchFixtures.Build(id, expandedText, reducedMotion);
        WorkbenchView workbench = null;
        foreach (IRunScreenView view in _views)
        {
            bool isWorkbench = view is WorkbenchView;
            view.Root.style.display = isWorkbench ? DisplayStyle.Flex : DisplayStyle.None;
            if (isWorkbench) workbench = (WorkbenchView)view;
        }
        if (workbench == null) return false;

        if (!ReferenceEquals(_activeView, workbench))
        {
            if (_activeView is IRunScreenLifecycle leaving) leaving.OnScreenExited();
            _activeView = workbench;
            workbench.OnScreenEntered();
        }
        _root.AddToClassList("ui-fixture-mode");
        _runtimeTooltips?.Hide();
        ApplyShellLayoutClasses();
        workbench.Bind(fixture.Shell);
        _warbandBarView?.Bind(fixture.Shell.WarbandBar);
        // Item 5b: QA fixtures carry a representative seven-law tray (one past the collapsed
        // cap) so every capture exercises the persistent chrome it must not collide with.
        SyncInscriptionRailFromIds(new[]
            { "firstblood", "leapstun", "brand", "bronzehour", "chorus", "thirdchime", "stilledbell" });

        if (_fightOverlay != null) _fightOverlay.style.display = DisplayStyle.None;
        _hallEnvironment?.SetVisible(true, HallStation.Market, reducedMotion);
        Debug.Log($"[UI QA] Loaded Workbench fixture '{fixture.Id}'" +
                  (expandedText ? " with 130% copy stress." : "."));
        return true;
    }

    /// <summary>
    /// Reproduces the live failure reported on 2026-07-27: buy a Sharpshot fork rank while a
    /// second Sharpshot remains in Market stock, then project that second card during the blocking
    /// B-rank choice. The card must wait for PathId instead of querying "sharpshot|A|-".
    /// This uses a temporary controller and never saves or mutates the player's run.
    /// </summary>
    public string EditorVerifyPendingForkMarketRebuild()
    {
        RunController previousRun = _run;
        int previousOffer = _selectedMarketOffer;
        string previousCard = _selectedCardKey;
        try
        {
            var hero = new HeroInstance { ChassisId = "sharpshot" };
            var temporary = new RunController(0x5A17UL, _content, new[] { hero }, _cfg);
            while (temporary.State.ShopOffers.Count < 2)
                temporary.State.ShopOffers.Add(null);
            temporary.State.Sand = 100;
            temporary.State.ShopOffers[0] = new ShopOffer
            {
                Kind = OfferKind.Hero,
                Id = "sharpshot",
                Price = 1,
            };
            temporary.State.ShopOffers[1] = new ShopOffer
            {
                Kind = OfferKind.Hero,
                Id = "sharpshot",
                Price = 1,
            };

            PurchaseResult purchase = temporary.BuyOffer(0);
            _run = temporary;
            _selectedMarketOffer = 1;
            _selectedCardKey = "market:1";
            CardModel projected = MarketCard(temporary.State.ShopOffers[1], 1);
            bool passed =
                purchase.Outcome == PurchaseOutcome.RankUp &&
                temporary.State.PendingSpec != null &&
                temporary.State.Field[0].Rank == Rank.B &&
                temporary.State.Field[0].PathId == null &&
                projected.Disabled &&
                string.Equals(
                    projected.AbilityName,
                    "SPECIALIZATION PENDING",
                    StringComparison.Ordinal);
            return passed
                ? "PASS · pending B fork safely defers the following A-rank Market preview"
                : "FAIL · pending fork projection did not resolve to the waiting card";
        }
        finally
        {
            _run = previousRun;
            _selectedMarketOffer = previousOffer;
            _selectedCardKey = previousCard;
        }
    }

    public bool EditorLoadWagerFixture(
        bool expandedText = false, bool reducedMotion = false)
    {
        _optionsPanel?.Close();   // a leftover open modal must not haunt the next capture
        WorkbenchFixtures.Fixture fixture =
            WorkbenchFixtures.Build("market-recruit", expandedText, reducedMotion);
        RunShellModel shell = fixture.Shell;
        shell.Screen = RunScreen.Wager;
        shell.Wager = new WagerModel
        {
            Act = "ACT II",
            Beat = "HOUR 6 OF 9",
            Sand = "31",
            Heading = "NAME YOUR WAGER",
            Brief = expandedText
                ? "Choose the pressure this warband will face. The opposing formation remains " +
                  "sealed until commitment, while the complete reward curve stays public."
                : "Choose the pressure this warband will face. The formation is revealed after commitment.",
            Disclosure = expandedText
                ? "The next opponent is hidden until lock-in. Risk and victory reward remain " +
                  "fully disclosed and do not change after your choice."
                : "Opponent composition is revealed after lock-in. Rewards do not change.",
            ContinueLabel = "LOCK IN WAGER",
            CanContinue = true,
            Track = EditorFixtureTrack(),
            Risks = new List<TierChoiceModel>
            {
                new TierChoiceModel
                {
                    Index = 0, Name = "FRAYING", Risk = "Lower enemy pressure",
                    CurrencyReward = 4,
                },
                new TierChoiceModel
                {
                    Index = 1, Name = "CONTESTED", Risk = "Matched enemy pressure",
                    CurrencyReward = 7, Selected = true,
                },
                new TierChoiceModel
                {
                    Index = 2, Name = "DIRE", Risk = "Greater enemy pressure",
                    CurrencyReward = 11,
                },
            },
        };
        shell.WarbandBar.Mode = WarbandBarMode.WagerReadOnly;
        shell.WarbandBar.Compact = true;
        shell.WarbandBar.CanEdit = false;
        return EditorBindFixtureView<WagerView>(shell, HallStation.Breach, reducedMotion);
    }

    public bool EditorLoadDeployFixture(
        bool expandedText = false, bool reducedMotion = false)
    {
        _optionsPanel?.Close();   // a leftover open modal must not haunt the next capture
        WorkbenchFixtures.Fixture fixture =
            WorkbenchFixtures.Build("rail-full", expandedText, reducedMotion);
        RunShellModel shell = fixture.Shell;
        shell.Screen = RunScreen.Deploy;
        shell.Deploy = new DeployModel
        {
            Heading = "DEPLOY — CONTESTED BREACH",
            Instruction = expandedText
                ? "Select a champion in the permanent rail, then choose a legal hex in your half. " +
                  "Selecting an occupied hex swaps the formation."
                : "Select a champion, then choose a legal hex in your half.",
            Placed = 3,
            Total = 3,
            CanCommit = true,
            PrimaryText = "LOCK IN",
            Enemies = new List<DeployEnemyRowModel>
            {
                new DeployEnemyRowModel
                {
                    Key = "enemy:0", Name = "Ash Warden", Role = "Anchor", Row = 0,
                    MaxHp = 188, Attack = 22, Range = 1, AttackIntervalTicks = 14,
                    WeaponName = "Greataxe", Accent = "ward",
                    Behavior = "Holds the line and soaks. Walk around it or go through it.",
                },
                new DeployEnemyRowModel
                {
                    Key = "enemy:1", Name = "Dune Reaver", Role = "Diver", Row = 1,
                    MaxHp = 132, Attack = 30, Range = 1, AttackIntervalTicks = 9,
                    WeaponName = "Twin Daggers", Accent = "power",
                    Behavior = "Leaps at your farthest unit the moment the fight begins.",
                    Selected = expandedText,
                },
                new DeployEnemyRowModel
                {
                    Key = "enemy:2", Name = "Glass Seer", Role = "Artillery", Row = 3,
                    MaxHp = 108, Attack = 28, Range = 4, AttackIntervalTicks = 15,
                    WeaponName = "Matchlock Musket", Accent = "utility",
                    Behavior = "Fires past your front line and gives ground to hold that reach.",
                },
            },
            EncounterRuleName = "The Waning",
            EncounterRule = expandedText
                ? "Begins after 45 seconds; both formations take increasing damage until the fight ends."
                : "Begins after 45 seconds.",
        };
        // The expanded-text pass is also the one that opens the unit card, so the capture
        // matrix covers both the row list and the card it opens.
        foreach (DeployEnemyRowModel enemy in shell.Deploy.Enemies)
            if (enemy.Selected) { shell.Deploy.SelectedEnemy = EnemyInspector(enemy); break; }
        shell.WarbandBar.Mode = WarbandBarMode.DeploymentSelect;
        shell.WarbandBar.Compact = false;
        shell.WarbandBar.CanEdit = false;
        shell.WarbandBar.CanManage = false;
        return EditorBindFixtureView<DeployView>(shell, HallStation.Breach, reducedMotion);
    }

    public bool EditorLoadResultFixture(
        bool expandedText = false, bool reducedMotion = false)
    {
        _optionsPanel?.Close();   // a leftover open modal must not haunt the next capture
        WorkbenchFixtures.Fixture fixture =
            WorkbenchFixtures.Build("rail-full", expandedText, reducedMotion);
        RunShellModel shell = fixture.Shell;
        shell.Screen = RunScreen.Fight;
        shell.Result = new ResultGateModel
        {
            Open = true,
            Victory = true,
            Eyebrow = "BREACH SECURED",
            Heading = "VICTORY",
            Summary = expandedText
                ? "The warband held through the final exchange and felled all five enemies " +
                  "without yielding the Hourstone."
                : "Five enemies felled. The Hourstone remains bound.",
            ContinueLabel = "RETURN TO THE WORKBENCH",
            Recommendation = "The Market has new stock.",
            CanWatchAgain = true,
            Stats = new List<ResultStatModel>
            {
                new ResultStatModel { Label = "ENEMIES FELLED", Value = "5 / 5", Tone = "good" },
                new ResultStatModel { Label = "HOURSTONE", Value = "+7", Tone = "sand" },
                new ResultStatModel { Label = "SURVIVORS", Value = "3 / 3", Tone = "good" },
            },
            // Deaths is deliberately EMPTY, matching the shipping path: the recap's timeline plus
            // its "Lost:" caption replaced those three labels. Leaving them here made the QA
            // capture render a screen the game no longer produces — and it overflowed the panel.
            Deaths = new List<string>(),
            // Without this the recap panel hides itself and the QA capture passes vacuously —
            // a green "result gate is fine" over a surface whose three charts never rendered.
            Recap = CombatRecapPanel.EditorFixture(),
        };
        shell.WarbandBar.Mode = WarbandBarMode.ResultReadOnly;
        shell.WarbandBar.Compact = true;
        shell.WarbandBar.CanEdit = false;
        shell.WarbandBar.CanManage = false;
        foreach (IRunScreenView view in _views)
            view.Root.style.display = DisplayStyle.None;
        if (_activeView is IRunScreenLifecycle leaving) leaving.OnScreenExited();
        _activeView = null;
        _root.AddToClassList("ui-fixture-mode");
        _runtimeTooltips?.Hide();
        _reducedMotion = reducedMotion;
        ApplyShellLayoutClasses();
        if (_fightOverlay != null) _fightOverlay.style.display = DisplayStyle.Flex;
        if (_fightHitSurface != null) _fightHitSurface.style.display = DisplayStyle.None;
        if (_fightHint != null) _fightHint.style.display = DisplayStyle.None;
        if (_fightSkip != null) _fightSkip.style.display = DisplayStyle.None;
        _resultGateView?.Bind(shell.Result, reducedMotion);
        _warbandBarView?.Bind(shell.WarbandBar);
        // Item 5b: QA fixtures carry a representative seven-law tray (one past the collapsed
        // cap) so every capture exercises the persistent chrome it must not collide with.
        SyncInscriptionRailFromIds(new[]
            { "firstblood", "leapstun", "brand", "bronzehour", "chorus", "thirdchime", "stilledbell" });

        _hallEnvironment?.SetVisible(false, HallStation.Breach, reducedMotion);
        return _resultGateView != null;
    }

    /// <summary>
    /// Deterministic combat-sheet fixture. It binds the same PlaybackInspector used by the live
    /// 150 ms refresh path, while keeping the player fold untouched. The stress variant appends
    /// another Weapon fact, property, and Passive through UnitSheetModel to prove variable-arity
    /// content needs no renderer branch.
    /// </summary>
    public bool EditorLoadCombatInspectorFixture(
        string id, bool expandedText = false, bool reducedMotion = false)
    {
        if (_fightInspector == null || _fightCard == null || _fightOverlay == null) return false;
        _optionsPanel?.Close();
        foreach (IRunScreenView view in _views)
            view.Root.style.display = DisplayStyle.None;
        if (_activeView is IRunScreenLifecycle leaving) leaving.OnScreenExited();
        _activeView = null;
        _model.Screen = RunScreen.Fight;
        _root.AddToClassList("ui-fixture-mode");
        _runtimeTooltips?.Hide();
        _reducedMotion = reducedMotion;
        ApplyShellLayoutClasses();
        _hallEnvironment?.SetVisible(false, HallStation.Breach, reducedMotion);

        bool enemy = string.Equals(id, "enemy", StringComparison.OrdinalIgnoreCase);
        PlaybackUnit unit;
        if (enemy)
        {
            UnitDef def = Enemies.Gunner();
            def.RoleId = Enemies.Artillery;
            unit = PlaybackUnit.From(UnitState.Spawn(
                90, 1, def, Hex.FromRowCol(3, 5)));
            unit.Hp = 61;
            unit.Statuses.Add((StatusKind.Burn, 4, 43));
        }
        else
        {
            ComposedLoadout loadout = Loadout.Compose(
                _content.Chassis("phalanx"),
                nodes: new[]
                {
                    _content.Node("phalanx.pikewall"),
                    _content.Node("phalanx.pikewall.spearpoint"),
                    _content.Node("phalanx.pikewall.unbrokenline"),
                },
                mastered: true,
                rankSteps: 3);
            unit = PlaybackUnit.From(UnitState.Spawn(
                40, 0, loadout.Def, Hex.FromRowCol(4, 2)));
            unit.Hp = 164;
            unit.Shield = 28;
            unit.Mana = 24;
            unit.Statuses.Add((StatusKind.CounterCharge, 1, -1));
            unit.Statuses.Add((StatusKind.Haste, 200, 51));
        }

        InspectorModel inspector = PlaybackInspector(unit);
        if (expandedText && inspector.UnitSheet != null)
        {
            inspector.UnitSheet.WeaponFacts.Add(new StatChipModel(
                "IMPACT", "2", "warn", PresentationFactId.Unknown,
                "Representative future Weapon fact appended by the adapter."));
            inspector.UnitSheet.WeaponProperties.Add(new RuleDeltaModel
            {
                RuleName = "FORMATION EDGE",
                DisplayName = "FORMATION",
                ShortSummary = "At exact reach, deal 50% bonus damage.",
                FullDescription =
                    "At exact Weapon reach, basic attacks deal 50% bonus damage.",
                Applies = true,
            });
            inspector.UnitSheet.Passives.Add(UnitRule(
                "PASSIVE", "⬢", "DISCIPLINED ADVANCE",
                "After moving, the next counterattack also grants 8 Shield. This deliberately " +
                "long stress rule proves another authored Passive wraps without clipping."));
        }

        _fightInspectedUnit = null; // keep the scheduled live refresh from replacing the fixture
        _fightInspector.Bind(inspector);
        _fightOverlay.style.display = DisplayStyle.Flex;
        if (_fightHitSurface != null) _fightHitSurface.style.display = DisplayStyle.None;
        if (_fightHint != null) _fightHint.style.display = DisplayStyle.None;
        if (_fightSkip != null) _fightSkip.style.display = DisplayStyle.None;
        if (_fightOptions != null) _fightOptions.style.display = DisplayStyle.None;
        if (_revisionCombatOverlay != null)
            _revisionCombatOverlay.Root.style.display = DisplayStyle.None;
        if (_resultGateView != null)
            _resultGateView.Root.style.display = DisplayStyle.None;
        _fightCardRing.style.display = DisplayStyle.None;
        _fightCard.style.display = DisplayStyle.Flex;
        _fightCard.style.left = StyleKeyword.Auto;
        _fightCard.style.right = 24f;
        _fightCard.style.top = 64f;
        return true;
    }

    private bool EditorBindFixtureView<T>(
        RunShellModel shell, HallStation station, bool reducedMotion)
        where T : class, IRunScreenView
    {
        T target = null;
        foreach (IRunScreenView view in _views)
        {
            bool active = view is T;
            view.Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (active) target = (T)view;
        }
        if (target == null) return false;
        if (!ReferenceEquals(_activeView, target))
        {
            if (_activeView is IRunScreenLifecycle leaving) leaving.OnScreenExited();
            _activeView = target;
            if (_activeView is IRunScreenLifecycle entering) entering.OnScreenEntered();
        }
        _root.AddToClassList("ui-fixture-mode");
        _runtimeTooltips?.Hide();
        _reducedMotion = reducedMotion;
        ApplyShellLayoutClasses();
        target.Bind(shell);
        _warbandBarView?.Bind(shell.WarbandBar);
        // Item 5b: QA fixtures carry a representative seven-law tray (one past the collapsed
        // cap) so every capture exercises the persistent chrome it must not collide with.
        SyncInscriptionRailFromIds(new[]
            { "firstblood", "leapstun", "brand", "bronzehour", "chorus", "thirdchime", "stilledbell" });

        if (_fightOverlay != null) _fightOverlay.style.display = DisplayStyle.None;
        _hallEnvironment?.SetVisible(true, station, reducedMotion);
        return true;
    }

    private static List<PlanningTrackNodeModel> EditorFixtureTrack() =>
        new List<PlanningTrackNodeModel>
        {
            new PlanningTrackNodeModel { Label = "I", Kind = "Fight", State = "past" },
            new PlanningTrackNodeModel { Label = "II", Kind = "Fight", State = "past" },
            new PlanningTrackNodeModel { Label = "III", Kind = "Fight", State = "current" },
            new PlanningTrackNodeModel { Label = "IV", Kind = "Interlude", State = "future" },
            new PlanningTrackNodeModel { Label = "V", Kind = "Boss", State = "future" },
        };

    public void EditorClearWorkbenchFixture()
    {
        if (_root == null) return;
        _root.RemoveFromClassList("ui-fixture-mode");
        _runtimeTooltips?.Hide();
        Rebuild();
    }

    public string[] EditorWorkbenchFixtureIds() =>
        (string[])WorkbenchFixtures.Ids.Clone();

    public string EditorWorkbenchLayoutReport()
    {
        string workbenchReport = "Workbench: FAIL · view is missing";
        foreach (IRunScreenView view in _views)
            if (view is WorkbenchView workbench)
            {
                workbenchReport = workbench.EditorResolvedLayoutReport();
                break;
            }
        string tooltipReport = _runtimeTooltips?.EditorResolvedLayoutReport() ??
                               "Runtime tooltip: FAIL · service is missing";
        string railReport = _warbandBarView?.EditorResolvedLayoutReport() ??
                            "Permanent warband rail: FAIL · view is missing";
        string rosterReport = _warbandBarView?.EditorRosterInteractionReport() ??
                              "Warband roster interaction: FAIL · view is missing";
        bool passed = !workbenchReport.Contains(": FAIL") &&
                      !tooltipReport.Contains(": FAIL") &&
                      !railReport.Contains(": FAIL") &&
                      !rosterReport.Contains(": FAIL");
        return $"Workbench QA: {(passed ? "PASS" : "FAIL")} · " +
               $"{workbenchReport}; {tooltipReport}; {railReport}; {rosterReport}";
    }

    public bool EditorSetWorkbenchChoiceCardMinHeight(float pixels)
    {
        foreach (IRunScreenView view in _views)
            if (view is WorkbenchView workbench)
            {
                workbench.EditorSetChoiceCardMinHeight(pixels);
                return true;
            }
        return false;
    }

    public string EditorWorkbenchEquipmentDragReport()
    {
        foreach (IRunScreenView view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorEquipmentDragReport();
        return "Equipment drag: FAIL · Workbench view is missing";
    }

    public bool EditorToggleRecruit(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= _offer.Count) return false;
        _actions.ToggleRecruit?.Invoke(_offer[offerIndex]);
        return true;
    }

    public bool EditorBeginRun()
    {
        if (_picked.Count != _cfg.StartingFieldSlots) return false;
        _actions.BeginRun?.Invoke();
        // Existing end-to-end editor probes predate First Draft and need a deterministic lineage.
        // Player input still stops on the choice scrim; only this automation helper auto-picks.
        if (_pendingFirstRevision)
            _actions.ChooseStartingRevision?.Invoke(RevisionCatalog.BorrowedFutureId);
        return _run != null && _model.Screen == RunScreen.Wager;
    }

    public bool EditorOpenRevisionDraft()
    {
        if (_picked.Count != _cfg.StartingFieldSlots) return false;
        _actions.BeginRun?.Invoke();
        return _pendingFirstRevision && _run == null;
    }

    public bool EditorChooseRevision(string revisionId)
    {
        if (!_pendingFirstRevision) return false;
        _actions.ChooseStartingRevision?.Invoke(revisionId);
        return _run != null && _model.Screen == RunScreen.Wager;
    }

    public bool EditorOpenRevisionAtTick(int tick)
    {
        if (_model.Screen != RunScreen.Fight || _preparedFight == null ||
            _player == null || _lastBattle == null)
            return false;
        _player.BuildRevisionPreview(Mathf.Clamp(tick, 10, _lastBattle.EndTick));
        OpenRevision();
        return _revisionCombat.Mode == RevisionCombatMode.Opening ||
               _revisionCombat.Mode == RevisionCombatMode.Selecting;
    }

    /// <summary>
    /// Bind the Revision draft with representative data so the Hourstone and the on-unit ability
    /// cluster can be captured without driving a whole run into a fight. Presentation only: it fills
    /// the overlay model and touches no run state, which is exactly the surface a layout check needs.
    /// </summary>
    public bool EditorLoadRevisionDraftFixture(
        int maxSeconds = 4, bool withTarget = true, bool recall = false, int openSeconds = 0)
    {
        if (_revisionCombatOverlay == null) return false;
        _model.Screen = RunScreen.Fight;
        _revisionCombat.Mode = RevisionCombatMode.Selecting;
        _revisionCombat.Presentation = RevisionPresentationPhase.Held;
        _revisionCombat.PresentationProgress = 1f;
        _revisionCombat.Lineage = recall
            ? RevisionEffectKind.RecallToFormation
            : RevisionEffectKind.BorrowedFuture;
        _revisionCombat.LineageName = recall ? "RECALL TO FORMATION" : "BORROWED FUTURE";
        _revisionCombat.Name = _revisionCombat.LineageName;
        _revisionCombat.FinalChance = false;
        _revisionCombat.CanOpen = false;
        _revisionCombat.MaxSeconds = Mathf.Clamp(maxSeconds, 1, 6);
        // Default 0 mirrors the real open: the stone starts at the present and the player turns it.
        _revisionCombat.SelectedSeconds =
            Mathf.Clamp(openSeconds, 0, _revisionCombat.MaxSeconds);
        _revisionCombat.DockSide = RevisionDockSide.Bottom;
        _revisionCombat.CanConfirm = withTarget && _revisionCombat.SelectedSeconds >= 1;
        _revisionCombat.Status = "";
        _revisionCombat.Sweep = _revisionCombat.SelectedSeconds;
        _revisionCombat.Hold = 0f;

        _revisionCombat.Anchors.Clear();
        // A deliberately non-monotonic payoff curve: the point of notch weight is that the best
        // second is not simply the deepest one.
        float[] shape = { 0.35f, 1f, 0.62f, 0.9f, 0.48f, 0.28f };
        _revisionCombat.Anchors.Add(new RevisionAnchorModel
        {
            Seconds = 0,
            Label = "NOW",
            Payoff = 1f,
        });
        for (int seconds = 1; seconds <= _revisionCombat.MaxSeconds; seconds++)
            _revisionCombat.Anchors.Add(new RevisionAnchorModel
            {
                Seconds = seconds,
                Label = $"\u2212{seconds}s",
                Payoff = withTarget && !recall ? shape[(seconds - 1) % shape.Length] : 1f,
            });

        _revisionFixtureCluster = true;
        RevisionClusterModel cluster = _revisionCombat.Cluster;
        cluster.Visible = withTarget && _revisionCombat.SelectedSeconds >= 1;
        cluster.Kind = _revisionCombat.Lineage;
        // Panel units, not screen pixels — the live path arrives here through ScreenToPanel.
        cluster.Panel = ScreenToPanel(new Vector2(Screen.width * 0.30f, Screen.height * 0.38f));
        cluster.Carry = 33;
        cluster.ManaAfter = 40;
        cluster.ManaMax = 40;
        cluster.Shield = 8;
        cluster.DisarmSeconds = 1.5f;
        cluster.HasHome = recall;
        cluster.HomePanel = ScreenToPanel(new Vector2(Screen.width * 0.42f, Screen.height * 0.78f));

        _revisionCombatOverlay.Bind(_revisionCombat);
        Rebuild();
        return true;
    }

    public bool EditorSelectFirstRevisionTarget()
    {
        if (_revisionCombat.Mode != RevisionCombatMode.Selecting || _lastBattle == null)
            return false;
        PlaybackState branch = FoldAt(_lastBattle, _revisionBranchTick - 1);
        foreach (PlaybackUnit unit in branch.Units)
            if (IsEligibleRevisionTarget(unit.Id))
            {
                ToggleRevisionTarget(unit);
                return _revisionCombat.CanConfirm;
            }
        return false;
    }

    public bool EditorConfirmRevision()
    {
        if (!_revisionCombat.CanConfirm) return false;
        ConfirmRevision();
        return _preparedFight == null;
    }

    public void EditorSetPlanningTab(int tab) => _actions.SetPlanningTab?.Invoke(tab);

    public void EditorAdvanceFromManagement() => _actions.Advance?.Invoke();

    public void EditorChooseWager(int tier) => _actions.ChooseTier?.Invoke(tier);

    public void EditorConfirmWager() => _actions.ConfirmWager?.Invoke();

    public void EditorSelectPlanningCard(string key) => _actions.SelectPlanningCard?.Invoke(key);

    public void EditorCommitDeployment() => _actions.CommitDeployment?.Invoke();

    /// <summary>
    /// Exercises the shipping deployment gesture without bypassing its pointer controller. The
    /// drag remains held so MCP can capture the miniature and exact drop marker before releasing.
    /// </summary>
    public string EditorPreviewDeploymentDrag(int unitIndex = 0)
    {
        if (!CanInteractWithDeployment())
            return "Deployment drag: FAIL · shipping deployment is not active";
        if (!_placement.ContainsKey(unitIndex))
            return $"Deployment drag: FAIL · unit {unitIndex} is not placed";
        if (_deployPointerId >= 0)
            CancelDeploymentGesture(refreshBoard: true);

        Hex destination = default;
        bool foundDestination = false;
        for (int row = 0; row < Battle.BoardRows / 2 && !foundDestination; row++)
            for (int col = 0; col < Battle.BoardCols; col++)
            {
                Hex candidate = Hex.FromRowCol(row, col);
                if (OccupantOf(candidate) >= 0) continue;
                destination = candidate;
                foundDestination = true;
                break;
            }
        if (!foundDestination)
            return "Deployment drag: FAIL · no empty legal hex is available";

        if (!_player.TryGetUnitScreenBounds(new[] { unitIndex }, out Rect sourceBounds) ||
            !_player.TryHexToScreen(destination, out Vector2 destinationScreen))
            return "Deployment drag: FAIL · live board projection is unavailable";

        const int AutomationPointerId = 7301;
        OnDeploymentPointerDown(ScreenToPanel(sourceBounds.center), AutomationPointerId);
        OnDeploymentPointerMoved(ScreenToPanel(destinationScreen), AutomationPointerId);
        bool passed = _deployPointerUnit == unitIndex &&
                      _deployDragging &&
                      _deployHoverValid &&
                      _deployHoverHex.Equals(destination) &&
                      _player.EditorPlanningDropTargetVisible;
        if (!passed)
            CancelDeploymentGesture(refreshBoard: true);
        return passed
            ? $"Deployment drag: PASS · unit {unitIndex} held over {destination}"
            : "Deployment drag: FAIL · pointer did not resolve the unit and legal drop marker";
    }

    public string EditorCompleteDeploymentDrag()
    {
        if (!_deployDragging || _deployPointerId < 0 || !_deployHoverValid)
            return "Deployment drop: FAIL · no legal preview drag is active";
        int unitIndex = _deployPointerUnit;
        Hex destination = _deployHoverHex;
        if (!_player.TryHexToScreen(destination, out Vector2 destinationScreen))
            return "Deployment drop: FAIL · destination projection is unavailable";
        int pointerId = _deployPointerId;
        OnDeploymentPointerUp(ScreenToPanel(destinationScreen), pointerId);
        bool passed = _deployPointerId < 0 &&
                      _placement.TryGetValue(unitIndex, out Hex placed) &&
                      placed.Equals(destination) &&
                      !_player.EditorPlanningDropTargetVisible;
        return passed
            ? $"Deployment drop: PASS · unit {unitIndex} committed to {destination}"
            : "Deployment drop: FAIL · authoritative placement did not match the preview";
    }

    public bool EditorInspectFirstCombatUnit()
    {
        if (_model.Screen != RunScreen.Fight || _player == null) return false;
        for (int y = 80; y < Screen.height - 80; y += 30)
            for (int x = 80; x < Screen.width - 80; x += 30)
            {
                var unit = _player.PickUnit(new Vector2(x, y), 22f);
                if (unit == null) continue;
                _fightInspectedUnit = unit;
                _fightCard.style.display = DisplayStyle.Flex;
                _fightInspector.Bind(PlaybackInspector(unit));
                PositionFightCard(unit);
                return true;
            }
        return false;
    }

    public void EditorSkipFight() => SkipFight();

    public bool EditorSelectMarketOffer(int index)
    {
        if (_run == null || index < 0 || index >= _run.State.ShopOffers.Count) return false;
        _actions.SelectPlanningCard?.Invoke($"market:{index}");
        return true;
    }

    public bool EditorFocusWarbandHero(int fieldIndex)
    {
        if (_run == null || fieldIndex < 0 || fieldIndex >= _run.State.Field.Count)
            return false;
        _actions.FocusWarbandHero?.Invoke(_run.State.Field[fieldIndex].InstanceId);
        return true;
    }

    public bool EditorPreviewWarbandRosterDrag() =>
        _warbandBarView?.EditorPreviewFirstRosterDrag() == true;

    public void EditorUseInspectorAction(string action)
    {
        HallActionId id =
            action == "buy" ? HallActionId.Buy :
            action == "freeze" ? HallActionId.Freeze :
            action == "buy-slot" ? HallActionId.BuySlot :
            action == "deploy" ? HallActionId.Deploy :
            action == "equip" ? HallActionId.Equip :
            action == "equip-now" ? HallActionId.EquipNow :
            action == "unequip" ? HallActionId.Unequip :
            action == "reforge" ? HallActionId.Reforge :
            action == "keep-shopping" ? HallActionId.KeepShopping :
            action == "move" ? HallActionId.Move :
            action == "sell" ? HallActionId.SellHero :
            HallActionId.SellItem;
        _actions.InspectorAction?.Invoke(id);
    }

    public void EditorOpenInspector() => _actions.OpenInspector?.Invoke();

    public void EditorRerollMarket() => _actions.Reroll?.Invoke();

    public bool EditorValidateHubFlow()
    {
        HubFlowContract.Validate();
        UiPresentationContract.Validate();
        MarketOfferPresentationContract.Validate(_model.Planning.MarketOffers);
        return true;
    }

    public bool EditorValidateMarketOfferLayout()
    {
        MarketOfferPresentationContract.Validate(_model.Planning.MarketOffers);
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorValidateResolvedLayout();
        return false;
    }

    public string EditorWagerLayoutReport()
    {
        foreach (IRunScreenView view in _views)
            if (view is WagerView wager)
                return wager.EditorResolvedLayoutReport(
                    _root?.Q<VisualElement>("persistent-warband-bar"));
        return "Wager: FAIL · view is missing";
    }

    public string EditorDeployLayoutReport()
    {
        foreach (IRunScreenView view in _views)
            if (view is DeployView deploy)
                return deploy.EditorResolvedLayoutReport(
                    _root?.Q<VisualElement>("persistent-warband-bar"));
        return "Deploy: FAIL · view is missing";
    }

    /// <summary>Item 9 QA: the modal over the busiest surface it can open on, so the contract
    /// checks the honest worst case rather than an empty menu.</summary>
    public bool EditorLoadOptionsFixture(
        bool expandedText = false, bool reducedMotion = false)
    {
        if (!EditorLoadWorkbenchFixture("market-recruit", expandedText, reducedMotion))
            return false;
        _optionsPanel?.Open();
        return true;
    }

    public string EditorOptionsLayoutReport() =>
        _optionsPanel?.EditorResolvedLayoutReport(_safeAreaFrame) ??
        "Options: FAIL · panel is missing";

    public string EditorResultLayoutReport() =>
        _resultGateView?.EditorResolvedLayoutReport(
            _root?.Q<VisualElement>("persistent-warband-bar")) ??
        "Result gate: FAIL · view is missing";

    public string EditorCombatInspectorLayoutReport()
    {
        if (_fightCard == null || _fightInspector?.Root == null || _safeAreaFrame == null)
            return "Combat inspector: FAIL · view is missing";
        Rect card = _fightCard.worldBound;
        Rect safe = _safeAreaFrame.worldBound;
        bool finite =
            !float.IsNaN(card.x) && !float.IsNaN(card.y) &&
            card.width > 340f && card.width < 430f && card.height > 0f;
        bool contained =
            card.xMin >= safe.xMin - 1f && card.xMax <= safe.xMax + 1f &&
            card.yMin >= safe.yMin - 1f && card.yMax <= safe.yMax + 1f;
        bool anatomy =
            _fightInspector.Root.ClassListContains("wb-inspector--unit-sheet") &&
            _fightInspector.Root.Q<VisualElement>(className: "wb-unit-weapon") != null;
        int weaponFacts = _fightInspector.Root
            .Query<VisualElement>(className: "wb-weapon-stat").ToList().Count;
        int properties = _fightInspector.Root
            .Query<VisualElement>(className: "wb-unit-weapon-property").ToList().Count;
        VisualElement passiveRegion = _fightInspector.Root
            .Q<VisualElement>(className: "wb-unit-passives");
        int passiveRows = passiveRegion == null
            ? 0
            : passiveRegion.Query<VisualElement>(
                className: "wb-inspector__line").ToList().Count;
        bool passed = finite && contained && anatomy && weaponFacts > 0 && passiveRows > 0;
        return $"Combat inspector: {(passed ? "PASS" : "FAIL")} · " +
               $"card={card.width:0.#}×{card.height:0.#}; contained={contained}; " +
               $"weapon facts={weaponFacts}; properties={properties}; passives={passiveRows}";
    }

    public string EditorEnvironmentReport()
    {
        if (_uiEnvironment == null) return "UI environment: FAIL · classifier is missing";
        UiEnvironmentSnapshot value = _uiEnvironment.Current;
        string classes = string.Join(",", _root.GetClasses());
        return
            $"UI environment: PASS · panel={value.PanelSize.x:0.#}×{value.PanelSize.y:0.#}; " +
            $"scale={value.PixelsPerPoint:0.###}; form={value.FormFactor}; " +
            $"input={value.InputModality}; safe={value.SafeInsets.Left:0.#}/" +
            $"{value.SafeInsets.Top:0.#}/{value.SafeInsets.Right:0.#}/" +
            $"{value.SafeInsets.Bottom:0.#}; classes={classes}";
    }

    public string EditorRotationGuardReport()
    {
        var report = new UiLayoutReport("Rotation guard");
        UiLayoutContract.RequireResolved(report, _rotationGuard, "guard");
        UiLayoutContract.RequireInside(report, _rotationGuard, _root, "rotation guard");
        if (_rotationGuard == null ||
            _rotationGuard.resolvedStyle.display == DisplayStyle.None)
            report.Fail("portrait handheld guard is hidden");
        if (_uiEnvironment == null || !_uiEnvironment.Current.PortraitHandheld)
            report.Fail("environment did not classify portrait handheld");
        return report.ToString();
    }

    public void EditorOpenWorkbenchArmory() => _actions.OpenLoadout?.Invoke("");

    public bool EditorShowWorkbenchKeywordTooltip()
    {
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorShowFirstKeywordTooltip();
        return false;
    }

    public bool EditorShowWorkbenchEquipmentTooltip() =>
        _warbandBarView?.EditorShowFirstEquipmentTooltip() ?? false;

    public bool EditorShowWorkbenchRankTierTooltip()
    {
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorShowFirstRankTierTooltip();
        return false;
    }

    public bool EditorShowWorkbenchWeaponFactTooltip()
    {
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorShowFirstWeaponFactTooltip();
        return false;
    }

    public bool EditorShowWorkbenchWeaponPropertyTooltip()
    {
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorShowWeaponPropertyTooltip();
        return false;
    }

    public string EditorWorkbenchSemanticSnapshot()
    {
        foreach (var view in _views)
            if (view is WorkbenchView workbench)
                return workbench.EditorSemanticLayoutSnapshot();
        return "workbench=missing";
    }

    public void EditorNewMusterOffer()
    {
        if (!_muster) _actions.NewRun?.Invoke();
        else _actions.RerollSeed?.Invoke();
    }

    public void EditorPreviewUiEffect(int cue) =>
        UiPolishSignals.Preview((UiPolishSignals.Cue)cue);

    public void EditorPreviewUiTransaction(int transaction) =>
        UiPolishSignals.Preview((UiTransactionKind)transaction);

    public void EditorOpenHallOverview() => OpenHallOverview();

    public void EditorOpenHallStation(int station) => OpenHallStation((HallStation)station);

    public void EditorOpenLoadout(string heroKey = "") => OpenLoadout(heroKey);

    public void EditorCloseLoadout() => CloseLoadout();

    public bool EditorOpenResultGate()
    {
        if (_lastBattle == null || _lastFightOutcome == null) return false;
        Go(RunScreen.Fight);
        OpenFightResult();
        return true;
    }

    public void EditorWatchFightAgain() => WatchFightAgain();

    public void EditorContinueFightResult() => ContinueFightResult();

    /// <summary>
    /// Unity-side proof that the shipped plugin accepts both banked-victory actions and that a
    /// saved choice survives the same resume path the menu uses. The helper owns only temporary
    /// controllers: it never touches the live run or autosave.
    /// </summary>
    public string EditorVerifyBeyondHourRuntimePaths()
    {
        if (_content == null || _cfg == null)
            return "FAIL · shell content is not ready";

        RunController ChoiceRun(ulong seed)
        {
            var temporary = new RunController(seed, _content,
                new[] { new HeroInstance { ChassisId = "bulwark" } }, _cfg);
            temporary.State.Act = _cfg.Acts;
            temporary.State.NodeIndex = _cfg.NodesPerAct;
            temporary.State.VictoryBanked = true;
            temporary.State.Phase = RunPhase.VictoryChoice;
            return temporary;
        }

        var continued = ChoiceRun(0xB37AUL);
        string saved = RunSave.Write(continued.State);
        RunController resumed = RunController.Resume(
            RunSave.Read(saved), _content, _cfg);
        resumed.ContinueBeyondTheHour();
        bool continuePass =
            resumed.State.InEndless &&
            resumed.State.Act == _cfg.Acts + 1 &&
            resumed.State.NodeIndex == 0 &&
            resumed.State.Victory;

        var retired = ChoiceRun(0xB37BUL);
        retired.RetireWithVictory();
        bool retirePass =
            retired.State.Over &&
            retired.State.Victory &&
            !retired.State.InEndless;

        RunController previousRun = _run;
        bool projectionPass;
        try
        {
            _run = ChoiceRun(0xB37CUL);
            BuildPlanning();
            projectionPass =
                _model.Planning.BeatKind == PlanningBeat.EndlessChoice &&
                _model.Planning.Interlude.Count == 2 &&
                _model.Planning.Interlude[0].Option == 0 &&
                _model.Planning.Interlude[1].Option == 1;
        }
        finally
        {
            _run = previousRun;
        }

        return continuePass && retirePass && projectionPass
            ? $"PASS · resumed choice continued into Act {resumed.State.Act}; " +
              "retirement kept the banked victory; live Workbench projected both actions"
            : "FAIL · a banked-victory runtime or Workbench projection contract failed";
    }

    public void EditorSetReducedMotion(bool enabled)
    {
        _reducedMotion = enabled;
        Rebuild();
    }

    public void EditorForcePhoneLayout(bool enabled)
    {
        _debugPhoneLayout = enabled;
        Rebuild();
    }

    public string EditorStateSummary()
    {
        string run = _run == null
            ? "run=none"
            : $"phase={_run.State.Phase}; act={_run.State.Act}; beat={_run.State.NodeIndex + 1}; " +
              $"sand={_run.State.Sand}; field={_run.State.Field.Count}/{_run.State.FieldSlots}";
        return $"screen={_model.Screen}; {run}; tab={_planningTab}; selected={_selectedCardKey}; " +
               $"placement={_placement.Count}";
    }
#endif
}
