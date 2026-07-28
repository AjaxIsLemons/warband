using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Desktop opening draft. Five direct-select MusterCards compare the same three facts, while
/// exact mechanics live in each portrait lens. The three sockets are both progress and party
/// order; no separate counter or universal-card footer is involved.
/// </summary>
internal sealed class RecruitView : IRunScreenView, IRunScreenLifecycle, IDisposable
{
    private sealed class SelectionSlot
    {
        public readonly VisualElement Root = new VisualElement();
        public readonly VisualElement Portrait = new VisualElement();
        public readonly Label Fallback = new Label();
        public readonly Label Number = new Label();
        private string _key = "";
        private int _motionGeneration;

        public SelectionSlot(int index)
        {
            Root.AddToClassList("muster-slot");
            Root.AddToClassList("muster-slot--" + (index + 1));
            Portrait.AddToClassList("muster-slot__portrait");
            Fallback.AddToClassList("muster-slot__fallback");
            Number.AddToClassList("muster-slot__number");
            Number.text = (index + 1).ToString();
            Portrait.Add(Fallback);
            Root.Add(Portrait);
            Root.Add(Number);
        }

        public string Key => _key;

        public void Bind(MusterSelectionSlotModel model, int compactMs, bool reducedMotion)
        {
            string nextKey = model?.ChampionKey ?? "";
            bool compacted = !string.IsNullOrEmpty(_key) &&
                             !string.Equals(_key, nextKey, StringComparison.Ordinal);
            _key = model?.ChampionKey ?? "";
            Root.userData = "muster-slot:" + (model?.Index ?? 0);
            Root.EnableInClassList("muster-slot--filled", model?.Filled == true);
            Root.tooltip = model?.Filled == true
                ? $"Pick {(model.Index + 1)}: {model.Name}"
                : $"Pick {(model?.Index + 1) ?? 1}: empty";
            Fallback.text = model?.PortraitFallback ?? "";

            Texture2D texture = model == null || string.IsNullOrEmpty(model.PortraitResource)
                ? null
                : Resources.Load<Texture2D>(model.PortraitResource);
            Portrait.style.backgroundImage = texture == null
                ? new StyleBackground(StyleKeyword.None)
                : new StyleBackground(Background.FromTexture2D(texture));
            SetDisplayed(Fallback, model?.Filled == true && texture == null);
            WarbandCard.SetAccent(Root, model?.Accent ?? "");
            if (compacted)
            {
                int generation = ++_motionGeneration;
                Root.style.transitionDuration =
                    new List<TimeValue> { new TimeValue(0f, TimeUnit.Millisecond) };
                Root.style.opacity = reducedMotion ? 0.72f : 0.78f;
                Root.style.translate = reducedMotion
                    ? new Translate(0f, 0f)
                    : new Translate(7f, 0f);
                Root.schedule.Execute(() =>
                {
                    if (generation != _motionGeneration) return;
                    var duration = new TimeValue(
                        reducedMotion ? 80 : Mathf.Max(1, compactMs),
                        TimeUnit.Millisecond);
                    Root.style.transitionProperty = new List<StylePropertyName>
                    {
                        new StylePropertyName("opacity"),
                        new StylePropertyName("translate"),
                    };
                    Root.style.transitionDuration = new List<TimeValue>
                        { duration, duration };
                    Root.style.opacity = 1f;
                    Root.style.translate = new Translate(0f, 0f);
                }).ExecuteLater(16);
            }
        }
    }

    private readonly RunShellActions _actions;
    private readonly List<MusterCard> _cards = new List<MusterCard>();
    private readonly List<string> _cardTargets = new List<string>();
    private readonly List<SelectionSlot> _slots = new List<SelectionSlot>();
    private readonly HubPresentationConfig _presentation;
    private readonly UiFxLayer _fx;
    private readonly UiFeedbackDirector _polish;
    private readonly VisualElement _root;
    private readonly Label _heading;
    private readonly Label _instruction;
    private readonly VisualElement _grid;
    private readonly Label _empty;
    private readonly Label _feedback;
    private readonly VisualElement _slotRail;
    private readonly VisualElement _footer;
    private readonly Label _footerHint;
    private readonly Button _begin;

    private string _offerGeneration = "";
    private bool _active;
    private bool _ready;
    private bool _reducedMotion;
    private IVisualElementScheduledItem _readyTask;

    public RunScreen Screen => RunScreen.Recruit;
    public VisualElement Root => _root;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int EditorActiveEffectCount => _fx.ActiveEffectCount;
#endif

