using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Warband.Sim;

internal enum RevisionCombatMode
{
    Hidden,
    Ready,
    Opening,
    Selecting,
    Rewinding,
    Landing,
    Receipt,
    Revised,
}

internal enum RevisionDockSide
{
    Top,
    Bottom,
}

public enum RevisionPresentationPhase
{
    None,
    Opening,
    Held,
    Tear,
    Rewind,
    Vacuum,
    RunUp,
    Landing,
    Receipt,
}

/// <summary>
/// One return anchor, drawn as a notch on the Hourstone. <see cref="Payoff"/> (0..1, normalised
/// across the current anchor set) drives the notch's weight — the carry is NOT monotonic in the
/// reach (it peaks just after the champion spent its own Mana), so the stone has to show where the
/// good seconds are without printing a table.
/// </summary>
internal sealed class RevisionAnchorModel
{
    public int Seconds;
    public string Label = "";
    public float Payoff = 1f;
}

/// <summary>
/// The ability, drawn on the unit it happens to. Panel-space because it rides a body around the
/// board: scrubbing moves the target, so this is refreshed every frame while Selecting.
/// </summary>
internal sealed class RevisionClusterModel
{
    public bool Visible;
    public Vector2 Panel;
    public RevisionEffectKind Kind;

    // Borrowed Future
    public int Carry;
    public int ManaAfter;
    public int ManaMax;
    public int Shield;

    // Recall to Formation
    public bool HasHome;
    public Vector2 HomePanel;
    public float DisarmSeconds;
}

internal sealed class RevisionCombatModel
{
    public RevisionCombatMode Mode;
    public string Name = "";
    public string Prompt = "";
    public string Status = "";
    public bool FinalChance;
    public bool CanOpen;
    public bool CanConfirm;
    public int MaxSeconds = 4;
    public int SelectedSeconds = 1;
    public RevisionDockSide DockSide = RevisionDockSide.Bottom;
    public RevisionEffectKind Lineage = RevisionEffectKind.BorrowedFuture;
    public string LineageName = "";
    public List<string> Targets = new List<string>();
    public List<RevisionAnchorModel> Anchors = new List<RevisionAnchorModel>();
    public RevisionClusterModel Cluster = new RevisionClusterModel();
    /// <summary>Where the board actually is, in seconds back. The knob rides this, not the
    /// selection, so the stone stays physically joined to the walk.</summary>
    public float Sweep;
    /// <summary>Hold-to-commit fill, 0..1.</summary>
    public float Hold;
    public RevisionPresentationPhase Presentation;
    public float PresentationProgress;
    public string CinematicTitle = "";
    public string CinematicSubtitle = "";
    public RevisionReceipt Receipt;
}

/// <summary>
/// The fight-only instrument panel for splitting a watched timeline. It owns input and rendering,
/// while RunShell owns every legality decision and all run mutation.
/// </summary>
internal sealed class RevisionCombatOverlay : IDisposable
{
    private readonly Action _open;
    private readonly Action<int> _selectSeconds;
    private readonly Action<int> _shiftSeconds;
    private readonly Action _confirm;
    private readonly Action _cancel;
    private readonly VisualElement _root;
    private readonly VisualElement _backdrop;
    private readonly VisualElement _clock;
    private readonly VisualElement _clockPulse;
    private readonly VisualElement _cinematic;
    private readonly Label _cinematicTitle;
    private readonly Label _cinematicSubtitle;
    private readonly VisualElement _receipt;
    private readonly Label _receiptTitle;
    private readonly Label _receiptSubtitle;
    private readonly VisualElement _receiptLines;
    private readonly VisualElement _instrument;
    private readonly VisualElement _ready;
    private readonly Label _name;
    private readonly Button _revise;

    // The Hourstone
    private readonly VisualElement _dial;
    private readonly VisualElement _dialFill;
    private readonly VisualElement _notches;
    private readonly VisualElement _knob;
    private readonly Label _lineage;
    private readonly Label _dialNumber;
    private readonly Label _dialSub;
    private readonly Label _hint;

