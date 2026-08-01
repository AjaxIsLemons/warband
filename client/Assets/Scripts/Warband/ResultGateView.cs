using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Blocking post-fight comprehension layer. It sits over the frozen battlefield, so a player can
/// read what happened, replay the same resolved log, then take one contextual exit.
/// </summary>
internal sealed class ResultGateView
{
    private readonly RunShellActions _actions;
    private readonly VisualElement _safe;
    private readonly VisualElement _panel;
    private readonly Label _eyebrow;
    private readonly Label _heading;
    private readonly Label _summary;
    private readonly VisualElement _stats;
    private readonly VisualElement _actionsRow;
    private readonly CombatRecapPanel _recap;
    private readonly VisualElement _deaths;
    private readonly Label _recommendation;
    private readonly Button _watchAgain;
    private readonly Button _continue;
    private readonly List<VisualElement> _statPool = new List<VisualElement>();
    private readonly List<Label> _deathPool = new List<Label>();
    private readonly List<IVisualElementScheduledItem> _revealItems =
        new List<IVisualElementScheduledItem>();
    private readonly HubPresentationConfig _config;
    private IVisualElementScheduledItem _enterCleanup;
    private int _revealGeneration;
    private bool _wasOpen;

    public VisualElement Root { get; }

    public ResultGateView(RunShellActions actions)
    {
        _actions = actions;
        _config = HubPresentationConfig.Load();

        Root = new VisualElement { name = "result-gate" };
        Root.AddToClassList("result-gate");
        Root.pickingMode = PickingMode.Position;

        _safe = new VisualElement { name = "result-safe" };
        _safe.AddToClassList("result-gate__safe");
        Root.Add(_safe);

        _panel = new VisualElement { name = "result-panel" };
        _panel.AddToClassList("result-gate__panel");
        _safe.Add(_panel);

        _eyebrow = Label("result-gate__eyebrow");
        _heading = Label("result-gate__heading");
        _summary = Label("result-gate__summary");
        _panel.Add(_eyebrow);
        _panel.Add(_heading);
        _panel.Add(_summary);

        _stats = new VisualElement();
        _stats.AddToClassList("result-gate__stats");
        _panel.Add(_stats);

        _recap = new CombatRecapPanel();
        _panel.Add(_recap.Root);

        _deaths = new VisualElement();
        _deaths.AddToClassList("result-gate__deaths");
        _panel.Add(_deaths);

        _recommendation = Label("result-gate__recommendation");
        _panel.Add(_recommendation);

        var actionsRow = new VisualElement();
        _actionsRow = actionsRow;
        actionsRow.AddToClassList("result-gate__actions");
        _watchAgain = new Button(() => _actions.WatchFightAgain?.Invoke())
        {
            text = "WATCH AGAIN",
        };
        _watchAgain.AddToClassList("btn");
        _watchAgain.AddToClassList("btn--ghost");
        _continue = new Button(() => _actions.ContinueFightResult?.Invoke());
        _continue.AddToClassList("btn");
        _continue.AddToClassList("btn--primary");
        actionsRow.Add(_watchAgain);
        actionsRow.Add(_continue);
        _panel.Add(actionsRow);

        Root.style.display = DisplayStyle.None;
    }

    public void Bind(ResultGateModel model, bool reducedMotion)
    {
        Root.EnableInClassList("result-gate--defeat", !model.Victory);
        Root.EnableInClassList("motion--reduced", reducedMotion);
        Root.style.display = model.Open ? DisplayStyle.Flex : DisplayStyle.None;
        if (!model.Open)
        {
            CancelReveals();
            _wasOpen = false;
            return;
        }

        bool entering = !_wasOpen;
        if (entering)
        {
            CancelReveals();
            _revealGeneration++;
        }
        _eyebrow.text = model.Eyebrow;
        _heading.text = model.Heading;
        _summary.text = model.Summary;
        _recommendation.text = model.Recommendation;
        _continue.text = model.ContinueLabel;
        _watchAgain.SetEnabled(model.CanWatchAgain);
        _watchAgain.style.display = model.CanWatchAgain ? DisplayStyle.Flex : DisplayStyle.None;
        SyncStats(model.Stats, entering && !reducedMotion);
        _recap.Bind(model.Recap);
        SyncDeaths(model.Deaths, entering && !reducedMotion);
        if (!entering) return;
        _wasOpen = true;
        Root.AddToClassList("result-gate--enter");
        _enterCleanup?.Pause();
        int delay = reducedMotion ? _config.reducedFadeMs : _config.result.durationMs;
        _enterCleanup = Root.schedule.Execute(() =>
            Root.RemoveFromClassList("result-gate--enter"));
        _enterCleanup.ExecuteLater(delay);
        _continue.Focus();
        UiPolishSignals.Emit(UiPolishSignals.Cue.Result, targetId: "result-panel",
            tone: model.Victory ? UiFeedbackTone.Major : UiFeedbackTone.Negative);
    }