    public RecruitView(RunShellActions actions, UiFeedbackServices services = null)
    {
        _actions = actions;
        _presentation = HubPresentationConfig.Load();

        _root = new VisualElement();
        _root.AddToClassList("shell-screen");
        _root.AddToClassList("shell-column");
        _root.AddToClassList("recruit-screen");

        var header = new VisualElement();
        header.AddToClassList("recruit-header");
        _root.Add(header);

        _heading = new Label();
        _heading.AddToClassList("shell-title");
        header.Add(_heading);
        _instruction = new Label();
        _instruction.AddToClassList("shell-subtitle");
        header.Add(_instruction);

        _grid = new VisualElement();
        _grid.AddToClassList("recruit-card-grid");
        _root.Add(_grid);

        _empty = new Label("No champions answered the Hour.");
        _empty.AddToClassList("empty-note");
        _root.Add(_empty);

        _footer = new VisualElement();
        _footer.AddToClassList("recruit-footer");
        _root.Add(_footer);

        var party = new VisualElement();
        party.AddToClassList("recruit-party");
        var partyTitle = new Label("YOUR WARBAND");
        partyTitle.AddToClassList("recruit-party__title");
        party.Add(partyTitle);
        _slotRail = new VisualElement();
        _slotRail.AddToClassList("recruit-party__slots");
        party.Add(_slotRail);
        _footer.Add(party);

        var footerCopy = new VisualElement();
        footerCopy.AddToClassList("recruit-footer__copy");
        _footerHint = new Label();
        _footerHint.AddToClassList("recruit-footer__hint");
        _feedback = new Label();
        _feedback.AddToClassList("recruit-feedback");
        _feedback.pickingMode = PickingMode.Ignore;
        footerCopy.Add(_footerHint);
        footerCopy.Add(_feedback);
        _footer.Add(footerCopy);

        _begin = new Button(() => _actions?.BeginRun?.Invoke()) { text = "SELECT 3 CHAMPIONS" };
        _begin.AddToClassList("btn");
        _begin.AddToClassList("btn--primary");
        _begin.AddToClassList("recruit-begin");
        var readySweep = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
        };
        readySweep.AddToClassList("recruit-begin__sweep");
        _begin.Add(readySweep);
        _footer.Add(_begin);

