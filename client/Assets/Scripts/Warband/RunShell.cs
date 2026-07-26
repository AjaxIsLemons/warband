using System;
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
    private ReplayPlayer _player;
    private HallEnvironmentController _hallEnvironment;
    private PanelSettings _panelSettings;
    private VisualElement _root;
    private VisualElement _fightOverlay;
    private VisualElement _fightHitSurface;
    private Label _fightHint;
    private Button _fightSkip;
    private VisualElement _fightInspectorScrim;
    private InspectorPanel _fightInspector;
    private ResultGateView _resultGateView;
    private PlaybackUnit _fightInspectedUnit;
    private BattleResult _lastBattle;
    private FightOutcome _lastFightOutcome;
    private HubSequencePlan _pendingHubPlan = new HubSequencePlan();
    private readonly HubAttentionModel _hubAttention = new HubAttentionModel();
    private RunConclusionReceipt _conclusionReceipt;
    private bool _resultGateOpen;
    private bool _hallOverview = true;
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
    /// <summary>Menu-scoped message (a discarded save). Separate from _feedback so a load failure
    /// cannot leak into the Hall's transaction receipts.</summary>
    private string _menuNotice = "";
    private List<string> _offer = new List<string>();
    private readonly List<string> _picked = new List<string>();
    private FightTier _tier = FightTier.Fraying;
    private bool _tierChosen;
    private PlanningTab _planningTab = PlanningTab.Muster;
    private string _selectedCardKey = "";
    private bool _inspectorOpen;
    private int _selectedMarketOffer = -1;
    private long _equipNowItemInstanceId;
    private int _equipNowOfferIndex = -1;

    // Deployment: hexes chosen for each FIELD index. Kept here, not in the view, and cleared on
    // every entry so a formation can never leak from one fight into the next.
    private readonly Dictionary<int, Hex> _placement = new Dictionary<int, Hex>();
    private int _deploySelected = -1;
    private int _selectedItem = -1;
    private string _feedback = "";
    private bool _feedbackIsError;
    private string _recruitFeedback = "";
    private bool _recruitFeedbackIsError;

    private void Start()
    {
        _content = new Catalog();
        _presentation = PresentationCatalog.Load();
        _cfg = new RunConfig();
        _player = FindFirstObjectByType<ReplayPlayer>();
        _reducedMotion = PlayerPrefs.GetInt("ui.reducedMotion", 0) != 0;
        _hallEnvironment = HallEnvironmentController.Create(Camera.main,
            HubPresentationConfig.Load());

        // The board belongs to the shell now, and the menu is not a fight: park it empty rather
        // than letting a leftover fixture loop behind the front end.
        if (_player != null) _player.Idle();

        NewSeed();
        WireActions();
        BuildUI();
        Rebuild();
    }

    private void OnDestroy()
    {
        if (_player != null) _player.PlaybackEnded -= OnFightWatched;   // never outlive the shell
        foreach (var view in _views)
            if (view is IDisposable disposable) disposable.Dispose();
        if (_hallEnvironment != null) Destroy(_hallEnvironment.gameObject);
        if (_panelSettings != null) Destroy(_panelSettings);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || !keyboard.f2Key.wasPressedThisFrame) return;
        _flowLabVisible = !_flowLabVisible;
        if (_flowLab != null)
            _flowLab.style.display = _flowLabVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