    // The ability, on the unit
    private readonly VisualElement _cluster;
    private readonly Label _carry;
    private readonly VisualElement _orb;
    private readonly Label _orbValue;
    private readonly Label _orbLabel;
    private readonly VisualElement _spill;
    private readonly Label _spillValue;
    private readonly VisualElement _home;
    private readonly VisualElement _homeGhost;
    private readonly VisualElement _tether;
    private readonly Label _disarm;
    private readonly Label _disarmLabel;
    private readonly InputAction _openAction;
    private readonly InputAction _previousAction;
    private readonly InputAction _nextAction;
    private readonly InputAction _confirmAction;
    private readonly InputAction _cancelAction;
    private RevisionCombatModel _model = new RevisionCombatModel();

    public VisualElement Root => _root;
    public bool ConsumesEscape =>
        _model.Mode == RevisionCombatMode.Opening ||
        _model.Mode == RevisionCombatMode.Selecting ||
        _model.Mode == RevisionCombatMode.Rewinding ||
        _model.Mode == RevisionCombatMode.Landing ||
        _model.Mode == RevisionCombatMode.Receipt;

    public RevisionCombatOverlay(
        Action open,
        Action<int> selectSeconds,
        Action<int> shiftSeconds,
        Action confirm,
        Action cancel)
    {
        _open = open;
        _selectSeconds = selectSeconds;
        _shiftSeconds = shiftSeconds;
        _confirm = confirm;
        _cancel = cancel;

        _root = new VisualElement { name = "revision-combat" };
        _root.AddToClassList("revision-combat");
        _root.pickingMode = PickingMode.Ignore;

        _backdrop = new VisualElement();
        _backdrop.AddToClassList("revision-ceremony__backdrop");
        _backdrop.pickingMode = PickingMode.Ignore;
        _root.Add(_backdrop);

        _clock = new VisualElement();
        _clock.AddToClassList("revision-clock");
        _clock.pickingMode = PickingMode.Ignore;
        var outer = new VisualElement();
        outer.AddToClassList("revision-clock__outer");
        var inner = new VisualElement();
        inner.AddToClassList("revision-clock__inner");
        var handLong = new VisualElement();
        handLong.AddToClassList("revision-clock__hand");
        handLong.AddToClassList("revision-clock__hand--long");
        var handShort = new VisualElement();
        handShort.AddToClassList("revision-clock__hand");
        handShort.AddToClassList("revision-clock__hand--short");
        _clockPulse = new VisualElement();
        _clockPulse.AddToClassList("revision-clock__pulse");
        _clock.Add(outer);
        _clock.Add(inner);
        _clock.Add(handLong);
        _clock.Add(handShort);
        _clock.Add(_clockPulse);
        _root.Add(_clock);

        _cinematic = new VisualElement();
        _cinematic.AddToClassList("revision-ceremony__copy");
        _cinematic.pickingMode = PickingMode.Ignore;
        _cinematicTitle = new Label();
        _cinematicTitle.AddToClassList("revision-ceremony__title");
        _cinematicSubtitle = new Label();
        _cinematicSubtitle.AddToClassList("revision-ceremony__subtitle");
        _cinematic.Add(_cinematicTitle);
        _cinematic.Add(_cinematicSubtitle);
        _root.Add(_cinematic);

        _receipt = new VisualElement();
        _receipt.AddToClassList("revision-receipt");
        _receipt.pickingMode = PickingMode.Ignore;
        var receiptEyebrow = new Label("THE NEW REALITY");
        receiptEyebrow.AddToClassList("revision-receipt__eyebrow");
        _receiptTitle = new Label();
        _receiptTitle.AddToClassList("revision-receipt__title");
        _receiptSubtitle = new Label();
        _receiptSubtitle.AddToClassList("revision-receipt__subtitle");
        _receiptLines = new VisualElement();
        _receiptLines.AddToClassList("revision-receipt__lines");
        _receipt.Add(receiptEyebrow);
        _receipt.Add(_receiptTitle);
        _receipt.Add(_receiptSubtitle);
        _receipt.Add(_receiptLines);
        _root.Add(_receipt);
        DisablePicking(_backdrop);
        DisablePicking(_clock);
        DisablePicking(_cinematic);
        DisablePicking(_receipt);

        _instrument = new VisualElement();
        _instrument.AddToClassList("revision-combat__instrument");
        _instrument.pickingMode = PickingMode.Ignore;

        _ready = new VisualElement();
        _ready.AddToClassList("revision-combat__ready");
        _ready.pickingMode = PickingMode.Position;
        var readyCopy = new VisualElement();
        readyCopy.AddToClassList("revision-combat__ready-copy");
        _name = new Label();
        _name.AddToClassList("revision-combat__name");
        var charge = new Label("ONE SPLIT REMAINS");
        charge.AddToClassList("revision-combat__charge");
        readyCopy.Add(_name);
        readyCopy.Add(charge);
        _revise = new Button(() => _open?.Invoke()) { text = "REVISE  [R]" };
        _revise.AddToClassList("revision-combat__revise");
        _ready.Add(readyCopy);
        _ready.Add(_revise);
        _instrument.Add(_ready);

        // ---- the ability, drawn on the unit it happens to (behind the stone in z-order) ----
        _cluster = new VisualElement { name = "revision-cluster" };
        _cluster.AddToClassList("revision-cluster");
        _cluster.pickingMode = PickingMode.Ignore;

        _tether = new VisualElement();
        _tether.AddToClassList("revision-cluster__tether");
        _tether.pickingMode = PickingMode.Ignore;
        _cluster.Add(_tether);
        _home = new VisualElement();
        _home.AddToClassList("revision-cluster__home");
        _home.pickingMode = PickingMode.Ignore;
        _homeGhost = new VisualElement();
        _homeGhost.AddToClassList("revision-cluster__home-ghost");
        _homeGhost.pickingMode = PickingMode.Ignore;
        _home.Add(_homeGhost);
        _cluster.Add(_home);

        _carry = new Label();
        _carry.AddToClassList("revision-cluster__carry");
        _carry.pickingMode = PickingMode.Ignore;
        _cluster.Add(_carry);

        _spill = new VisualElement();
        _spill.AddToClassList("revision-cluster__spill");
        _spill.pickingMode = PickingMode.Ignore;
        _spillValue = new Label();
        _spillValue.AddToClassList("revision-cluster__spill-value");
        _spillValue.pickingMode = PickingMode.Ignore;
        _cluster.Add(_spill);
        _cluster.Add(_spillValue);

        _orb = new VisualElement();
        _orb.AddToClassList("revision-cluster__orb");
        _orb.pickingMode = PickingMode.Ignore;
        _orbValue = new Label();
        _orbValue.AddToClassList("revision-cluster__orb-value");
        _orbValue.pickingMode = PickingMode.Ignore;
        _orb.Add(_orbValue);
        _orbLabel = new Label();
        _orbLabel.AddToClassList("revision-cluster__orb-label");
        _orbLabel.pickingMode = PickingMode.Ignore;
        _cluster.Add(_orb);
        _cluster.Add(_orbLabel);

        _disarm = new Label();
        _disarm.AddToClassList("revision-cluster__disarm");
        _disarm.pickingMode = PickingMode.Ignore;
        _disarmLabel = new Label("CANNOT SWING");
        _disarmLabel.AddToClassList("revision-cluster__disarm-label");
        _disarmLabel.pickingMode = PickingMode.Ignore;
        _cluster.Add(_disarm);
        _cluster.Add(_disarmLabel);
        _root.Add(_cluster);

        // ---- the Hourstone ----
        _dial = new VisualElement { name = "revision-dial" };
        _dial.AddToClassList("revision-dial");
        _dial.pickingMode = PickingMode.Position;
        _dialFill = new VisualElement();
        _dialFill.AddToClassList("revision-dial__fill");
        _dialFill.pickingMode = PickingMode.Ignore;
        _dial.Add(_dialFill);
        _notches = new VisualElement();
        _notches.AddToClassList("revision-dial__notches");
        _notches.pickingMode = PickingMode.Ignore;
        _dial.Add(_notches);
        _knob = new VisualElement();
        _knob.AddToClassList("revision-dial__knob");
        _knob.pickingMode = PickingMode.Ignore;
        _dial.Add(_knob);
        _lineage = new Label();
        _lineage.AddToClassList("revision-dial__lineage");
        _lineage.pickingMode = PickingMode.Ignore;
        _dial.Add(_lineage);
        var eye = new VisualElement();
        eye.AddToClassList("revision-dial__eye");
        eye.pickingMode = PickingMode.Position;
        eye.RegisterCallback<PointerDownEvent>(e => { _pointerHold = true; e.StopPropagation(); });
        eye.RegisterCallback<PointerUpEvent>(e => { _pointerHold = false; e.StopPropagation(); });
        eye.RegisterCallback<PointerLeaveEvent>(_ => _pointerHold = false);
        _dialNumber = new Label();
        _dialNumber.AddToClassList("revision-dial__number");
        _dialSub = new Label("ROLLED BACK");
        _dialSub.AddToClassList("revision-dial__sub");
        eye.Add(_dialNumber);
        eye.Add(_dialSub);
        _dial.Add(eye);
        _root.Add(_dial);

        _hint = new Label();
        _hint.AddToClassList("revision-dial__hint");
        _hint.pickingMode = PickingMode.Ignore;
        _root.Add(_hint);

        _dial.RegisterCallback<PointerDownEvent>(OnDialDown);
        _dial.RegisterCallback<PointerMoveEvent>(OnDialMove);
        _dial.RegisterCallback<PointerUpEvent>(OnDialUp);
        _dial.RegisterCallback<PointerLeaveEvent>(OnDialUp);

        _root.Add(_instrument);

        _openAction = new InputAction("Open Revision", InputActionType.Button, "<Keyboard>/r");
        _openAction.AddBinding("<Gamepad>/buttonNorth");
        _previousAction = new InputAction("Earlier Anchor", InputActionType.Button, "<Keyboard>/leftArrow");
        _previousAction.AddBinding("<Gamepad>/dpad/left");
        _nextAction = new InputAction("Later Anchor", InputActionType.Button, "<Keyboard>/rightArrow");
        _nextAction.AddBinding("<Gamepad>/dpad/right");
        _confirmAction = new InputAction("Confirm Revision", InputActionType.Button, "<Keyboard>/enter");
        _confirmAction.AddBinding("<Gamepad>/buttonSouth");
        _cancelAction = new InputAction("Cancel Revision", InputActionType.Button, "<Keyboard>/escape");
        _cancelAction.AddBinding("<Gamepad>/buttonEast");
        _openAction.performed += _ =>
        {
            if (_model.Mode == RevisionCombatMode.Ready && _model.CanOpen) _open?.Invoke();
        };
        _previousAction.performed += _ =>
        {
            if (_model.Mode == RevisionCombatMode.Selecting) _shiftSeconds?.Invoke(1);
        };
        _nextAction.performed += _ =>
        {
            if (_model.Mode == RevisionCombatMode.Selecting) _shiftSeconds?.Invoke(-1);
        };
        _cancelAction.performed += _ =>
        {
            if (_model.Mode == RevisionCombatMode.Selecting) _cancel?.Invoke();
        };
        _openAction.Enable();
        _previousAction.Enable();
        _nextAction.Enable();
        _confirmAction.Enable();
        _cancelAction.Enable();
        Bind(_model);
    }

