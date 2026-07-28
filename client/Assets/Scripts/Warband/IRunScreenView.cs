using System;
using UnityEngine.UIElements;

/// <summary>
/// One screen of the run shell. Views are pure renderers: they build their element tree once,
/// then re-render from the model on every Bind. A view NEVER stores game state, never reaches
/// into Warband.Run, and never formats a raw content id — the controller hydrates first.
///
/// Adding a screen: implement this, add a RunScreen case, register it in RunShell. The router
/// shows exactly one view at a time and does not care what any of them contain.
/// </summary>
internal interface IRunScreenView
{
    RunScreen Screen { get; }

    /// <summary>Built once in the constructor. The router parents it and toggles visibility.</summary>
    VisualElement Root { get; }

    /// <summary>Re-render from the model. Must be idempotent and safe to call every frame-ish.</summary>
    void Bind(RunShellModel model);
}

/// <summary>
/// Optional presentation lifecycle for views with scheduled reveals, hover disclosure, or FX.
/// Screen exit is a cancellation boundary: no outgoing animation may survive into the next view.
/// </summary>
internal interface IRunScreenLifecycle
{
    void OnScreenEntered();
    void OnScreenExited();
}

/// <summary>
/// Everything a view can ASK for. Views raise intents; the controller decides whether they are
/// legal and what they mean. This is the only direction data flows out of a view — which is why
/// no view needs a reference to RunShell, and why any screen can be tested or restyled alone.
///
/// Every handler is null-safe by construction (the controller assigns all of them at startup),
/// but views should still use `?.Invoke` so a partially-wired shell degrades instead of throwing
/// inside a UI callback, where exceptions are easy to lose.
/// </summary>
internal sealed class RunShellActions
{
    // Menu
    public Action NewRun = () => { };
    public Action ContinueRun = () => { };
    public Action OpenOptions = () => { };
    public Action Quit = () => { };

    // Recruit
    public Action<string> ToggleRecruit = _ => { };
    public Action RerollSeed = () => { };
    public Action BeginRun = () => { };

    // Persistent Planning workspace
    public Action<int> SetPlanningTab = _ => { };
    public Action<string> SelectPlanningCard = _ => { };
    public Action<string> SelectComparisonTarget = _ => { };
    public Action OpenInspector = () => { };
    public Action CloseInspector = () => { };
    public Action BuySelectedOffer = () => { };
    public Action<HallActionId> InspectorAction = _ => { };
    public Action<int, int> ChooseInterlude = (_, __) => { };
    public Action<int> ChooseBossReward = _ => { };
    public Action OpenHallOverview = () => { };
    public Action<int> OpenHallStation = _ => { };
    public Action<string> OpenLoadout = _ => { };
    public Action CloseLoadout = () => { };
    public Action<string> SelectLoadoutHero = _ => { };
    public Action<string> SelectLoadoutItem = _ => { };

    // Shell-owned Warband Bar. Stable identities are mandatory because roster indices move.
    public Action<long> FocusWarbandHero = _ => { };
    public Action<long> ManageWarbandHero = _ => { };
    public Action<long, bool, int> MoveWarbandHero = (_, __, ___) => { };
    public Action<long, int> SelectWarbandEquipment = (_, __) => { };
    public Action<long, int, long> TransferWarbandEquipment = (_, __, ___) => { };
    public Action<long, int> UnequipWarbandEquipment = (_, __) => { };
    public Action<long> EquipSelectedWarbandItem = _ => { };

    // Map / node
    public Action<int> ChooseTier = _ => { };
    public Action Advance = () => { };          // resolve the current node (fight, event, or boss)
    public Action ConfirmWager = () => { };
    public Action ReturnToManagement = () => { };
    public Action WatchFightAgain = () => { };
    public Action ContinueFightResult = () => { };

    // Deploy — the board handles hex clicks; these are the roster-side intents.
    public Action<int> SelectForDeploy = _ => { };   // field index; -1 clears
    public Action ClearDeployment = () => { };
    public Action CommitDeployment = () => { };
    /// <summary>A click on the board during deployment, in PANEL coordinates. The controller
    /// owns the panel→screen→hex conversion; the view must not know the board exists.</summary>
    public Action<UnityEngine.Vector2> BoardClicked = _ => { };

    // Shop
    public Action<int> BuyOffer = _ => { };
    public Action<int> SelectItem = _ => { };            // inventory index; -1 clears
    public Action<bool, int> EquipSelected = (_, __) => { };  // (inBench, heroIndex)
    public Action<bool, int> UnequipWeapon = (_, __) => { };
    public Action<bool, int> Reforge = (_, __) => { };
    public Action<bool, int> SellHero = (_, __) => { };
    public Action<bool, int> MoveHero = (_, __) => { };   // field <-> bench
    public Action<int> SellItem = _ => { };
    public Action<int> ToggleFreeze = _ => { };
    public Action Reroll = () => { };
    public Action<int> ChooseSpec = _ => { };   // 0 = A, 1 = B
    public Action BuySlot = () => { };
    public Action LeaveShop = () => { };

    // Terminal
    public Action BackToMenu = () => { };
}