    public void Hide()
    {
        CancelReveals();
        _enterCleanup?.Pause();
        _enterCleanup = null;
        _wasOpen = false;
        Root.style.display = DisplayStyle.None;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string EditorResolvedLayoutReport(VisualElement permanentRail = null)
    {
        var report = new UiLayoutReport("Result gate");
        UiLayoutContract.RequireResolved(report, Root, "root");
        UiLayoutContract.RequireInside(report, _panel, _safe, "result panel");
        if (permanentRail != null)
            UiLayoutContract.RequireNoOverlap(
                report, _panel, permanentRail, "result panel / permanent rail");
        UiLayoutContract.RequireNoScrollView(report, Root, "Result gate");
        UiLayoutContract.RequireMinimumHeight(report, Root, "btn", 44f);
        UiLayoutContract.RequireMinimumFont(report, Root, "result-gate__summary", 16f);
        UiLayoutContract.RequireMinimumFont(report, Root, "result-stat__label", 13f);
        UiLayoutContract.RequireMinimumFont(report, Root, "result-gate__death", 16f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, Root, "result-gate__summary", 12.5f);
        UiLayoutContract.RequireWrappedTextFits(report, Root, "result-gate__summary");
        UiLayoutContract.RequireWrappedTextFits(report, Root, "result-gate__death");

        // ⚠ HEIGHT FIRST. The 2026-07-27 break was a VERTICAL collapse: the panel ran past its
        // max-height and Yoga's default flex-shrink squashed every child, so 22px rows resolved
        // to ~11px and their text spilled out of the box. The min-font and text-fits checks below
        // were blind to it — the font never changed and nothing clipped horizontally. These
        // height assertions are the ones that fail when it happens again.
        // Each minimum is the USS design height MINUS 1px. PanelSettings scales this document
        // (1600x900 reference, match=1), and layout rounds to device pixels, so a 24px row
        // measures 23.9px at 2556x1317 and 23.1px at 1024x768 while resolving exactly at
        // scale 1.0. That 1px of slack is rounding; it is not collapse — the real bug resolved
        // 22px rows at 11px, a 50% shortfall this still fails on loudly.
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-row", 21f);
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-row__track", 11f);
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-comp", 15f);
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-track", 21f);
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-key", 17f);
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap-losses", 17f);
        // The columns must be as tall as what they hold. This is the check that catches a
        // zero-height flex column — the phone break where composition, timeline and the
        // recommendation all drew on top of each other while every other assertion passed,
        // because overlapping siblings are still the right height and still inside the panel.
        UiLayoutContract.RequireMinimumHeight(report, Root, "recap__col", 55f);
        // The gate's own boxes squashed too — the stat card spilled its value outside its
        // background — so the pre-existing elements get the same guard.
        UiLayoutContract.RequireMinimumHeight(report, Root, "result-stat", 59f);

        UiLayoutContract.RequireInside(report, _recap.Root, _panel, "combat recap");
        // The exit buttons are the LAST thing in the panel, so they are where a height overrun
        // now surfaces: with flex-shrink pinned off, overflow pushes them through the bottom
        // border instead of quietly squashing the charts above them.
        // Assert the BUTTONS, not their row — the buttons overflow the row, so checking the row
        // reported PASS while they were visibly hanging through the panel's bottom border.
        UiLayoutContract.RequireInside(report, _actionsRow, _panel, "result actions");
        UiLayoutContract.RequireClassInside(report, Root, "btn", _panel);
        UiLayoutContract.RequireClassInside(report, Root, "recap-row", _panel);
        UiLayoutContract.RequireMinimumFont(report, Root, "recap-row__name", 13f);
        UiLayoutContract.RequireMinimumFont(report, Root, "recap-row__value", 13f);
        UiLayoutContract.RequireMinimumFont(report, Root, "recap-key__label", 12f);
        UiLayoutContract.RequireSingleLineTextFits(report, Root, "recap-row__value");
        UiLayoutContract.RequireSingleLineTextFits(report, Root, "recap-key__label");
        return report.ToString();
    }
#endif

    private void SyncStats(IReadOnlyList<ResultStatModel> models, bool animate)
    {
        while (_statPool.Count > models.Count)
        {
            int last = _statPool.Count - 1;
            _statPool[last].RemoveFromHierarchy();
            _statPool.RemoveAt(last);
        }
        while (_statPool.Count < models.Count)
        {
            var stat = new VisualElement();
            stat.AddToClassList("result-stat");
            stat.Add(Label("result-stat__label"));
            stat.Add(Label("result-stat__value"));
            _statPool.Add(stat);
            _stats.Add(stat);
        }

        for (int i = 0; i < models.Count; i++)
        {
            var stat = _statPool[i];
            ((Label)stat.ElementAt(0)).text = models[i].Label;
            var value = (Label)stat.ElementAt(1);
            value.text = models[i].Value;
            if (animate)
                ScheduleCount(value, models[i].Value,
                    Mathf.Min(_config.result.staggerCapMs, i * _config.result.staggerMs));
            stat.EnableInClassList("result-stat--sand", models[i].Tone == "sand");
            stat.EnableInClassList("result-stat--good", models[i].Tone == "good");
        }
    }

    private void SyncDeaths(IReadOnlyList<string> models, bool animate)
    {
        while (_deathPool.Count > models.Count)
        {
            int last = _deathPool.Count - 1;
            _deathPool[last].RemoveFromHierarchy();
            _deathPool.RemoveAt(last);
        }
        while (_deathPool.Count < models.Count)
        {
            var line = Label("result-gate__death");
            _deathPool.Add(line);
            _deaths.Add(line);
        }
        for (int i = 0; i < models.Count; i++)
        {
            _deathPool[i].text = models[i];
            PrepareDeathReveal(_deathPool[i], animate, i);
        }
        _deaths.style.display = models.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ScheduleCount(Label label, string finalText, int delayMs)
    {
        if (!TryFindNumber(finalText, out int first, out int length, out int end)) return;
        string prefix = finalText.Substring(0, first);
        string suffix = finalText.Substring(first + length);
        label.text = prefix + "0" + suffix;
        int generation = _revealGeneration;
        IVisualElementScheduledItem ticker = null;
        var starter = label.schedule.Execute(() =>
        {
            if (generation != _revealGeneration) return;
            float started = Time.unscaledTime;
            float duration = Mathf.Clamp(_config.result.durationMs / 1000f * 0.75f,
                0.25f, 0.40f);
            ticker = label.schedule.Execute(() =>
            {
                if (generation != _revealGeneration)
                {
                    ticker?.Pause();
                    return;
                }
                float t = Mathf.Clamp01((Time.unscaledTime - started) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                label.text = prefix + Mathf.RoundToInt(Mathf.Lerp(0f, end, eased)) + suffix;
                if (t < 1f) return;
                label.text = finalText;
                ticker?.Pause();
            }).Every(16);
            _revealItems.Add(ticker);
        });
        starter.ExecuteLater(Mathf.Max(0, delayMs));
        _revealItems.Add(starter);
    }

    private void PrepareDeathReveal(Label line, bool animate, int index)
    {
        if (!animate)
        {
            ClearRevealStyle(line);
            return;
        }

        line.style.opacity = 0f;
        line.style.translate = new Translate(0f, 8f);
        var duration = new TimeValue(Mathf.Clamp(_config.result.durationMs / 2, 120, 240),
            TimeUnit.Millisecond);
        line.style.transitionProperty = new List<StylePropertyName>
        {
            new StylePropertyName("opacity"),
            new StylePropertyName("translate"),
        };
        line.style.transitionDuration = new List<TimeValue> { duration, duration };
        line.style.transitionTimingFunction = new List<EasingFunction>
        {
            new EasingFunction(EasingMode.EaseOut),
            new EasingFunction(EasingMode.EaseOut),
        };
        int generation = _revealGeneration;
        int delay = Mathf.Min(_config.result.staggerCapMs,
            (index + 3) * _config.result.staggerMs);
        var reveal = line.schedule.Execute(() =>
        {
            if (generation != _revealGeneration) return;
            line.style.opacity = 1f;
            line.style.translate = new Translate(0f, 0f);
        });
        reveal.ExecuteLater(delay);
        _revealItems.Add(reveal);
    }

    private void CancelReveals()
    {
        foreach (var item in _revealItems) item?.Pause();
        _revealItems.Clear();
        _revealGeneration++;
        foreach (var stat in _statPool)
            ClearRevealStyle(stat.ElementAt(1));
        foreach (var death in _deathPool)
            ClearRevealStyle(death);
    }

    private static void ClearRevealStyle(VisualElement element)
    {
        element.style.opacity = StyleKeyword.Null;
        element.style.translate = StyleKeyword.Null;
        element.style.transitionProperty = StyleKeyword.Null;
        element.style.transitionDuration = StyleKeyword.Null;
        element.style.transitionTimingFunction = StyleKeyword.Null;
    }

    private static bool TryFindNumber(string text, out int first, out int length, out int value)
    {
        first = -1;
        length = 0;
        value = 0;
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i])) continue;
            first = i;
            int end = i + 1;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            length = end - first;
            return int.TryParse(text.Substring(first, length), out value);
        }
        return false;
    }

    private static Label Label(string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        return label;
    }
}