    private static void DisablePicking(VisualElement element)
    {
        element.pickingMode = PickingMode.Ignore;
        foreach (VisualElement child in element.Children())
            DisablePicking(child);
    }

    public void Bind(RevisionCombatModel model)
    {
        _model = model ?? new RevisionCombatModel();
        bool visible = _model.Mode != RevisionCombatMode.Hidden;
        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _root.EnableInClassList("revision-combat--final", _model.FinalChance);
        _root.EnableInClassList("revision-combat--selecting",
            _model.Mode == RevisionCombatMode.Selecting);
        _root.EnableInClassList("revision-combat--rewinding",
            _model.Mode == RevisionCombatMode.Rewinding);
        _root.EnableInClassList("revision-combat--landing",
            _model.Mode == RevisionCombatMode.Landing);
        _root.EnableInClassList("revision-combat--receipt",
            _model.Mode == RevisionCombatMode.Receipt);
        _root.EnableInClassList("revision-combat--dock-top",
            _model.DockSide == RevisionDockSide.Top);
        _root.EnableInClassList("revision-combat--dock-bottom",
            _model.DockSide == RevisionDockSide.Bottom);

        bool ready = _model.Mode == RevisionCombatMode.Ready ||
                     _model.Mode == RevisionCombatMode.Revised;
        _ready.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
        bool selecting = _model.Mode == RevisionCombatMode.Selecting;
        _instrument.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
        _dial.style.display = selecting ? DisplayStyle.Flex : DisplayStyle.None;
        _hint.style.display = selecting ? DisplayStyle.Flex : DisplayStyle.None;
        _name.text = _model.Mode == RevisionCombatMode.Revised
            ? _model.Name + " \u00b7 SPENT"
            : _model.Name;
        _revise.style.display = _model.Mode == RevisionCombatMode.Ready
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        _revise.SetEnabled(_model.CanOpen);

        _lineage.text = _model.LineageName;
        bool atPresent = _model.SelectedSeconds <= 0;
        _dialNumber.text = atPresent ? "NOW" : _model.SelectedSeconds + "s";
        _dialSub.text = atPresent ? "THE MOMENT YOU STOPPED" : "ROLLED BACK";
        _dial.EnableInClassList("revision-dial--present", atPresent);
        BuildNotches();
        SetSweep(_model.Sweep);
        SetHold(_model.Hold);
        SetCluster(_model.Cluster);
        _hint.text = HintText();

        ApplyPresentation(
            _model.Presentation,
            _model.PresentationProgress,
            _model.CinematicTitle,
            _model.CinematicSubtitle,
            _model.Receipt);
    }