#endif

    // ---- wiring ------------------------------------------------------------------

    private void NewSeed() => _seed = (ulong)DateTime.Now.Ticks;

    private void WireActions()
    {
        _actions.NewRun = () =>
        {
            _picked.Clear();
            _recruitFeedback = "";
            _recruitFeedbackIsError = false;
            _offer = RunSetup.RecruitOffer(_content, _seed);
            Go(RunScreen.Recruit);
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
                _menuNotice = problem;
                Go(RunScreen.Menu);
                return;
            }
            _menuNotice = "";
            AdoptResumedRun(loaded);
            OpenHallOverview();
        };
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
            _recruitFeedback = "";
            _recruitFeedbackIsError = false;
            _offer = RunSetup.RecruitOffer(_content, _seed);
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
                    receipt: "Champion removed from the warband.",
                    transaction: UiTransactionKind.MusterDeselect);
                _picked.RemoveAt(selectedSlot);
                _recruitFeedback = "";
                _recruitFeedbackIsError = false;
            }
            else if (_picked.Count < _cfg.StartingFieldSlots)
            {
                int destination = _picked.Count;
                UiPolishSignals.Emit(UiPolishSignals.Cue.Confirm,
                    sourceId: "muster:" + id,
                    targetId: "muster-slot:" + destination,
                    tone: UiFeedbackTone.Positive,
                    receipt: "Champion added to the warband.",
                    transaction: UiTransactionKind.MusterSelect);
                _picked.Add(id);
                _recruitFeedback = "";
                _recruitFeedbackIsError = false;
            }
            else
            {
                _recruitFeedback = "Remove one champion first.";
                _recruitFeedbackIsError = true;
                UiPolishSignals.Emit(UiPolishSignals.Cue.Error,
                    sourceId: "muster:" + id,
                    targetId: "muster:" + id,
                    tone: UiFeedbackTone.Negative,
                    receipt: _recruitFeedback);
            }
            Rebuild();
        };
        _actions.BeginRun = () =>
        {
            if (RunSetup.PicksRemaining(_picked.Count, _cfg) > 0) return;
            // Starting a new run abandons any saved one. Do it here rather than on the menu so a
            // player who opens the draft and backs out still has their old run.
            RunSaveFile.Delete();
            _savedText = "";
            _menuNotice = "";
            _run = RunSetup.Begin(_seed, _content, _picked, _cfg);
            _planningTab = PlanningTab.Muster;
            _selectedCardKey = "hero:field:0";
            _inspectorOpen = false;
            _tierChosen = false;
            _hallOverview = true;
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
            Go(RunScreen.Management);
        };

        _actions.ChooseTier = i =>
        {
            _tier = (FightTier)i;
            _tierChosen = true;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Select);
            Rebuild();
        };
        _actions.Advance = BeginNode;
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
        _actions.SelectPlanningCard = SelectPlanningCard;
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
        _actions.ChooseBossReward = ChooseBossReward;
        _actions.WatchFightAgain = WatchFightAgain;
        _actions.ContinueFightResult = ContinueFightResult;

        _actions.BuyOffer = i =>
        {
            _selectedMarketOffer = i;
            _selectedCardKey = $"market:{i}";
            Rebuild();
        };
        _actions.ToggleFreeze = i => ShopAction(() => _run.ToggleFreeze(i));
        _actions.Reroll = () => ShopAction(() =>
        {
            int beforeSand = _run.State.Sand;
            _run.Reroll();
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reroll,
                sourceId: "action-secondary", targetId: "hub-workspace",
                resourceId: "ledger-sand", groupId: "market-offers",
                amount: _run.State.Sand - beforeSand, tone: UiFeedbackTone.Sand,
                receipt: "Market stock refreshed.");
        });
        _actions.ChooseSpec = w =>
        {
            ShopAction(() =>
            {
                _run.ChooseSpec(w);
                UiPolishSignals.Emit(UiPolishSignals.Cue.RankUp,
                    targetId: "station-warband", tone: UiFeedbackTone.Major,
                    receipt: "Specialization engraved.",
                    transaction: UiTransactionKind.RankChoice);
            });
            if (!_feedbackIsError) OpenHallStation(HallStation.Market);
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
            _feedback = "";
            _feedbackIsError = false;
            ShowDeploymentOnBoard();
            Rebuild();
        };
        _actions.ClearDeployment = () =>
        {
            _placement.Clear();
            _deploySelected = -1;
            ShowDeploymentOnBoard();
            Rebuild();
        };
        _actions.CommitDeployment = () =>
        {
            if (_placement.Count < _run.State.Field.Count) return;
            ResolveCurrentNode();
        };
        _actions.BoardClicked = OnBoardClicked;

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
                tone: UiFeedbackTone.Positive, receipt: "Equipment seated.",
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
            _run.SellHero(bench ? RosterZone.Bench : RosterZone.Field, idx);
            _selectedItem = -1;
        });
        _actions.SellItem = i => ShopAction(() => { _run.SellItem(i); _selectedItem = -1; });
        _actions.MoveHero = (bench, idx) =>
            ShopAction(() => { if (bench) _run.BenchToField(idx); else _run.FieldToBench(idx); });

        _actions.BackToMenu = () =>
        {
            _run = null;
            _resultGateOpen = false;
            _lastBattle = null;
            _lastFightOutcome = null;
            _hubAttention.Reset();
            _equipNowItemInstanceId = 0;
            _equipNowOfferIndex = -1;
            NewSeed();
            Go(RunScreen.Menu);
        };
    }

    private void OpenHallOverview()
    {
        if (_run == null) return;
        HallStation leaving = TabStation(_planningTab);
        UiPolishSignals.Emit(UiPolishSignals.Cue.Route,
            sourceId: AnchorTarget(leaving), targetId: "hub-workspace",
            tone: UiFeedbackTone.Sand, receipt: "Returned to the Hourstone Table.");
        _hallOverview = true;
        _inspectorOpen = false;
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
            tone: UiFeedbackTone.Sand, receipt: StationDisplayName(station) + " opened.");
        _planningTab = StationTab(station);
        if (station != HallStation.Armory) _selectedItem = -1;
        _hallOverview = false;
        _inspectorOpen = false;
        _hubAttention.Clear(station);
        if (_recommendedStation == station && _run.State.PendingSpec == null &&
            _run.State.Phase != RunPhase.Reward)
            _recommendedStation = HallStation.Breach;
        SelectDefaultForTab();
        Go(RunScreen.Management);
    }

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
    /// Every shop call can legally refuse (not enough gold, spec choice pending, …). The run
    /// layer throws with a human-readable reason, so surface it instead of swallowing it — a
    /// silent no-op reads as a broken button.
    /// </summary>
    private bool ShopAction(Action act, bool rebuild = true)
    {
        RunMutationSnapshot before = _run == null ? null : RunMutationSnapshot.Capture(_run.State);
        bool succeeded = false;
        try
        {
            act();
            _feedback = "";
            _feedbackIsError = false;
            succeeded = true;
            if (before != null)
            {
                var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
                RecordHubPlan(plan, navigateBlocking: true);
            }
        }
        catch (Exception ex)
        {
            _feedback = ex.Message;
            _feedbackIsError = true;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "feedback",
                tone: UiFeedbackTone.Negative, receipt: ex.Message);
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
        if (_run == null || string.IsNullOrEmpty(key)) return;
        _selectedCardKey = key;
        // Selection seats the choice in the Hall action tray. Full rules are progressive
        // disclosure through INSPECT, so choosing never throws a large dossier over the stage.
        _inspectorOpen = false;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Select, sourceId: key,
            tone: UiFeedbackTone.Preview);
        if (TrySimpleIndex(key, "market", out var offer))
            _selectedMarketOffer = offer;
        if (TrySimpleIndex(key, "item", out var item))
            _selectedItem = item;
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
        CardModel selected = _model.Planning.Market.Find(card => card.Key == $"market:{index}");
        string acquiredName = selected?.Title ?? "Offer";
        string sourceId = $"market:{index}";
        PurchaseResult purchase = null;
        bool succeeded = ShopAction(() =>
        {
            purchase = _run.BuyOffer(index);
            if (purchase.ItemInstanceId > 0)
            {
                _equipNowItemInstanceId = purchase.ItemInstanceId;
                _equipNowOfferIndex = index;
                _selectedMarketOffer = index;
                _selectedCardKey = $"market:{index}";
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
            string result = purchase.Outcome == PurchaseOutcome.RankUp
                ? $"{purchase.PreviousRank} → {purchase.NewRank} secured"
                : purchase.Outcome == PurchaseOutcome.Recruit
                    ? "recruited"
                    : purchase.Outcome == PurchaseOutcome.Inscription
                        ? "bound to the Hourstone"
                        : "sent to the Armory";
            _feedback = $"{acquiredName} {result} · {spent} Sand spent.";
            UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                sourceId: sourceId, targetId: TransactionTarget(purchase.Outcome),
                resourceId: "ledger-sand", groupId: "market-offers",
                amount: -spent, tone: UiFeedbackTone.Sand, receipt: _feedback,
                transaction: TransactionFor(purchase.Outcome));
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
            _feedback =
                $"Field capacity {beforeCapacity} → {_run.State.FieldSlots} · " +
                $"{spent} Sand spent.";
            UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                sourceId: _selectedCardKey, targetId: "station-warband",
                resourceId: "ledger-sand", amount: -spent, tone: UiFeedbackTone.Sand,
                receipt: _feedback, transaction: UiTransactionKind.BuyCapacity);
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
        if (_run == null) return;
        if (action == HallActionId.KeepShopping)
        {
            _equipNowItemInstanceId = 0;
            _equipNowOfferIndex = -1;
            SelectDefaultForTab();
            _feedback = "";
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
            _feedback = "Item pinned. Choose a champion to preview exact equipment changes.";
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
                _feedback = "Choose a hex in your half.";
                _feedbackIsError = false;
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
                    _feedback =
                        $"{forged.WeaponId} forged {forged.PreviousTier} → {forged.NewTier} · " +
                        $"{forged.SandSpent} Sand spent.";
                    UiPolishSignals.Emit(UiPolishSignals.Cue.Purchase,
                        sourceId: $"hero:{(inBench ? "bench" : "field")}:{heroIndex}",
                        targetId: $"hero:{(inBench ? "bench" : "field")}:{heroIndex}",
                        resourceId: "ledger-sand", amount: -forged.SandSpent,
                        tone: UiFeedbackTone.Major, receipt: _feedback,
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
            _feedback = path == InterludePath.Treasury
                ? $"+{reward.Sand} Sand secured."
                : $"{RewardName(reward)} secured.";
            _feedbackIsError = false;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reward,
                targetId: StationTarget(path == InterludePath.Armory ? HallStation.Armory :
                    path == InterludePath.Hourstone ? HallStation.Hourstone : HallStation.Market),
                amount: reward.Sand, tone: UiFeedbackTone.Positive, receipt: _feedback,
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
            _feedback = ex.Message;
            _feedbackIsError = true;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "feedback",
                tone: UiFeedbackTone.Negative, receipt: ex.Message);
            Rebuild();
        }
    }

    private void ChooseBossReward(int option)
    {
        if (_run == null) return;
        var before = RunMutationSnapshot.Capture(_run.State);
        try
        {
            string name = _content.Banner(_run.PreviewBossRewards()[option]).Name;
            _run.ChooseBossReward(option);
            _feedback = $"{name} bound to the Hourstone.";
            _feedbackIsError = false;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Reward,
                targetId: "station-hourstone", tone: UiFeedbackTone.Major,
                receipt: _feedback, transaction: UiTransactionKind.BindInscription);
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
            _feedback = ex.Message;
            _feedbackIsError = true;
            UiPolishSignals.Emit(UiPolishSignals.Cue.Error, targetId: "feedback",
                tone: UiFeedbackTone.Negative, receipt: ex.Message);
            Rebuild();
        }
    }

    private string RewardName(RewardOffer reward)
    {
        switch (reward.Kind)
        {
            case OfferKind.Weapon: return _content.Weapon(reward.Id).Name;
            case OfferKind.Trinket: return _content.Trinket(reward.Id).Name;
            case OfferKind.Inscription: return _content.Banner(reward.Id).Name;
            default: return "Reward";
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
    /// Leaving the map. An event resolves immediately; a fight opens DEPLOYMENT first — placing
    /// the warband is the decision the fight is made of, so it must not be skipped.
    /// </summary>
    private void BeginNode()
    {
        if (_run == null || _run.State.Phase != RunPhase.Planning) return;

        if (_run.CurrentNodeKind == NodeKind.Event)
        {
            ResolveCurrentNode();
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
        _feedback = "";
        _feedbackIsError = false;
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
                int gained = _run.ResolveEvent();
                _feedback = $"Quiet road. +{gained} Sand.";
                _feedbackIsError = false;
                var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
                RecordHubPlan(plan, navigateBlocking: false);
                OpenHallStation(plan.RecommendedStation);
                return;
            }
            else
            {
                var placement = CurrentPlacement();
                var outcome = kind == NodeKind.Boss
                    ? _run.ResolveBoss(placement)
                    : _run.ResolveFight(_tier, placement);

                _feedback = outcome.Won
                    ? $"Won — {outcome.EnemiesKilled}/{outcome.EnemyCount} felled, +{outcome.SandEarned} Sand."
                    : "The warband is broken.";
                _feedbackIsError = !outcome.Won;
                _lastFightOutcome = outcome;
                _lastBattle = outcome.Battle;
                _fightsCompleted++;
                var plan = HubFlowPlanner.Plan(before, RunMutationSnapshot.Capture(_run.State));
                RecordHubPlan(plan, navigateBlocking: false);
                BuildConclusionReceiptIfNeeded();

                // Watch it. The fight is already resolved — playback is presentation only, so the
                // shell parks on the board and moves on when the replay ends (or immediately, if
                // there is no player to watch it with).
                if (_player != null && outcome.Battle != null)
                {
                    _player.PlaybackEnded += OnFightWatched;
                    _player.PlayBattle(outcome.Battle);
                    _resultGateOpen = false;
                    Go(RunScreen.Fight);
                    return;
                }

                Go(RunScreen.Fight);
                OpenFightResult();
                return;
            }
        }
        catch (Exception ex)
        {
            _feedback = ex.Message;
            _feedbackIsError = true;
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
    private Vector2 PanelToScreen(Vector2 panel)
    {
        float pw = _root.resolvedStyle.width, ph = _root.resolvedStyle.height;
        if (pw <= 0f) pw = Screen.width;
        if (ph <= 0f) ph = Screen.height;
        return new Vector2(panel.x * Screen.width / Mathf.Max(1f, pw),
                           Screen.height - panel.y * Screen.height / Mathf.Max(1f, ph));
    }

    private void OnBoardClicked(Vector2 panelPos)
    {
        if (_model.Screen != RunScreen.Deploy || _player == null ||
            _run == null || _run.State.Phase != RunPhase.Planning ||
            _run.CurrentNodeKind == NodeKind.Event)
            return;
        if (!_player.TryScreenToHex(PanelToScreen(panelPos), out var hex))
        {
            Fail("That is off the board.");
            return;
        }
        // The run layer enforces this too, but a refusal AFTER lock-in would be far too late to
        // be useful — say it at the moment of the click.
        if (hex.Row >= Battle.BoardRows / 2)
        {
            Fail("You may only deploy in your own half.");
            return;
        }

        int occupant = OccupantOf(hex);
        if (_deploySelected < 0)
        {
            // Nothing held: clicking a placed hero picks them up, so a formation can be
            // rearranged without first hunting for their chip in the rail.
            if (occupant >= 0) { _deploySelected = occupant; _feedback = ""; }
            else Fail("Choose a champion first.");
            ShowDeploymentOnBoard();
            Rebuild();
            return;
        }

        if (occupant >= 0 && occupant != _deploySelected)
        {
            // Swap rather than refuse — refusing here would force a tedious move-out-of-the-way
            // dance for the most ordinary rearrangement there is.
            if (_placement.TryGetValue(_deploySelected, out var from)) _placement[occupant] = from;
            else _placement.Remove(occupant);
        }
        _placement[_deploySelected] = hex;
        _deploySelected = -1;
        _feedback = "";
        _feedbackIsError = false;
        ShowDeploymentOnBoard();
        Rebuild();
    }

    private int OccupantOf(Hex hex)
    {
        foreach (var kv in _placement)
            if (kv.Value.Equals(hex)) return kv.Key;
        return -1;
    }

    private void Fail(string why)
    {
        _feedback = why;
        _feedbackIsError = true;
        Rebuild();
    }

    /// <summary>
    /// Paint the pending formation onto the real board using the SAME projection a fight uses,
    /// so what the player arranges is literally what they will watch.
    /// </summary>
    private void ShowDeploymentOnBoard()
    {
        if (_player == null || _run == null) return;
        var units = new List<PlaybackUnit>();
        for (int i = 0; i < _run.State.Field.Count; i++)
        {
            if (!_placement.TryGetValue(i, out var hex)) continue;
            var def = ComposeHero(_run.State.Field[i]);
            units.Add(PlaybackUnit.From(UnitState.Spawn(i, 0, def, hex)));
        }
        int id = 100;
        foreach (var e in EnemiesForCurrentNode())
            units.Add(PlaybackUnit.From(UnitState.Spawn(id++, 1, e.Def, e.Pos)));

        _player.ShowSnapshot(units);
        _player.SetPlanningSelection(_deploySelected);
    }

    private List<(UnitDef Def, Hex Pos)> EnemiesForCurrentNode()
    {
        // RunController.PreviewEnemies, never a local guess: the encounter rng is derived from
        // private salts, so any reconstruction here would show a different army than spawns.
        try { return _run.PreviewEnemies(_tier); }
        catch { return new List<(UnitDef, Hex)>(); }
    }

    private void OnFightWatched()
    {
        if (_player != null) _player.PlaybackEnded -= OnFightWatched;
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
        _resultGateOpen = true;
        CloseFightInspector();
        Rebuild();
    }

    private void WatchFightAgain()
    {
        if (!_resultGateOpen || _lastBattle == null || _player == null) return;
        _resultGateOpen = false;
        _resultGateView?.Hide();
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
            FinalCause = _run.State.Victory ? "Final boss defeated" : "Warband defeated",
        };
    }

    // ---- view plumbing -----------------------------------------------------------

    private void BuildUI()
    {
        var document = GetComponent<UIDocument>();
        _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        var theme = Resources.Load<ThemeStyleSheet>("DebugTheme");
        if (theme != null) _panelSettings.themeStyleSheet = theme;
        _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        _panelSettings.match = 0.5f;
        _panelSettings.sortingOrder = 700;          // under the debug cockpit, over the board
        document.panelSettings = _panelSettings;
        document.sortingOrder = 700;

        _root = document.rootVisualElement;
        foreach (var sheet in new[]
                 { "UI/SkirmishStyles", "UI/RunShellStyles", "UI/PlanningWorkspaceStyles",
                   "UI/RunFlowStyles", "UI/HubStyles", "UI/LastHourTokens",
                   "UI/HallPhysicalStyles", "UI/MarketOfferCardStyles" })
        {
            var uss = Resources.Load<StyleSheet>(sheet);
            if (uss != null) _root.styleSheets.Add(uss);
            else Debug.LogWarning($"[RunShell] stylesheet not found: {sheet}");
        }
        _root.RegisterCallback<GeometryChangedEvent>(_ => ApplyShellLayoutClasses());

        _views.Add(new MenuView(_actions));
        _views.Add(new RecruitView(_actions, _hallEnvironment?.Services));
        _views.Add(new ManagementView(_actions, _hallEnvironment?.Services));
        _views.Add(new WagerView(_actions));
        _views.Add(new DeployView(_actions));
        _views.Add(new RunOverView(_actions));
        foreach (var v in _views) _root.Add(v.Root);

        BuildFightOverlay();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BuildFlowLab();
#endif
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

        _fightHint = new Label("CLICK OR TAP A UNIT  ·  OPEN COMBAT CARD");
        _fightHint.AddToClassList("fight-inspect-hint");
        _fightHint.pickingMode = PickingMode.Ignore;
        _fightOverlay.Add(_fightHint);

        _fightSkip = new Button(SkipFight) { text = "SKIP ▶" };
        _fightSkip.AddToClassList("btn");
        _fightSkip.AddToClassList("btn--ghost");
        _fightSkip.AddToClassList("fight-skip");
        _fightSkip.pickingMode = PickingMode.Position;
        _fightOverlay.Add(_fightSkip);

        _fightInspectorScrim = new VisualElement();
        _fightInspectorScrim.AddToClassList("modal-scrim");
        _fightInspectorScrim.AddToClassList("fight-inspector-scrim");
        _fightInspectorScrim.pickingMode = PickingMode.Position;
        var modal = new VisualElement();
        modal.AddToClassList("management-inspector-modal");
        modal.AddToClassList("fight-inspector-modal");
        var close = new Button(CloseFightInspector) { text = "CLOSE  ×" };
        close.AddToClassList("btn");
        close.AddToClassList("btn--ghost");
        close.AddToClassList("management-inspector-close");
        modal.Add(close);
        _fightInspector = new InspectorPanel(_ => { });
        _fightInspector.Root.AddToClassList("wb-inspector--modal");
        modal.Add(_fightInspector.Root);
        close.BringToFront();
        _fightInspectorScrim.Add(modal);
        _fightOverlay.Add(_fightInspectorScrim);
        _fightInspectorScrim.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _fightInspectorScrim) CloseFightInspector();
        });
        _fightOverlay.schedule.Execute(RefreshFightInspector).Every(150);
        _resultGateView = new ResultGateView(_actions);
        _fightOverlay.Add(_resultGateView.Root);
        _root.Add(_fightOverlay);
        CloseFightInspector();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BuildFlowLab()
    {
        _flowLab = new VisualElement();
        _flowLab.AddToClassList("flow-lab");
        _flowLab.pickingMode = PickingMode.Position;
        _flowLab.Add(new Label("F2 · UI FLOW LAB") { name = "flow-lab-title" });

        AddFlowLabButton("MUSTER SCREEN", () => _actions.NewRun?.Invoke());
        AddFlowLabButton("MUSTER · NEW OFFER", () =>
        {
            if (_model.Screen == RunScreen.Recruit) _actions.RerollSeed?.Invoke();
        });
        AddFlowLabButton("MUSTER · REVEAL", () => RecruitViewInstance()?.PreviewReveal());
        AddFlowLabButton("MUSTER · HEALTH LENS", () =>
            RecruitViewInstance()?.PreviewLens(0, MusterLensTarget.Health));
        AddFlowLabButton("MUSTER · SIGNATURE LENS", () =>
            RecruitViewInstance()?.PreviewLens(0, MusterLensTarget.Signature));
        AddFlowLabButton("MUSTER · PASSIVE LENS", () =>
            RecruitViewInstance()?.PreviewLens(0, MusterLensTarget.Passive));
        AddFlowLabButton("MUSTER · COMBINED LENS", () =>
            RecruitViewInstance()?.PreviewLens(0, MusterLensTarget.Combined));
        AddFlowLabButton("MUSTER · SELECT", () =>
            UiPolishSignals.Preview(UiTransactionKind.MusterSelect));
        AddFlowLabButton("MUSTER · DESELECT", () =>
            UiPolishSignals.Preview(UiTransactionKind.MusterDeselect));
        AddFlowLabButton("MUSTER · BLOCKED", () =>
            RecruitViewInstance()?.PreviewBlocked());
        AddFlowLabButton("MUSTER · READY", () => RecruitViewInstance()?.PreviewReady());
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
            MusterPresentationContract.Validate(_model.Recruit.Offer);
            RecruitViewInstance()?.ValidateResolvedLayout();
            Debug.Log("[HubFlowContract] Route, UI, Muster, and Market checks passed.");
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

    private RecruitView RecruitViewInstance()
    {
        foreach (IRunScreenView view in _views)
            if (view is RecruitView recruit) return recruit;
        return null;
    }
#endif

    private void InspectFightUnit(PointerDownEvent evt)
    {
        if (_model.Screen != RunScreen.Fight || evt.button != 0 || _player == null) return;
        var picked = _player.PickUnit(PanelToScreen(new Vector2(evt.position.x, evt.position.y)), 76f);
        if (picked == null)
        {
            CloseFightInspector();
            return;
        }
        _fightInspectedUnit = picked;
        RefreshFightInspector();
        _fightInspectorScrim.style.display = DisplayStyle.Flex;
        evt.StopPropagation();
    }

    private void RefreshFightInspector()
    {
        if (_model.Screen != RunScreen.Fight || _fightInspectedUnit == null ||
            _fightInspectorScrim.style.display != DisplayStyle.Flex)
            return;
        _fightInspector.Bind(PlaybackInspector(_fightInspectedUnit));
    }

    private void CloseFightInspector()
    {
        _fightInspectedUnit = null;
        if (_fightInspectorScrim != null)
            _fightInspectorScrim.style.display = DisplayStyle.None;
    }

    private void SkipFight()
    {
        if (_model.Screen != RunScreen.Fight) return;
        CloseFightInspector();
        if (_player != null) _player.PlaybackEnded -= OnFightWatched;
        if (_player != null && _lastBattle != null)
            _player.BuildLoadedPreview(_lastBattle.EndTick);
        OpenFightResult();
    }

    /// <summary>
    /// Switch screens. The board is cleared on any transition to a screen that is not about the
    /// board — only on the TRANSITION, because Idle() rebuilds the grid and doing that on every
    /// Rebuild would thrash it while the player clicks around a shop.
    /// </summary>
    private void Go(RunScreen screen)
    {
        bool changed = _model.Screen != screen;
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
        _seed = run.State.Seed;
        _planningTab = PlanningTab.Muster;
        _selectedCardKey = "hero:field:0";
        _inspectorOpen = false;
        _tier = FightTier.Fraying;
        _tierChosen = false;
        _hallOverview = true;
        _recommendedStation = HallStation.Breach;
        _hubAttention.Reset();
        _resultGateOpen = false;
        _lastBattle = null;
        _lastFightOutcome = null;
        _pendingHubPlan = new HubSequencePlan { RecommendedStation = HallStation.Breach };
        _fightsCompleted = 0;              // display-only; the run's own act/beat is authoritative
        _conclusionReceipt = null;
        _equipNowItemInstanceId = 0;
        _equipNowOfferIndex = -1;
        _placement.Clear();
        _deploySelected = -1;
        _selectedItem = -1;
        _selectedMarketOffer = -1;
        _feedback = "";
        _feedbackIsError = false;
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
    }

    private void ApplyShellLayoutClasses()
    {
        if (_root == null) return;
        float width = _root.resolvedStyle.width;
        float height = _root.resolvedStyle.height;
        if (width <= 0f) width = Screen.width;
        if (height <= 0f) height = Screen.height;

        bool touch = SystemInfo.deviceType == DeviceType.Handheld || Input.touchSupported;
        float diagonal = Screen.dpi > 0f
            ? Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / Screen.dpi
            : 0f;
        bool phone = _debugPhoneLayout ||
                     (SystemInfo.deviceType == DeviceType.Handheld &&
                      (diagonal <= 0f || diagonal < 8f));
        bool tablet = SystemInfo.deviceType == DeviceType.Handheld && !phone;
        _root.EnableInClassList("input--touch", touch);
        _root.EnableInClassList("layout--compact", width < 1500f || height < 820f || phone);
        _root.EnableInClassList("layout--short", height < 760f || phone);
        _root.EnableInClassList("layout--phone", phone);
        _root.EnableInClassList("layout--tablet", tablet);
        _root.EnableInClassList("motion--reduced", _reducedMotion);
    }

    // ---- model construction (the only place ids become words) ---------------------

    private void BuildModel()
    {
        BuildMenu();
        BuildRecruit();
        BuildResultGate();
        if (_run != null)
        {
            if (!_run.State.Over)
            {
                BuildPlanning();
                BuildWager();
                BuildDeploy();
            }
            BuildRunOver();
        }
    }

    private void BuildResultGate()
    {
        var result = _model.Result;
        result.Open = _resultGateOpen && _lastFightOutcome != null && _lastBattle != null;
        result.Stats = new List<ResultStatModel>();
        result.Deaths = new List<string>();
        if (!result.Open) return;

        var outcome = _lastFightOutcome;
        var summary = FightSummary.Build(_lastBattle);
        result.Victory = outcome.Won;
        result.Eyebrow = _run != null && _run.State.Over
            ? "RUN CONCLUSION"
            : $"ACT {_run.State.Act} · FIGHT RESOLVED";
        result.Heading = outcome.Won ? "VICTORY" : "THE WARBAND BREAKS";
        result.Summary = outcome.Won
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

        UnitSummary top = null;
        foreach (var unit in summary.Units)
            if (unit.Team == 0 && (top == null || unit.DamageDealt > top.DamageDealt))
                top = unit;
        result.Stats.Add(new ResultStatModel
        {
            Label = "TOP DAMAGE",
            Value = top == null ? "—" : $"{top.Name} · {top.DamageDealt}",
        });

        foreach (var beat in summary.Beats)
        {
            UnitSummary victim = summary.Unit(beat.Victim);
            if (victim == null || victim.Team != 0) continue;
            UnitSummary killer = summary.Unit(beat.Killer);
            string source = killer == null ? Lexicon.Of(beat.Cause).Name : killer.Name;
            string cause = Lexicon.Of(beat.Cause).Name;
            result.Deaths.Add($"{victim.Name} fell to {source} · {cause} · {beat.Tick / 10f:0.0}s");
            if (result.Deaths.Count >= 3) break;
        }

        if (_run.State.Over)
        {
            result.ContinueLabel = "VIEW RUN RESULT  ›";
            result.Recommendation = _run.State.Victory
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

    private static string TransactionTarget(PurchaseOutcome outcome) =>
        StationTarget(outcome switch
        {
            PurchaseOutcome.Recruit => HallStation.Warband,
            PurchaseOutcome.RankUp => HallStation.Warband,
            PurchaseOutcome.Weapon => HallStation.Armory,
            PurchaseOutcome.Trinket => HallStation.Armory,
            PurchaseOutcome.Inscription => HallStation.Hourstone,
            PurchaseOutcome.Capacity => HallStation.Warband,
            _ => HallStation.Market,
        });

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
        _model.Menu.Notice = _menuNotice;
        _model.Menu.VersionLine =
            $"First playable · {_cfg.Acts} acts × {_cfg.NodesPerAct + 1} beats · one loss ends the run";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The content fingerprint, visible only in dev builds. Without it, "my save refused to
        // load" is unfalsifiable; with it you can compare the message's stamp against the build's.
        _model.Menu.SeedLabel += $"   ·   CONTENT {_content.ContentVersion}";
#endif
    }

    private void BuildRecruit()
    {
        var r = _model.Recruit;
        r.Heading = "MUSTER YOUR WARBAND";
        r.Capacity = _cfg.StartingFieldSlots;
        r.Picked = _picked.Count;
        int left = RunSetup.PicksRemaining(_picked.Count, _cfg);
        r.Instruction = left > 0
            ? "Choose three champions. Hover a stat or rule for exact mechanics."
            : "Your warband is ready.";
        r.CanBegin = left == 0;
        r.Feedback = _recruitFeedback;
        r.FeedbackIsError = _recruitFeedbackIsError;
        r.ReducedMotion = _reducedMotion;
        r.OfferGeneration = $"{_seed}|{string.Join("|", _offer)}";
        r.Offer = _offer.Select(id =>
        {
            var card = MusterCard(id);
            card.Selected = _picked.Contains(id);
            card.CanToggle = card.Selected || _picked.Count < _cfg.StartingFieldSlots;
            card.DisabledReason = card.CanToggle ? "" : "Remove one champion first.";
            card.SelectedSlot = _picked.IndexOf(id);
            return card;
        }).ToList();
        r.Slots = new List<MusterSelectionSlotModel>();
        for (int i = 0; i < r.Capacity; i++)
        {
            string id = i < _picked.Count ? _picked[i] : "";
            PresentationCatalog.UnitPresentation presentation =
                string.IsNullOrEmpty(id) ? null : _presentation.Unit(id);
            string name = string.IsNullOrEmpty(id) ? "" : ContentLexicon.Chassis(id).Name;
            r.Slots.Add(new MusterSelectionSlotModel
            {
                Index = i,
                Filled = !string.IsNullOrEmpty(id),
                ChampionKey = id,
                Name = name,
                PortraitResource = presentation?.portrait ?? "",
                PortraitFallback = string.IsNullOrEmpty(name) ? "" : Initials(name),
                Accent = presentation?.accent ?? "",
            });
        }
        if (r.Offer.Count > 0) MusterPresentationContract.Validate(r.Offer);
    }

    private void BuildPlanning()
    {
        var p = _model.Planning;
        var s = _run.State;
        p.Act = $"ACT {s.Act} / {_cfg.Acts}";
        p.Beat = s.Phase == RunPhase.Reward
            ? "ACT BOSS CLEARED"
            : $"BEAT {s.NodeIndex + 1} / {_cfg.NodesPerAct + 1}";
        p.Sand = s.Sand.ToString();
        p.Capacity = $"{s.Field.Count} / {s.FieldSlots}";
        p.ActiveTab = _planningTab;
        p.HallOverview = _hallOverview;
        p.ActiveStation = TabStation(_planningTab);
        p.ReducedMotion = _reducedMotion;
        p.ForcePhoneLayout = _debugPhoneLayout;
        p.Feedback = _feedback;
        p.FeedbackIsError = _feedbackIsError;
        p.RerollLabel = $"REROLL · {_cfg.RerollCost} SAND";
        p.CanReroll = s.Phase == RunPhase.Planning &&
                      s.PendingSpec == null &&
                      s.Sand >= _cfg.RerollCost;
        p.CommitLabel = _run.AtBoss ? "PREPARE FOR THE BOSS  ›" : "CHOOSE NEXT WAGER  ›";
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
            ? $"A field place is unlocked: buy capacity {s.FieldSlots + 1} for {_run.SlotOfferCost} Sand."
            : "";
        p.SlotAffordable = p.SlotOfferOpen && s.Sand >= _run.SlotOfferCost;

        if (s.Phase == RunPhase.Reward)
        {
            p.BeatKind = PlanningBeat.BossReward;
            p.Heading = "THE HOUR ANSWERS";
            p.Brief = $"+{s.PendingBossSand} Sand. Bind one Inscription before Act {s.Act + 1}.";
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
                BuildInterludeBeat(p);
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
    }

    private List<HallStationModel> BuildHallStations(PlanningModel model)
    {
        var s = _run.State;
        int liveOffers = 0;
        foreach (var offer in s.ShopOffers)
            if (offer != null) liveOffers++;

        string breachStatus;
        if (s.Phase == RunPhase.Reward) breachStatus = "Claim the boss reward first";
        else if (s.PendingSpec != null) breachStatus = "Choose a specialization first";
        else if (_run.CurrentNodeKind == NodeKind.Boss) breachStatus = "The act boss waits";
        else if (_run.CurrentNodeKind == NodeKind.Event) breachStatus = "An Interlude lies ahead";
        else breachStatus = "Set the next wager";

        return new List<HallStationModel>
        {
            new HallStationModel
            {
                Station = HallStation.Breach,
                Eyebrow = "NEXT BEAT",
                Name = "BREACH",
                Status = breachStatus,
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
        for (int i = 0; i <= _cfg.NodesPerAct; i++)
        {
            string kind = i == _cfg.NodesPerAct
                ? "Boss"
                : s.ActMaps[s.Act - 1][i] == NodeKind.Event ? "Interlude" : "Fight";
            result.Add(new PlanningTrackNodeModel
            {
                Label = kind == "Boss" ? "BOSS" : (i + 1).ToString(),
                Kind = kind,
                State = i < s.NodeIndex ? "past" : i == s.NodeIndex ? "current" : "future",
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
                model.Brief = "Spend Sand on visible stock. Select any offer for full rules, price, and purchase actions.";
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
            Reward = $"+{_cfg.FightReward(s.Act, entry.Item1)} SAND ON VICTORY",
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
            : "A loss ends the run and pays no Sand.";
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
        return new CardModel
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

    private SpecChoiceModel BuildSpecChoice()
    {
        var pending = _run.State.PendingSpec;
        if (pending == null) return new SpecChoiceModel();
        HeroInstance hero = HeroAt(pending);
        UnitDef current = ComposeHero(hero);
        HeroInstance choiceA = hero.Clone();
        choiceA.SpecNodeIds.Add(pending.OptionA);
        HeroInstance choiceB = hero.Clone();
        choiceB.SpecNodeIds.Add(pending.OptionB);
        MechanicalRule ruleA = MechanicalRulePresenter.Node(_content.Node(pending.OptionA));
        MechanicalRule ruleB = MechanicalRulePresenter.Node(_content.Node(pending.OptionB));
        return new SpecChoiceModel
        {
            Pending = true,
            HeroName = ContentLexicon.Chassis(hero.ChassisId).Name,
            RankLabel = $"RANK {pending.ForRank}",
            OptionAName = ContentLexicon.Node(pending.OptionA).Name,
            OptionAText = ruleA.Full,
            OptionAChange = ruleA.Change.ToString().ToUpperInvariant(),
            OptionAComparisons = ChangedFacts(current, ComposeHero(choiceA)),
            OptionBName = ContentLexicon.Node(pending.OptionB).Name,
            OptionBText = ruleB.Full,
            OptionBChange = ruleB.Change.ToString().ToUpperInvariant(),
            OptionBComparisons = ChangedFacts(current, ComposeHero(choiceB)),
        };
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
        card.Tags = hero.SpecNodeIds.Select(id => ContentLexicon.Node(id).Name).ToList();
        card.Selected = card.Key == _selectedCardKey;
        return card;
    }

    private CardModel UnitCardFromDef(UnitDef def, string key, string eyebrow, string rank,
                                      bool owned)
    {
        var id = string.IsNullOrEmpty(def.ChassisId) ? "" : def.ChassisId;
        var presentation = _presentation.Unit(id);
        string title = string.IsNullOrEmpty(id) ? def.Name : ContentLexicon.Chassis(id).Name;
        return new CardModel
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
            AbilityIcon = presentation.abilityIcon,
            AbilityTrigger = SignatureTrigger(def),
            AbilityName = presentation.abilityName,
            AbilitySummary = presentation.abilitySummary,
            InspectorAbilitySummary = presentation.abilitySummary,
            PassiveIcon = presentation.passiveIcon,
            PassiveTrigger = presentation.passiveTrigger,
            PassiveName = presentation.passiveName,
            PassiveSummary = presentation.passiveSummary,
            KeywordNotes = presentation.keywords == null
                ? new List<string>()
                : presentation.keywords.ToList(),
            Stats = StatChips(def),
            Selected = key == _selectedCardKey,
        };
    }

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
                card = TryOwnedHero(offer.Id, out HeroInstance owned)
                    ? RankUpCard(owned, key)
                    : UnitCardFromDef(
                        Loadout.Compose(_content.Chassis(offer.Id)).Def,
                        key,
                        "RECRUIT",
                        "",
                        false);
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
        card.Price = $"{offer.Price} SAND";
        card.Frozen = offer.Frozen;
        // Affordability disables BUY, never inspection. The Market card remains selectable so
        // the player can learn why an offer is worth saving for.
        card.Disabled = false;
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

        int shortfall = Mathf.Max(0, offer.Price - _run.State.Sand);
        return new MarketOfferCardModel
        {
            Key = detail.Key,
            ContentId = detail.ContentId,
            Kind = kind,
            Classification = classification,
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
            Price = $"{offer.Price} SAND",
            EconomyState = shortfall == 0 ? "AVAILABLE" : "SHORT",
            Selected = detail.Selected,
            Affordable = shortfall == 0,
            Frozen = offer.Frozen,
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
        HeroInstance guaranteedHero = hero.Clone();
        guaranteedHero.Rank++;
        UnitDef guaranteed = ComposeHero(guaranteedHero);
        var options = _content.SpecOptions(hero.ChassisId, guaranteedHero.Rank, hero.PathId);
        ChoicePreviewModel optionA = RankChoicePreview(guaranteedHero, options.A);
        ChoicePreviewModel optionB = RankChoicePreview(guaranteedHero, options.B);
        var presentation = _presentation.Unit(hero.ChassisId);
        var card = new CardModel
        {
            Key = key,
            ContentId = hero.ChassisId,
            Eyebrow = "RANK UP",
            Title = ContentLexicon.Chassis(hero.ChassisId).Name,
            Subtitle = $"{hero.Rank} → {guaranteedHero.Rank} · CHOOSE 1 OF 2",
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
                $"{Signed(guaranteed.Attack - current.Attack)} basic power. Then choose 1 of 2 specializations.",
            PassiveIcon = "◇",
            PassiveTrigger = "SPECIALIZATION · CHOOSE 1 OF 2",
            PassiveName = $"{ContentLexicon.Node(options.A).Name}  OR  {ContentLexicon.Node(options.B).Name}",
            PassiveSummary = "Both exact paths are previewed in the dossier before purchase.",
            Stats = new List<StatChipModel>
            {
                new StatChipModel("RANK", $"{hero.Rank} → {guaranteedHero.Rank}", "warn",
                    PresentationFactId.Rank, "Current and resulting rank."),
                new StatChipModel("HP", Signed(guaranteed.MaxHp - current.MaxHp), "good",
                    PresentationFactId.Hp, "Guaranteed maximum HP gained before the choice."),
                new StatChipModel("POWER", Signed(guaranteed.Attack - current.Attack), "good",
                    PresentationFactId.BasicPower,
                    "Guaranteed basic attack power gained before the choice."),
                new StatChipModel("CHOICE", "1 OF 2", "warn",
                    PresentationFactId.ChoiceCount,
                    "A blocking specialization choice follows the purchase."),
            },
            ComparisonTitle = "GUARANTEED RANK GAIN",
            Comparisons = ChangedFacts(current, guaranteed),
            ChoicePreviews = new List<ChoicePreviewModel> { optionA, optionB },
            Selected = key == _selectedCardKey,
        };
        return card;
    }

    private ChoicePreviewModel RankChoicePreview(HeroInstance guaranteedHero, string nodeId)
    {
        UnitDef before = ComposeHero(guaranteedHero);
        HeroInstance chosen = guaranteedHero.Clone();
        chosen.SpecNodeIds.Add(nodeId);
        UnitDef after = ComposeHero(chosen);
        MechanicalRule rule = MechanicalRulePresenter.Node(_content.Node(nodeId));
        return new ChoicePreviewModel
        {
            Change = rule.Change.ToString().ToUpperInvariant(),
            Name = ContentLexicon.Node(nodeId).Name,
            Rule = rule.Full,
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
        card.Eyebrow = item.Kind == ItemKind.Weapon ? "ARMORY · WEAPON" : "ARMORY · TRINKET";
        card.Selected = key == _selectedCardKey;
        card.Pinned = index == _selectedItem;
        return card;
    }

    private CardModel InscriptionCard(string id, int index, bool offered)
    {
        string key = offered ? $"reward:{index}" : $"inscription:{index}";
        var presentation = _presentation.Content(id);
        MechanicalRule rules = MechanicalRulePresenter.Inscription(_content.Banner(id));
        return new CardModel
        {
            Key = key,
            ContentId = id,
            Eyebrow = offered ? "INSCRIPTION" : "BOUND INSCRIPTION",
            Title = _content.Banner(id).Name.Replace("Banner", "Inscription"),
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
            Price = $"{_run.SlotOfferCost} SAND",
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
            Price = $"{_run.SlotOfferCost} SAND",
            EconomyState = shortfall == 0 ? "AVAILABLE" : "SHORT",
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
        var all = AllPlanningCards(p);
        CardModel selected = all.FirstOrDefault(c => c.Key == _selectedCardKey);
        if (selected == null)
        {
            selected = p.Field.FirstOrDefault() ?? p.Market.FirstOrDefault() ??
                       p.Armory.FirstOrDefault() ?? p.Inscriptions.FirstOrDefault();
            _selectedCardKey = selected?.Key ?? "";
        }
        foreach (var card in all) card.Selected = card.Key == _selectedCardKey;
        foreach (var offer in p.MarketOffers)
            offer.Selected = offer.Key == _selectedCardKey;
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

    private InspectorModel BuildInspector(PlanningModel p)
    {
        var card = AllPlanningCards(p).FirstOrDefault(c => c.Key == _selectedCardKey);
        if (card == null) return new InspectorModel { Empty = true };

        var inspector = new InspectorModel
        {
            Key = card.Key,
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
            Price = card.Price,
            Stats = new List<StatChipModel>(card.Stats),
            Tags = new List<string>(card.Tags),
            KeywordNotes = new List<string>(card.KeywordNotes),
            WeaponName = card.Weapon,
            WeaponSummary = string.IsNullOrEmpty(card.WeaponSummary)
                ? WeaponInspectorSummary(card)
                : card.WeaponSummary,
            ComparisonTitle = card.ComparisonTitle,
            Comparisons = new List<StatComparisonModel>(card.Comparisons),
            ChoicePreviews = new List<ChoicePreviewModel>(card.ChoicePreviews),
        };

        if (!string.IsNullOrEmpty(card.PassiveName))
        {
            inspector.PassiveIcon = card.PassiveIcon;
            inspector.PassiveTrigger = card.PassiveTrigger;
            inspector.PassiveName = card.PassiveName;
            inspector.PassiveSummary = card.PassiveSummary;
        }
        else
        {
            inspector.PassiveIcon = "◇";
            inspector.PassiveTrigger = "DETAIL";
            inspector.PassiveName = "CARD TYPE";
            inspector.PassiveSummary = card.Subtitle;
        }

        if (TrySimpleIndex(card.Key, "market", out var offerIndex))
        {
            var offer = _run.State.ShopOffers[offerIndex];
            if (offer != null)
            {
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Buy,
                    Label = _run.State.Sand >= offer.Price ? $"BUY · {offer.Price} SAND" : $"NEED {offer.Price} SAND",
                    Primary = true,
                    Enabled = _run.State.Sand >= offer.Price,
                    DisabledReason = _run.State.Sand >= offer.Price
                        ? ""
                        : $"{offer.Price - _run.State.Sand} more Sand required.",
                });
                inspector.Actions.Add(new InspectorActionModel
                {
                    Id = HallActionId.Freeze,
                    Label = offer.Frozen ? "RELEASE STOCK" : "HOLD STOCK · FREE",
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
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.BuySlot,
                Label = $"UNLOCK · {_run.SlotOfferCost} SAND",
                Primary = true,
                Enabled = _run.State.Sand >= _run.SlotOfferCost,
                DisabledReason = _run.State.Sand >= _run.SlotOfferCost
                    ? ""
                    : $"{_run.SlotOfferCost - _run.State.Sand} more Sand required.",
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
                      $"{(hero.WeaponTier + 1).ToString().ToUpperInvariant()} · {forgeCost} SAND"
                    : $"FORGE CEILING · {ceiling.ToString().ToUpperInvariant()}",
                Enabled = canAffordForge,
                DisabledReason = !canTemper
                    ? $"Act {_run.State.Act} forge ceiling is {ceiling}."
                    : $"{forgeCost - _run.State.Sand} more Sand required.",
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
                    : $"DISMISS · {hero.GoldSpent * _cfg.SellPct / 100} SAND",
            });
        }
        else if (TrySimpleIndex(card.Key, "item", out var itemIndex))
        {
            var item = _run.State.Inventory[itemIndex];
            inspector.Actions.Add(new InspectorActionModel
            {
                Id = HallActionId.SellItem,
                Label = $"SELL · {item.SandInvested * _cfg.SellPct / 100} SAND",
            });
        }
        return inspector;
    }

    private void BuildEquipmentComparison(InspectorModel inspector, HeroInstance hero, ItemRef item)
    {
        UnitDef before = ComposeHero(hero);
        HeroInstance preview = hero.Clone();
        string itemName;
        if (item.Kind == ItemKind.Weapon)
        {
            preview.WeaponId = item.Id;
            preview.WeaponTier = item.Tier;
            itemName = _content.Weapon(item.Id).Name;
        }
        else
        {
            preview.TrinketIds.Clear();
            preview.TrinketIds.Add(item.Id);
            itemName = _content.Trinket(item.Id).Name;
        }
        UnitDef after = ComposeHero(preview);

        inspector.ComparisonTitle = "EQUIP PREVIEW · " + itemName.ToUpperInvariant();
        AddComparison(inspector, "HP", before.MaxHp.ToString(), after.MaxHp.ToString(),
            after.MaxHp > before.MaxHp);
        AddComparison(inspector, "BASIC POWER",
            before.HealAutos ? $"{before.Attack} HEAL" : $"{before.Attack} DMG",
            after.HealAutos ? $"{after.Attack} HEAL" : $"{after.Attack} DMG",
            after.Attack > before.Attack);
        AddComparison(inspector, "REACH", before.Range.ToString(), after.Range.ToString(),
            after.Range > before.Range);
        AddComparison(inspector, "CADENCE",
            $"{before.AttackInterval / 10f:0.0}s",
            $"{after.AttackInterval / 10f:0.0}s",
            after.AttackInterval < before.AttackInterval);
        AddComparison(inspector, "MANA / SWING",
            before.ManaPerSwing.ToString(), after.ManaPerSwing.ToString(),
            after.ManaPerSwing > before.ManaPerSwing);
        AddComparison(inspector, "SIGNATURE MANA",
            before.ManaMax.ToString(), after.ManaMax.ToString(),
            after.ManaMax < before.ManaMax);
        if (before.CritChance > 0 || after.CritChance > 0)
            AddComparison(inspector, "CRIT", $"{before.CritChance}%", $"{after.CritChance}%",
                after.CritChance > before.CritChance);
        if (before.CleavePct > 0 || after.CleavePct > 0)
            AddComparison(inspector, "CLEAVE", $"{before.CleavePct}%", $"{after.CleavePct}%",
                after.CleavePct > before.CleavePct);
    }

    private static void AddComparison(InspectorModel inspector, string label,
                                      string before, string after, bool improved)
    {
        inspector.Comparisons.Add(new StatComparisonModel
        {
            Label = label,
            Before = before,
            After = after,
            Tone = before == after ? "" : improved ? "good" : "bad",
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
            });
        if (before.CleavePct != after.CleavePct)
            result.Add(new StatComparisonModel
            {
                Label = "CLEAVE",
                Before = before.CleavePct + "%",
                After = after.CleavePct + "%",
                Tone = after.CleavePct > before.CleavePct ? "good" : "",
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
                m.NodeHeading = "A QUIET STRETCH";
                m.NodeBlurb = "No one contests the road. Take the coin and move on.";
                m.PrimaryText = "TRAVEL ON";
                break;
            case NodeKind.Boss:
            {
                // Per-act bosses (ADR 0024): the heading, rule and roster all come off the brief.
                // This previously hardcoded "THE LAST OATH" and Encounters.BondedPair(), which was
                // only ever true because every act fielded the same boss.
                var bossBrief = BriefForCurrentNode();
                m.NodeHeading = bossBrief == null ? "THE ACT BOSS" : bossBrief.Name.ToUpperInvariant();
                m.NodeBlurb = "The act will not let you past without this. Formation and rules are final — there is no hidden phase.";
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
        sh.Feedback = _feedback;
        sh.FeedbackIsError = _feedbackIsError;
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
        sh.SpecChoice = pending == null
            ? new SpecChoiceModel()
            : new SpecChoiceModel
            {
                Pending = true,
                HeroName = ContentLexicon.Chassis(HeroAt(pending).ChassisId).Name,
                RankLabel = $"RANK {pending.ForRank}",
                OptionAName = ContentLexicon.Node(pending.OptionA).Name,
                OptionAText = ContentLexicon.Node(pending.OptionA).Text,
                OptionBName = ContentLexicon.Node(pending.OptionB).Name,
                OptionBText = ContentLexicon.Node(pending.OptionB).Text,
            };
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
        d.Feedback = _feedback;
        d.FeedbackIsError = _feedbackIsError;

        d.Instruction = _deploySelected >= 0
            ? "Click a hex in your half to place them. Clicking an occupied hex swaps the two."
            : d.CanCommit
                ? "Formation set. Move anyone you like, or lock it in."
                : "Pick a champion, then click a hex in your half.";

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

        d.EnemyPreview = new List<string>();
        d.EncounterRule = "";
        if (s.Phase == RunPhase.Planning)
        {
            // Every fight discloses its rule, not just the boss, and the roster line names the
            // authored monster rather than the hero silhouette it borrows.
            var brief = BriefForCurrentNode();
            if (brief != null)
            {
                d.EncounterRule = $"{brief.RuleName} — {brief.RuleText}";
                foreach (var u in brief.Units)
                    d.EnemyPreview.Add($"{u.Name} · {u.Role} · {u.MaxHp} HP · reach {u.Range} · row {u.Row}");
            }
            else
            {
                foreach (var e in EnemiesForCurrentNode())
                    d.EnemyPreview.Add($"{e.Def.Name} · {e.Def.MaxHp} HP · reach {e.Def.Range} · row {e.Pos.Row}");
            }
        }
    }

    private void BuildRunOver()
    {
        var o = _model.RunOver;
        var s = _run.State;
        bool won = s.Victory;
        o.Tone = won ? RunOverTone.Victory : RunOverTone.Defeat;
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
        o.FinalWarband = s.Field.Select(HeroCard).ToList();
    }

    // ---- hydration helpers -------------------------------------------------------

    private InspectorModel PlaybackInspector(PlaybackUnit unit)
    {
        string id = unit.ChassisId ?? "";
        var presentation = _presentation.Unit(id);
        string role = string.IsNullOrEmpty(id) ? "COMBATANT" : presentation.role;
        var stats = new List<StatChipModel>
        {
            new StatChipModel("HP", $"{unit.Hp} / {unit.MaxHp}",
                unit.Hp * 3 <= unit.MaxHp ? "bad" : ""),
            new StatChipModel(unit.HealAutos ? "HEAL" : "ATK", unit.Attack.ToString(),
                unit.HealAutos ? "good" : ""),
            new StatChipModel("REACH", unit.Range.ToString(), unit.Range >= 3 ? "good" : ""),
            new StatChipModel("SPEED", $"{unit.AttackInterval / 10f:0.0}s"),
        };
        if (unit.Shield > 0) stats.Add(new StatChipModel("SHIELD", unit.Shield.ToString(), "good"));
        if (unit.ManaMax > 0) stats.Add(new StatChipModel("MANA", $"{unit.Mana} / {unit.ManaMax}"));
        if (unit.CritChance > 0) stats.Add(new StatChipModel("CRIT", $"{unit.CritChance}%", "warn"));

        var notes = presentation.keywords == null
            ? new List<string>()
            : presentation.keywords.ToList();
        foreach (var status in unit.Statuses)
        {
            var lex = Lexicon.Of(status.Kind);
            string magnitude = status.Mag == 0 ? "" : $" {status.Mag}";
            notes.Add($"{lex.Name.ToUpperInvariant()}{magnitude} · {lex.Text}");
        }

        return new InspectorModel
        {
            Eyebrow = (unit.Team == 0 ? "ALLY" : "ENEMY") + " · " + role,
            Title = string.IsNullOrEmpty(unit.Name) ? "Unknown Combatant" : unit.Name,
            Subtitle = unit.Dead ? "DEFEATED" : $"LIVE COMBAT · {unit.Hp} HP REMAINING",
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(unit.Name),
            Accent = presentation.accent,
            AbilityIcon = presentation.abilityIcon,
            AbilityTrigger = unit.ManaMax > 0
                ? $"SIGNATURE · AT {unit.ManaMax} MANA"
                : "SIGNATURE",
            AbilityName = presentation.abilityName,
            AbilitySummary = presentation.abilitySummary,
            PassiveIcon = presentation.passiveIcon,
            PassiveTrigger = presentation.passiveTrigger,
            PassiveName = presentation.passiveName,
            PassiveSummary = presentation.passiveSummary,
            WeaponName = unit.WeaponName,
            WeaponSummary = BasicAttackSummary(unit),
            Stats = stats,
            Tags = unit.Traits.Select(value => ContentLexicon.Node(value).Name).ToList(),
            KeywordNotes = notes,
        };
    }

    private MusterCardModel MusterCard(string chassisId)
    {
        var lex = ContentLexicon.Chassis(chassisId);
        PresentationCatalog.UnitPresentation presentation = _presentation.Unit(chassisId);
        ChassisDef chassis = _content.Chassis(chassisId);
        UnitDef def = Loadout.Compose(chassis).Def;

        string role = string.IsNullOrWhiteSpace(presentation.musterRole)
            ? presentation.role
            : presentation.musterRole;
        UiGlyphId roleIcon = UiGlyphCatalog.Parse(
            presentation.musterRoleIcon, DefaultMusterRoleIcon(chassisId));
        string signatureKeyword = string.IsNullOrWhiteSpace(
            presentation.musterSignatureKeyword)
            ? "Special"
            : presentation.musterSignatureKeyword;
        string passiveKeyword = string.IsNullOrWhiteSpace(
            presentation.musterPassiveKeyword)
            ? "Innate"
            : presentation.musterPassiveKeyword;

        var passiveTriggers = new List<Trigger>(chassis.Passives);
        var signatureTriggers = new List<Trigger>();
        if (chassis.Signature.Count == 0)
        {
            // A signature can be authored as an on-cast trigger (Frenzy). When there is no direct
            // effect, that trigger is the signature payload, not a second passive.
            for (int i = passiveTriggers.Count - 1; i >= 0; i--)
                if (passiveTriggers[i].On == EventKind.Cast)
                {
                    signatureTriggers.Insert(0, passiveTriggers[i]);
                    passiveTriggers.RemoveAt(i);
                }
        }

        string signatureRule = chassis.Signature.Count > 0
            ? MechanicalRulePresenter.Signature(chassis.Signature)
            : MechanicalRulePresenter.Passives(signatureTriggers);
        string passiveRule = MechanicalRulePresenter.Passives(
            passiveTriggers, chassis.StatRules);
        int? baseAttacks = MechanicalRulePresenter.BasicAttacksToSignature(def);
        string signatureContext = def.ManaMax > 0 ? $"{def.ManaMax} MANA" : "SPECIAL";
        string signatureAdvanced = def.ManaMax <= 0
            ? ""
            : def.ManaPerSwing <= 0
                ? $"Costs {def.ManaMax} Mana. This starting kit does not gain Mana from " +
                  "ordinary basic attacks."
                : $"Costs {def.ManaMax} Mana and gains {def.ManaPerSwing} Mana when a basic " +
                  $"attack resolves." + (baseAttacks.HasValue
                    ? $" Baseline: ready after {baseAttacks.Value} basic " +
                      $"hit{(baseAttacks.Value == 1 ? "" : "s")} before modifiers."
                    : "");

        string basicVerb = def.HealAutos ? "Heal" : "Damage";
        var basicDetails = new List<string>
        {
            $"{def.WeaponName}.",
            def.HealAutos
                ? $"Each basic attack heals the lowest-HP ally for {def.Attack}."
                : $"Each basic attack deals {def.Attack} damage.",
            $"It completes every {def.AttackInterval / 10f:0.0}s and grants " +
            $"{def.ManaPerSwing} Mana.",
        };
        if (def.CritChance > 0)
            basicDetails.Add($"Basic attacks have {def.CritChance}% critical-hit chance.");
        if (def.CleavePct > 0)
            basicDetails.Add(
                $"They also deal {def.CleavePct}% damage to enemies adjacent to the target.");

        return new MusterCardModel
        {
            Key = chassisId,
            Name = lex.Name,
            Role = role,
            RoleIcon = roleIcon,
            PortraitResource = presentation.portrait,
            PortraitFallback = Initials(lex.Name),
            Accent = presentation.accent,
            Facts = new List<MusterFactModel>
            {
                new MusterFactModel
                {
                    Id = PresentationFactId.Hp,
                    Kind = MusterFactKind.Health,
                    Icon = UiGlyphId.Health,
                    Value = def.MaxHp.ToString(),
                    AccessibleLabel = $"Health {def.MaxHp}",
                    TooltipTitle = $"HEALTH  {def.MaxHp}",
                    TooltipBody = "Maximum combat HP.",
                },
                new MusterFactModel
                {
                    Id = PresentationFactId.BasicPower,
                    Kind = MusterFactKind.Basic,
                    Icon = def.HealAutos ? UiGlyphId.Heal : UiGlyphId.Damage,
                    SecondaryIcon = UiGlyphId.Cadence,
                    Value = def.Attack.ToString(),
                    SecondaryValue = $"{def.AttackInterval / 10f:0.0}s",
                    AccessibleLabel =
                        $"{basicVerb} {def.Attack} every {def.AttackInterval / 10f:0.0} seconds",
                    TooltipTitle =
                        $"BASIC {basicVerb.ToUpperInvariant()}  {def.Attack}",
                    TooltipBody = string.Join(" ", basicDetails),
                },
                new MusterFactModel
                {
                    Id = PresentationFactId.Reach,
                    Kind = MusterFactKind.Reach,
                    Icon = UiGlyphId.Reach,
                    Value = def.Range.ToString(),
                    AccessibleLabel = $"Reach {def.Range}",
                    TooltipTitle = $"REACH  {def.Range}",
                    TooltipBody =
                        $"Basic attacks can resolve against targets up to {def.Range} " +
                        $"hex{(def.Range == 1 ? "" : "es")} away.",
                },
            },
            Signature = new MusterRuleModel
            {
                Kind = MusterRuleKind.Signature,
                Icon = UiGlyphId.Signature,
                KeywordIcon = UiGlyphCatalog.Keyword(signatureKeyword),
                Name = presentation.abilityName,
                Keyword = signatureKeyword,
                Context = signatureContext,
                ExactRule = signatureRule,
                AdvancedRule = signatureAdvanced,
                ManaCost = def.ManaMax > 0 ? def.ManaMax : -1,
                KeywordNotes = MusterKeywordNotes(
                    presentation.keywords, signatureKeyword),
            },
            Passive = new MusterRuleModel
            {
                Kind = MusterRuleKind.Passive,
                Icon = UiGlyphId.Passive,
                KeywordIcon = UiGlyphCatalog.Keyword(passiveKeyword),
                Name = presentation.passiveName,
                Keyword = passiveKeyword,
                Context = "ALWAYS ON",
                ExactRule = passiveRule,
                KeywordNotes = MusterKeywordNotes(
                    presentation.keywords, passiveKeyword),
            },
        };
    }

    private static UiGlyphId DefaultMusterRoleIcon(string chassisId) =>
        chassisId switch
        {
            "cleric" => UiGlyphId.Healer,
            "bulwark" => UiGlyphId.Tank,
            "shade" => UiGlyphId.Diver,
            "sharpshot" => UiGlyphId.Sniper,
            "pyromancer" => UiGlyphId.Zoner,
            "berserker" => UiGlyphId.Bruiser,
            "phalanx" => UiGlyphId.Frontline,
            "banneret" => UiGlyphId.Captain,
            _ => UiGlyphId.Frontline,
        };

    private static List<string> MusterKeywordNotes(
        IReadOnlyList<string> notes, string keyword)
    {
        var result = new List<string>();
        if (notes == null || string.IsNullOrWhiteSpace(keyword)) return result;
        string prefix = keyword.Trim().ToUpperInvariant();
        foreach (string note in notes)
            if (!string.IsNullOrWhiteSpace(note) &&
                note.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(note);
        return result;
    }

    private HeroCardModel ChassisCard(string chassisId)
    {
        var lex = ContentLexicon.Chassis(chassisId);
        var presentation = _presentation.Unit(chassisId);
        var def = Loadout.Compose(_content.Chassis(chassisId)).Def;
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
            AbilityName = presentation.abilityName,
            AbilitySummary = presentation.abilitySummary,
            PassiveIcon = presentation.passiveIcon,
            PassiveTrigger = presentation.passiveTrigger,
            PassiveName = presentation.passiveName,
            PassiveSummary = presentation.passiveSummary,
            WeaponSummary = BasicAttackSummary(def),
            KeywordNotes = presentation.keywords == null
                ? new List<string>()
                : presentation.keywords.ToList(),
            Stats = StatChips(def),
        };
    }

    private HeroCardModel HeroCard(HeroInstance hero)
    {
        var card = ChassisCard(hero.ChassisId);
        card.RankLabel = $"RANK {hero.Rank}";
        card.Traits = hero.SpecNodeIds.Select(id => ContentLexicon.Node(id).Name).ToList();

        var def = ComposeHero(hero);
        card.WeaponName = def.WeaponName;
        card.WeaponSummary = BasicAttackSummary(def);
        card.AbilityTrigger = SignatureTrigger(def);
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
                "", PresentationFactId.BasicPower,
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
            default: return _content.Banner(o.Id).Name;
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
    public void EditorNewRun() => _actions.NewRun?.Invoke();

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
        return _run != null && _model.Screen == RunScreen.Management;
    }

    public void EditorSetPlanningTab(int tab) => _actions.SetPlanningTab?.Invoke(tab);

    public void EditorAdvanceFromManagement() => _actions.Advance?.Invoke();

    public void EditorChooseWager(int tier) => _actions.ChooseTier?.Invoke(tier);

    public void EditorConfirmWager() => _actions.ConfirmWager?.Invoke();

    public void EditorSelectPlanningCard(string key) => _actions.SelectPlanningCard?.Invoke(key);

    public void EditorCommitDeployment() => _actions.CommitDeployment?.Invoke();

    public bool EditorInspectFirstCombatUnit()
    {
        if (_model.Screen != RunScreen.Fight || _player == null) return false;
        for (int y = 80; y < Screen.height - 80; y += 30)
            for (int x = 80; x < Screen.width - 80; x += 30)
            {
                var unit = _player.PickUnit(new Vector2(x, y), 22f);
                if (unit == null) continue;
                _fightInspectedUnit = unit;
                _fightInspector.Bind(PlaybackInspector(unit));
                _fightInspectorScrim.style.display = DisplayStyle.Flex;
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
            if (view is ManagementView management)
                return management.EditorValidateMarketOfferLayout();
        return false;
    }

    public bool EditorValidateMusterLayout()
    {
        MusterPresentationContract.Validate(_model.Recruit.Offer);
        RecruitView recruit = RecruitViewInstance();
        return recruit != null && recruit.ValidateResolvedLayout();
    }

    public int EditorMusterActiveEffectCount() =>
        RecruitViewInstance()?.EditorActiveEffectCount ?? -1;

    public void EditorPreviewMusterLens(int cardIndex, int lens)
    {
        if (_model.Screen != RunScreen.Recruit) _actions.NewRun?.Invoke();
        RecruitViewInstance()?.PreviewLens(cardIndex, (MusterLensTarget)lens);
    }

    public void EditorPreviewMusterReveal()
    {
        if (_model.Screen != RunScreen.Recruit) _actions.NewRun?.Invoke();
        RecruitViewInstance()?.PreviewReveal();
    }

    public void EditorPreviewMusterBlocked()
    {
        if (_model.Screen != RunScreen.Recruit) _actions.NewRun?.Invoke();
        RecruitViewInstance()?.PreviewBlocked();
    }

    public void EditorPreviewMusterReady()
    {
        if (_model.Screen != RunScreen.Recruit) _actions.NewRun?.Invoke();
        RecruitViewInstance()?.PreviewReady();
    }

    public void EditorNewMusterOffer()
    {
        if (_model.Screen != RunScreen.Recruit) _actions.NewRun?.Invoke();
        else _actions.RerollSeed?.Invoke();
    }

    public void EditorPreviewUiEffect(int cue) =>
        UiPolishSignals.Preview((UiPolishSignals.Cue)cue);

    public void EditorPreviewUiTransaction(int transaction) =>
        UiPolishSignals.Preview((UiTransactionKind)transaction);

    public void EditorOpenHallOverview() => OpenHallOverview();

    public void EditorOpenHallStation(int station) => OpenHallStation((HallStation)station);

    public int EditorHallActiveEffectCount()
    {
        foreach (var view in _views)
            if (view is ManagementView management)
                return management.EditorActiveEffectCount;
        return -1;
    }

    public bool EditorOpenResultGate()
    {
        if (_lastBattle == null || _lastFightOutcome == null) return false;
        Go(RunScreen.Fight);
        OpenFightResult();
        return true;
    }

    public void EditorWatchFightAgain() => WatchFightAgain();

    public void EditorContinueFightResult() => ContinueFightResult();

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