        _fx = new UiFxLayer(_presentation);
        _root.Add(_fx);
        _polish = new UiFeedbackDirector(_root, _presentation, _fx,
            services?.Haptics, services?.Audio);
        _polish.RegisterTarget("muster-feedback", _feedback);
        _polish.RegisterTarget("muster-begin", _begin);
        _polish.AttachInteractable(_begin, () => "muster-begin");
        _polish.SetActive(false);
    }

    public void Bind(RunShellModel model)
    {
        RecruitModel recruit = model.Recruit;
        _reducedMotion = recruit.ReducedMotion;
        _root.EnableInClassList("motion--reduced", _reducedMotion);
        _polish.SetReducedMotion(_reducedMotion);

        _heading.text = recruit.Heading;
        _instruction.text = recruit.Instruction;
        _feedback.text = recruit.Feedback;
        _feedback.EnableInClassList("recruit-feedback--error", recruit.FeedbackIsError);
        SetDisplayed(_feedback, !string.IsNullOrEmpty(recruit.Feedback));

        SyncCards(recruit.Offer);
        SyncSlots(recruit.Slots);
        SetDisplayed(_empty, recruit.Offer.Count == 0);

        int remaining = Math.Max(0, recruit.Capacity - recruit.Picked);
        _footerHint.text = recruit.CanBegin
            ? "Ready. Pick order is shown left to right; click a champion again to change it."
            : remaining == 1
                ? "Choose one more champion."
                : $"Choose {remaining} champions.";
        _begin.text = recruit.CanBegin ? "BEGIN RUN  ›" : "SELECT 3 CHAMPIONS";
        _begin.SetEnabled(recruit.CanBegin);
        _root.EnableInClassList("recruit-screen--ready", recruit.CanBegin);

        if (_active && !string.Equals(_offerGeneration, recruit.OfferGeneration,
                StringComparison.Ordinal))
        {
            _offerGeneration = recruit.OfferGeneration;
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].PlayReveal(i, _reducedMotion);
        }

        if (_active && !_ready && recruit.CanBegin) PlayReady();
        _ready = recruit.CanBegin;
        MusterPresentationContract.Validate(recruit.Offer);
    }

    public void OnScreenEntered()
    {
        _active = true;
        _offerGeneration = "";
        _polish.SetActive(true);
    }

    public void OnScreenExited()
    {
        _active = false;
        _readyTask?.Pause();
        _readyTask = null;
        _root.RemoveFromClassList("recruit-screen--ready-sweep");
        foreach (MusterCard card in _cards) card.CancelPresentation();
        _polish.SetActive(false);
    }

    public void Dispose()
    {
        OnScreenExited();
        _polish.Dispose();
    }

    public bool ValidateResolvedLayout()
    {
        bool valid = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        foreach (MusterCard card in _cards) valid &= card.ValidateResolvedLayout();
        var report = new UiLayoutReport("Muster");
        UiLayoutContract.RequireResolved(report, _root, "root");
        UiLayoutContract.RequireNoScrollView(report, _root, "Muster");
        UiLayoutContract.RequireNonBlocking(report, _feedback, "Muster feedback");
        UiLayoutContract.RequireAbove(report, _grid, _footer, "card rail / selection footer");
        UiLayoutContract.RequireMinimumFont(
            report, _root, "recruit-footer__hint", 16f);
        UiLayoutContract.RequireMinimumFont(
            report, _root, "muster-lens__body", 16f);
        UiLayoutContract.RequireMinimumFont(
            report, _root, "muster-card__class", 13f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, _root, "recruit-footer__hint", 12.5f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _root, "recruit-footer__hint");
        if (!report.Passed)
        {
            Debug.LogError("[Muster Layout] " + report);
            valid = false;
        }
        const float epsilon = 0.75f;
        if (_grid.panel != null)
        {
            bool gridEscapes = _grid.worldBound.yMax >
                               _root.worldBound.yMax - _footer.resolvedStyle.height + epsilon;
            if (gridEscapes)
            {
                Debug.LogError("[Muster Layout] Card rail overlaps the selection footer.");
                valid = false;
            }
        }
#endif
        return valid;
    }

    public void PreviewLens(int cardIndex, MusterLensTarget target)
    {
        if (cardIndex < 0 || cardIndex >= _cards.Count) return;
        foreach (MusterCard card in _cards) card.PreviewLens(MusterLensTarget.None);
        _cards[cardIndex].PreviewLens(target);
    }

    public void PreviewReveal()
    {
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].PlayReveal(i, _reducedMotion);
    }

    public void PreviewBlocked()
    {
        VisualElement target = _cards.Count > 0 ? _cards[0].Root : _feedback;
        _polish.Error(target);
    }

    public void PreviewReady() => PlayReady();

    private void SyncCards(IReadOnlyList<MusterCardModel> offer)
    {
        while (_cards.Count > offer.Count)
        {
            int last = _cards.Count - 1;
            _polish.UnregisterTarget(_cardTargets[last], _cards[last].Root);
            _cards[last].Root.RemoveFromHierarchy();
            _cards.RemoveAt(last);
            _cardTargets.RemoveAt(last);
        }

        while (_cards.Count < offer.Count)
        {
            var card = new MusterCard(id => _actions?.ToggleRecruit?.Invoke(id),
                _presentation.muster);
            _cards.Add(card);
            _cardTargets.Add("");
            _grid.Add(card.Root);
            _polish.AttachInteractable(card.Root,
                () => card.Root.userData as string ?? "");
        }

        for (int i = 0; i < offer.Count; i++)
        {
            string target = "muster:" + offer[i].Key;
            if (!string.Equals(_cardTargets[i], target, StringComparison.Ordinal))
            {
                _polish.UnregisterTarget(_cardTargets[i], _cards[i].Root);
                _cardTargets[i] = target;
                _polish.RegisterTarget(target, _cards[i].Root);
            }
            _cards[i].Bind(offer[i]);
        }
    }

    private void SyncSlots(IReadOnlyList<MusterSelectionSlotModel> models)
    {
        while (_slots.Count > models.Count)
        {
            int last = _slots.Count - 1;
            _polish.UnregisterTarget("muster-slot:" + last, _slots[last].Root);
            _slots[last].Root.RemoveFromHierarchy();
            _slots.RemoveAt(last);
        }
        while (_slots.Count < models.Count)
        {
            int index = _slots.Count;
            var slot = new SelectionSlot(index);
            _slots.Add(slot);
            _slotRail.Add(slot.Root);
            _polish.RegisterTarget("muster-slot:" + index, slot.Root);
        }
        for (int i = 0; i < models.Count; i++)
            _slots[i].Bind(models[i], _presentation.muster.slotCompactMs, _reducedMotion);
    }

    private void PlayReady()
    {
        _readyTask?.Pause();
        _root.RemoveFromClassList("recruit-screen--ready-sweep");
        _root.AddToClassList("recruit-screen--ready-sweep");
        _polish.Commit(null, _begin, UiFeedbackTone.Positive, _presentation.muster.ready);
        _readyTask = _root.schedule.Execute(() =>
            _root.RemoveFromClassList("recruit-screen--ready-sweep"));
        _readyTask.ExecuteLater(_presentation.muster.ready.durationMs +
                                _presentation.muster.ready.settleMs);
    }

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