    /// <summary>
    /// Cheap per-frame ceremony update. The timeline buttons are deliberately not rebuilt while
    /// the unscaled cinematic clock runs.
    /// </summary>
    public void SetPresentation(
        RevisionPresentationPhase phase,
        float progress,
        string title = "",
        string subtitle = "",
        RevisionReceipt receipt = null)
    {
        _model.Presentation = phase;
        _model.PresentationProgress = Mathf.Clamp01(progress);
        _model.CinematicTitle = title ?? "";
        _model.CinematicSubtitle = subtitle ?? "";
        if (receipt != null) _model.Receipt = receipt;
        ApplyPresentation(phase, progress, title, subtitle, receipt ?? _model.Receipt);
    }

    private void ApplyPresentation(
        RevisionPresentationPhase phase,
        float progress,
        string title,
        string subtitle,
        RevisionReceipt receipt)
    {
        progress = Mathf.Clamp01(progress);
        bool ceremony = phase != RevisionPresentationPhase.None &&
                        phase != RevisionPresentationPhase.Held;
        bool held = phase == RevisionPresentationPhase.Held;
        bool receiptVisible = phase == RevisionPresentationPhase.Receipt;
        // The run-up is live combat, not a held frame: the instrument announces itself and then
        // gets out of the way, and the dial never covers the board the player came back to read.
        bool runUp = phase == RevisionPresentationPhase.RunUp;
        float runUpCopy = runUp
            ? Mathf.Clamp01(1f - (progress - 0.25f) / 0.25f)
            : 1f;
        _backdrop.style.display = ceremony || held ? DisplayStyle.Flex : DisplayStyle.None;
        _clock.style.display = ceremony && !receiptVisible && !runUp
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        _cinematic.style.display = ceremony && !receiptVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        _receipt.style.display = receiptVisible ? DisplayStyle.Flex : DisplayStyle.None;

        float opening = phase == RevisionPresentationPhase.Opening ? progress : 1f;
        float retreat = phase == RevisionPresentationPhase.Receipt ? 1f - progress : 1f;
        float backdrop = held ? 0.16f :
            phase == RevisionPresentationPhase.Vacuum ? 0.28f :
            phase == RevisionPresentationPhase.Receipt ? 0.22f :
            phase == RevisionPresentationPhase.Landing ? 0.06f :
            runUp ? 0.05f * runUpCopy : 0.10f;
        _backdrop.style.opacity = Mathf.Clamp01(backdrop * opening * retreat);
        float pulse = 1f + Mathf.Sin(progress * Mathf.PI) *
            (phase == RevisionPresentationPhase.Landing ? 0.28f : 0.08f);
        _clock.style.scale = new Scale(new Vector2(pulse, pulse));
        _clock.style.opacity = phase == RevisionPresentationPhase.Vacuum
            ? 0.08f
            : Mathf.Clamp01(opening * retreat);
        _clockPulse.style.opacity = Mathf.Sin(progress * Mathf.PI);

        _cinematicTitle.text = title ?? "";
        _cinematicSubtitle.text = subtitle ?? "";
        _cinematic.style.opacity = phase == RevisionPresentationPhase.Vacuum
            ? 0f
            : Mathf.Clamp01(opening * retreat * runUpCopy);

        if (receipt != null)
        {
            _receiptTitle.text = receipt.Title;
            _receiptSubtitle.text = receipt.Subtitle;
            _receiptLines.Clear();
            foreach (string line in receipt.Lines)
            {
                var label = new Label(line);
                label.AddToClassList("revision-receipt__line");
                label.pickingMode = PickingMode.Ignore;
                _receiptLines.Add(label);
            }
        }
        _receipt.style.opacity = receiptVisible
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.4f))
            : 0f;
        if (receiptVisible)
        {
            float scale = Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0f, 1f, progress));
            _receipt.style.scale = new Scale(new Vector2(scale, scale));
        }
    }

    // Dial geometry. The stone is a fixed 620px circle in USS; everything on it is placed in its
    // own pixel space so the notches, the knob and the sand all agree.
    private const float DialSize = 388f;
    private const float DialCentre = DialSize * 0.5f;
    private const float NotchRadius = 156f;
    private const float ArcDegrees = 58f;
    private const int SandSegments = 40;

    /// <summary>Angle of the k-th anchor (1 = shallowest). Deepest sits left, as the board reads.</summary>
    private float AnchorAngle(float seconds, int max) =>
        max <= 0 ? 0f : Mathf.Lerp(ArcDegrees, -ArcDegrees, Mathf.Clamp01(seconds / max));

    private static Vector2 OnRing(float degrees, float radius)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(DialCentre + radius * Mathf.Sin(r), DialCentre - radius * Mathf.Cos(r));
    }

    private void BuildNotches()
    {
        _notches.Clear();
        foreach (RevisionAnchorModel anchor in _model.Anchors)
        {
            Vector2 at = OnRing(AnchorAngle(anchor.Seconds, _model.MaxSeconds), NotchRadius);
            bool selected = anchor.Seconds == _model.SelectedSeconds;
            int seconds = anchor.Seconds;
            var notch = new Button(() => _selectSeconds?.Invoke(seconds));
            notch.AddToClassList("revision-dial__notch");
            notch.EnableInClassList("revision-dial__notch--selected", selected);
            notch.style.left = at.x;
            notch.style.top = at.y;
            // Weight carries payoff: the carry peaks just after the champion spent its own Mana, so
            // the good seconds have to be visible on the stone rather than printed in a table.
            float weight = Mathf.Clamp01(anchor.Payoff);
            float size = Mathf.Lerp(7f, 17f, weight);
            var bead = new VisualElement();
            bead.AddToClassList("revision-dial__bead");
            bead.pickingMode = PickingMode.Ignore;
            bead.style.width = size;
            bead.style.height = size;
            bead.style.marginLeft = -size * 0.5f;
            bead.style.marginTop = -size * 0.5f;
            bead.style.opacity = Mathf.Lerp(0.42f, 1f, weight);
            notch.Add(bead);
            var label = new Label(anchor.Label);
            label.AddToClassList("revision-dial__notch-label");
            label.pickingMode = PickingMode.Ignore;
            notch.Add(label);
            notch.tooltip = anchor.Label;
            _notches.Add(notch);
        }

        _dialFill.Clear();
        for (int i = 0; i < SandSegments; i++)
        {
            // UI Toolkit has no conic gradient, so the filling ring is a run of beads that light in
            // order. It reads as sand closing the circle and costs one class toggle per frame.
            Vector2 at = OnRing(Mathf.Lerp(-176f, 176f, i / (SandSegments - 1f)), NotchRadius + 44f);
            var bead = new VisualElement();
            bead.AddToClassList("revision-dial__sand");
            bead.pickingMode = PickingMode.Ignore;
            bead.style.left = at.x;
            bead.style.top = at.y;
            _dialFill.Add(bead);
        }
    }

    /// <summary>Slide the knob to where the board actually is. Cheap: one style per frame.</summary>
    public void SetSweep(float secondsBack)
    {
        _model.Sweep = secondsBack;
        Vector2 at = OnRing(AnchorAngle(secondsBack, _model.MaxSeconds), NotchRadius);
        _knob.style.left = at.x;
        _knob.style.top = at.y;
    }

    /// <summary>Hold-to-commit fill, 0..1. One irreversible action per battle earns a held beat.</summary>
    public void SetHold(float progress)
    {
        _model.Hold = Mathf.Clamp01(progress);
        int lit = Mathf.RoundToInt(_model.Hold * SandSegments);
        for (int i = 0; i < _dialFill.childCount; i++)
            _dialFill[i].EnableInClassList("revision-dial__sand--lit", i < lit);
        _dial.EnableInClassList("revision-dial--holding", _model.Hold > 0.001f);
    }

    /// <summary>True while the player is asking to commit, by key, pad or the stone's eye.</summary>
    public bool ConfirmHeld => _pointerHold || _confirmAction.IsPressed();

    private bool _pointerHold;
    private int _dragPointer = -1;
    private float _dragOriginX;
    private int _dragOriginSeconds;

    private void OnDialDown(PointerDownEvent evt)
    {
        if (_model.Mode != RevisionCombatMode.Selecting) return;
        _dragPointer = evt.pointerId;
        _dragOriginX = evt.position.x;
        _dragOriginSeconds = _model.SelectedSeconds;
        _dial.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnDialMove(PointerMoveEvent evt)
    {
        if (_dragPointer != evt.pointerId || _model.Mode != RevisionCombatMode.Selecting) return;
        // Dragging right walks toward the present, left reaches deeper — the same sense as the board.
        int steps = Mathf.RoundToInt((_dragOriginX - evt.position.x) / 52f);
        int wanted = Mathf.Clamp(_dragOriginSeconds + steps, 1, _model.MaxSeconds);
        if (wanted != _model.SelectedSeconds) _selectSeconds?.Invoke(wanted);
        evt.StopPropagation();
    }

    private void OnDialUp(EventBase evt)
    {
        if (_dragPointer < 0) return;
        _dial.ReleasePointer(_dragPointer);
        _dragPointer = -1;
    }

    private string HintText()
    {
        if (!string.IsNullOrEmpty(_model.Status)) return _model.Status;
        if (_model.SelectedSeconds <= 0) return "TURN THE HOURSTONE TO REACH BACK";
        if (!_model.CanConfirm)
            return _model.Lineage == RevisionEffectKind.BorrowedFuture
                ? "CHOOSE A CHAMPION"
                : "CHOOSE AN ENEMY";
        return _model.FinalChance
            ? "HOLD ⏎ TO SPLIT  ·  ESC TO ACCEPT FATE"
            : "HOLD ⏎ TO SPLIT THE HOUR";
    }

    /// <summary>
    /// Draw the ability on the unit it happens to. Panel-space and refreshed every frame, because
    /// scrubbing walks the target across the board and the numbers have to ride it.
    /// </summary>
    public void SetCluster(RevisionClusterModel cluster)
    {
        _model.Cluster = cluster ?? new RevisionClusterModel();
        cluster = _model.Cluster;
        bool on = cluster.Visible && _model.Mode == RevisionCombatMode.Selecting;
        _cluster.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (!on) return;

        bool borrowed = cluster.Kind == RevisionEffectKind.BorrowedFuture;
        _carry.style.display = borrowed ? DisplayStyle.Flex : DisplayStyle.None;
        _orb.style.display = borrowed ? DisplayStyle.Flex : DisplayStyle.None;
        _orbLabel.style.display = borrowed ? DisplayStyle.Flex : DisplayStyle.None;
        bool spilling = borrowed && cluster.Shield > 0;
        _spill.style.display = spilling ? DisplayStyle.Flex : DisplayStyle.None;
        _spillValue.style.display = spilling ? DisplayStyle.Flex : DisplayStyle.None;
        _home.style.display = !borrowed && cluster.HasHome ? DisplayStyle.Flex : DisplayStyle.None;
        _tether.style.display = !borrowed && cluster.HasHome ? DisplayStyle.Flex : DisplayStyle.None;
        _disarm.style.display = borrowed ? DisplayStyle.None : DisplayStyle.Flex;
        _disarmLabel.style.display = borrowed ? DisplayStyle.None : DisplayStyle.Flex;

        Vector2 p = cluster.Panel;
        if (borrowed)
        {
            Place(_carry, p.x, p.y - 134f);
            Place(_orb, p.x, p.y - 60f);
            Place(_orbLabel, p.x, p.y - 16f);
            Place(_spill, p.x, p.y - 60f);
            Place(_spillValue, p.x + 52f, p.y - 96f);
            _carry.text = "+" + cluster.Carry;
            _orbValue.text = cluster.ManaAfter.ToString();
            _orbLabel.text = cluster.ManaAfter >= cluster.ManaMax && cluster.ManaMax > 0
                ? "MANA · FULL"
                : "MANA";
            _spillValue.text = cluster.Shield + " SHIELD";
            return;
        }

        Place(_disarm, p.x, p.y - 134f);
        Place(_disarmLabel, p.x, p.y - 94f);
        _disarm.text = cluster.DisarmSeconds.ToString("0.0") + "s";
        if (!cluster.HasHome) return;
        Place(_home, cluster.HomePanel.x, cluster.HomePanel.y);
        Vector2 delta = cluster.HomePanel - p;
        _tether.style.left = p.x;
        _tether.style.top = p.y - 25f;
        _tether.style.width = delta.magnitude;
        _tether.style.transformOrigin =
            new TransformOrigin(Length.Percent(0f), Length.Percent(50f));
        _tether.style.rotate =
            new Rotate(Angle.Degrees(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
    }

    private static void Place(VisualElement element, float x, float y)
    {
        element.style.left = x;
        element.style.top = y;
    }

    public void Dispose()
    {
        RevisionScreenEffect.Clear();
        _openAction.Dispose();
        _previousAction.Dispose();
        _nextAction.Dispose();
        _confirmAction.Dispose();
        _cancelAction.Dispose();
    }
}
