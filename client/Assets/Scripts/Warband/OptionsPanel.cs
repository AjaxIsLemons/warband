using System;
using UnityEngine.UIElements;

/// <summary>
/// The player options modal (roadmap item 9): a screen over existing seams, owned by the shell's
/// persistent layer so it opens the same way over the Menu, the Hall and a running fight. Every
/// control writes <see cref="PlayerOptions"/> and applies instantly — there is no OK/Cancel,
/// because there is nothing to stage: the seams are all live.
/// </summary>
internal sealed class OptionsPanel
{
    private readonly VisualElement _modal;
    private readonly Toggle _sound;
    private readonly Slider _master;
    private readonly Slider _ui;
    private readonly Slider _board;
    private readonly Label _masterValue;
    private readonly Label _uiValue;
    private readonly Label _boardValue;
    private readonly Toggle _reducedMotion;
    private readonly Slider _battleSpeed;
    private readonly Label _battleSpeedValue;
    private readonly Button _close;
    private readonly Action<bool> _onReducedMotion;
    private readonly Action _onBattleSpeed;

    public VisualElement Root { get; }

    public bool IsOpen => Root.style.display == DisplayStyle.Flex;

    public OptionsPanel(Action<bool> onReducedMotion, Action onBattleSpeed)
    {
        _onReducedMotion = onReducedMotion;
        _onBattleSpeed = onBattleSpeed;

        Root = new VisualElement { name = "options-gate" };
        Root.AddToClassList("modal-scrim");
        Root.AddToClassList("options-scrim");
        Root.pickingMode = PickingMode.Position;
        Root.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == Root) Close();
        });

        _modal = new VisualElement { name = "options-modal" };
        _modal.AddToClassList("modal");
        _modal.AddToClassList("options-modal");
        Root.Add(_modal);

        var heading = new Label("OPTIONS");
        heading.AddToClassList("options-modal__heading");
        _modal.Add(heading);

        _modal.Add(Section("AUDIO"));
        _sound = new Toggle("SOUND");
        _sound.AddToClassList("options-modal__toggle");
        _sound.RegisterValueChangedCallback(evt =>
        {
            PlayerOptions.AudioEnabled = evt.newValue;
            SyncEnabledStates();
        });
        _modal.Add(_sound);
        (_master, _masterValue) = SliderRow("MASTER", v => PlayerOptions.MasterVolume = v);
        (_ui, _uiValue) = SliderRow("INTERFACE", v => PlayerOptions.UiVolume = v);
        (_board, _boardValue) = SliderRow("BATTLE", v => PlayerOptions.BoardVolume = v);

        _modal.Add(Section("MOTION"));
        _reducedMotion = new Toggle("REDUCED MOTION");
        _reducedMotion.AddToClassList("options-modal__toggle");
        _reducedMotion.RegisterValueChangedCallback(evt =>
        {
            PlayerOptions.ReducedMotion = evt.newValue;
            _onReducedMotion?.Invoke(evt.newValue);
        });
        _modal.Add(_reducedMotion);
        _modal.Add(Hint("Cuts interface animation. Applies immediately."));

        _modal.Add(Section("BATTLE"));
        (_battleSpeed, _battleSpeedValue) = SliderRow(
            "BATTLE SPEED",
            v =>
            {
                PlayerOptions.BattleSpeed = v;
                _onBattleSpeed?.Invoke();
            },
            PlayerOptions.MinBattleSpeed, PlayerOptions.MaxBattleSpeed);
        _modal.Add(Hint("Playback pace only — the outcome never changes."));

        var actionsRow = new VisualElement();
        actionsRow.AddToClassList("options-modal__actions");
        _close = new Button(Close) { text = "CLOSE" };
        _close.AddToClassList("btn");
        _close.AddToClassList("btn--primary");
        actionsRow.Add(_close);
        _modal.Add(actionsRow);

        Root.style.display = DisplayStyle.None;
    }

    public void Open()
    {
        // Read the store fresh on every open — the F1 cockpit or a dev toggle may have moved
        // values while the panel was closed. SetValueWithoutNotify: rendering is not deciding.
        _sound.SetValueWithoutNotify(PlayerOptions.AudioEnabled);
        _master.SetValueWithoutNotify(PlayerOptions.MasterVolume);
        _ui.SetValueWithoutNotify(PlayerOptions.UiVolume);
        _board.SetValueWithoutNotify(PlayerOptions.BoardVolume);
        _reducedMotion.SetValueWithoutNotify(PlayerOptions.ReducedMotion);
        _battleSpeed.SetValueWithoutNotify(PlayerOptions.BattleSpeed);
        SyncValueLabels();
        SyncEnabledStates();
        Root.style.display = DisplayStyle.Flex;
        Root.BringToFront();
    }

    public void Close()
    {
        Root.style.display = DisplayStyle.None;
        UnityEngine.PlayerPrefs.Save();   // sliders write per-tick; flush once, on the way out
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private (Slider, Label) SliderRow(
        string label, Action<float> apply, float min = 0f, float max = 1f)
    {
        var row = new VisualElement();
        row.AddToClassList("options-modal__row");
        var name = new Label(label);
        name.AddToClassList("options-modal__label");
        row.Add(name);
        var slider = new Slider(min, max);
        slider.AddToClassList("options-modal__slider");
        var value = new Label();
        value.AddToClassList("options-modal__value");
        slider.RegisterValueChangedCallback(evt =>
        {
            apply(evt.newValue);
            SyncValueLabels();
        });
        row.Add(slider);
        row.Add(value);
        _modal.Add(row);
        return (slider, value);
    }

    private VisualElement Section(string title)
    {
        var label = new Label(title);
        label.AddToClassList("options-modal__section");
        return label;
    }

    private VisualElement Hint(string copy)
    {
        var label = new Label(copy);
        label.AddToClassList("options-modal__hint");
        return label;
    }

    private void SyncValueLabels()
    {
        _masterValue.text = Percent(PlayerOptions.MasterVolume);
        _uiValue.text = Percent(PlayerOptions.UiVolume);
        _boardValue.text = Percent(PlayerOptions.BoardVolume);
        _battleSpeedValue.text = $"×{PlayerOptions.BattleSpeed:0.0}";
    }

    private void SyncEnabledStates()
    {
        bool on = PlayerOptions.AudioEnabled;
        _master.SetEnabled(on);
        _ui.SetEnabled(on);
        _board.SetEnabled(on);
    }

    private static string Percent(float v) => $"{UnityEngine.Mathf.RoundToInt(v * 100f)}%";

    /// <summary>Structural contracts for the UI QA matrix. The result-gate lesson applies: assert
    /// the CONTROLS against the modal, not just the modal against the screen — buttons overflow
    /// their row before the row overflows anything.</summary>
    public string EditorResolvedLayoutReport(VisualElement host)
    {
        var report = new UiLayoutReport("Options");
        UiLayoutContract.RequireResolved(report, _modal, "modal");
        if (host != null) UiLayoutContract.RequireInside(report, _modal, host, "options modal");
        UiLayoutContract.RequireInside(report, _sound, _modal, "sound toggle");
        UiLayoutContract.RequireInside(report, _master, _modal, "master slider");
        UiLayoutContract.RequireInside(report, _ui, _modal, "interface slider");
        UiLayoutContract.RequireInside(report, _board, _modal, "battle slider");
        UiLayoutContract.RequireInside(report, _reducedMotion, _modal, "reduced motion toggle");
        UiLayoutContract.RequireInside(report, _battleSpeed, _modal, "battle speed slider");
        UiLayoutContract.RequireInside(report, _close, _modal, "close button");
        UiLayoutContract.RequireNoScrollView(report, _modal, "options modal");
        return report.ToString();
    }
}
