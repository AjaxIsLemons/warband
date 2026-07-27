using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// The shop: offers, inventory, roster, and the blocking 1-of-2 rank-up choice. Densest screen in
/// the shell, so it reads top-down — what you can buy, what it costs, what you own but have not
/// equipped, then who you already have.
///
/// Equipping is a two-step intent: select an inventory item, then press EQUIP on the hero that
/// should take it. The view holds neither half — the selection lives on the model
/// (`InventoryItemModel.Selected`) and legality arrives per hero as `CanEquip`.
///
/// While a spec choice is pending the run layer refuses every other shop action, so the shop is
/// genuinely disabled underneath the modal rather than merely covered by it.
///
/// Pure renderer per IRunScreenView: tree built once, Bind re-renders from ShopModel, no state.
/// </summary>
internal sealed class ShopView : IRunScreenView
{
    private readonly RunShellActions _actions;

    private readonly VisualElement _root;
    /// <summary>Everything the spec modal blocks. The scrim is deliberately not in here.</summary>
    private readonly VisualElement[] _blocked;
    private readonly Label _heading;
    private readonly Label _gold;
    private readonly Button _reroll;
    private readonly VisualElement _offers;
    private readonly Label _offersEmpty;
    private readonly VisualElement _slotPanel;
    private readonly Label _slotText;
    private readonly Button _slotBuy;
    private readonly Label _feedback;
    private readonly Label _inventoryHint;
    private readonly VisualElement _inventory;
    private readonly Label _inventoryEmpty;
    private readonly VisualElement _field;
    private readonly Label _fieldEmpty;
    private readonly VisualElement _bench;
    private readonly Label _benchEmpty;
    private readonly Button _continue;

    private readonly VisualElement _scrim;
    private readonly Label _specHero;
    private readonly Label _specRank;
    private readonly Label _optionAName;
    private readonly Label _optionAText;
    private readonly Label _optionBName;
    private readonly Label _optionBText;

    public RunScreen Screen => RunScreen.Shop;

    public VisualElement Root => _root;

    public ShopView(RunShellActions actions)
    {
        _actions = actions;

        _root = new VisualElement();
        _root.AddToClassList("shell-screen");
        _root.AddToClassList("shell-column");

        var topbar = new VisualElement();
        topbar.AddToClassList("topbar");
        _root.Add(topbar);

        _heading = MakeLabel("topbar__item");
        topbar.Add(_heading);
        _gold = MakeLabel("gold-pill");
        topbar.Add(_gold);

        _reroll = new Button(() => _actions.Reroll?.Invoke());
        _reroll.AddToClassList("btn");
        _reroll.AddToClassList("btn--ghost");
        topbar.Add(_reroll);

        // Offers plus both roster sections outgrow the panel easily. `shell-screen` is inset-0
        // absolute, so the root has a definite height and this one structural inline style is
        // what makes the middle band take the leftover space and scroll.
        var scroll = new ScrollView();
        scroll.style.flexGrow = 1f;
        _root.Add(scroll);

        var column = new VisualElement();
        column.AddToClassList("shell-column");
        scroll.Add(column);

        // MODEL GAP: ShopModel names only the shop itself (Heading); the section headings and the
        // empty notes below are static chrome.
        column.Add(MakeLabel("section-heading", "OFFERS"));

        _offers = new VisualElement();
        _offers.AddToClassList("card-grid");
        column.Add(_offers);

        _offersEmpty = MakeLabel("empty-note", "Nothing on the shelf.");
        column.Add(_offersEmpty);

        _slotPanel = new VisualElement();
        _slotPanel.AddToClassList("panel");
        column.Add(_slotPanel);

        _slotText = MakeLabel("body-copy");
        _slotPanel.Add(_slotText);

        // MODEL GAP: no model string labels the slot purchase button.
        _slotBuy = new Button(() => _actions.BuySlot?.Invoke()) { text = "BUY SLOT" };
        _slotBuy.AddToClassList("btn");
        _slotBuy.AddToClassList("btn--primary");
        _slotPanel.Add(_slotBuy);

        // Inventory sits above the roster: an item has to be armed here before any hero card can
        // offer EQUIP, so the player reads it in the order they act in.
        column.Add(MakeLabel("section-heading", "INVENTORY"));

        _inventoryHint = MakeLabel("body-copy");
        column.Add(_inventoryHint);

        _inventory = new VisualElement();
        _inventory.AddToClassList("card-grid");
        column.Add(_inventory);

        _inventoryEmpty = MakeLabel("empty-note", "Nothing stowed.");
        column.Add(_inventoryEmpty);

        column.Add(MakeLabel("section-heading", "FIELD"));

        _field = new VisualElement();
        _field.AddToClassList("card-grid");
        column.Add(_field);

        _fieldEmpty = MakeLabel("empty-note", "No one deployed.");
        column.Add(_fieldEmpty);

        column.Add(MakeLabel("section-heading", "BENCH"));

        _bench = new VisualElement();
        _bench.AddToClassList("card-grid");
        column.Add(_bench);

        _benchEmpty = MakeLabel("empty-note", "Bench empty.");
        column.Add(_benchEmpty);

        var footer = new VisualElement();
        footer.AddToClassList("topbar");
        _root.Add(footer);

        _continue = new Button(() => _actions.LeaveShop?.Invoke());
        _continue.AddToClassList("btn");
        _continue.AddToClassList("btn--primary");
        footer.Add(_continue);

        _blocked = new VisualElement[] { topbar, scroll, footer };

        // `.feedback-label` is authored in SkirmishStyles as an absolutely-positioned toast
        // (bottom: 374px, centred, fixed width), so it has to hang off the inset-0 `.shell-screen`
        // root — inside the scrolling column it would float over the roster instead. It ignores
        // picking so a message can never swallow a click on the cards underneath it.
        _feedback = MakeLabel("feedback-label");
        _feedback.pickingMode = PickingMode.Ignore;
        _root.Add(_feedback);

        // Sibling of the shop, not a child: `.modal-scrim` is absolute against `.shell-screen`,
        // and a scrim nested inside the shop would be disabled along with it.
        _scrim = new VisualElement();
        _scrim.AddToClassList("modal-scrim");
        _root.Add(_scrim);

        var modal = new VisualElement();
        modal.AddToClassList("modal");
        _scrim.Add(modal);

        _specRank = MakeLabel("eyebrow");
        modal.Add(_specRank);
        _specHero = MakeLabel("shell-title");
        modal.Add(_specHero);

        var optionA = new Button(() => _actions.ChooseSpec?.Invoke(0));
        optionA.AddToClassList("btn");
        optionA.AddToClassList("btn--primary");
        _optionAName = MakeLabel("card-title");
        optionA.Add(_optionAName);
        _optionAText = MakeLabel("card-body");
        optionA.Add(_optionAText);
        modal.Add(optionA);

        var optionB = new Button(() => _actions.ChooseSpec?.Invoke(1));
        optionB.AddToClassList("btn");
        optionB.AddToClassList("btn--primary");
        _optionBName = MakeLabel("card-title");
        optionB.Add(_optionBName);
        _optionBText = MakeLabel("card-body");
        optionB.Add(_optionBText);
        modal.Add(optionB);
    }

    public void Bind(RunShellModel model)
    {
        ShopModel shop = model.Shop;
        SpecChoiceModel spec = shop.SpecChoice;

        _heading.text = shop.Heading;
        _gold.text = shop.Gold;
        // MODEL GAP: ShopModel has no ready-made reroll button label, only the bare cost.
        _reroll.text = "REROLL  " + shop.RerollCost;
        _reroll.SetEnabled(shop.CanReroll);

        RebuildOffers(shop.Offers);

        SetDisplayed(_slotPanel, shop.SlotOfferOpen);
        _slotText.text = shop.SlotOfferText;
        _slotBuy.SetEnabled(shop.SlotAffordable);

        _feedback.text = shop.Feedback;
        _feedback.EnableInClassList("feedback-label--error", shop.FeedbackIsError);
        SetDisplayed(_feedback, !string.IsNullOrEmpty(shop.Feedback));

        _inventoryHint.text = shop.InventoryHint;
        SetDisplayed(_inventoryHint, !string.IsNullOrEmpty(shop.InventoryHint));
        RebuildInventory(shop.Inventory);

        RebuildRoster(_field, _fieldEmpty, shop.Field);
        RebuildRoster(_bench, _benchEmpty, shop.Bench);

        _continue.text = shop.ContinueText;

        // The run layer refuses every non-spec action while the choice is pending, so the shop
        // must not merely look blocked — it has to be blocked.
        SetDisplayed(_scrim, spec.Pending);
        foreach (var section in _blocked)
            section.SetEnabled(!spec.Pending);

        _specRank.text = spec.RankLabel;
        _specHero.text = spec.HeroName;
        _optionAName.text = spec.OptionAName;
        MechanicPresentation.BindInline(_optionAText, spec.OptionAText);
        _optionBName.text = spec.OptionBName;
        MechanicPresentation.BindInline(_optionBText, spec.OptionBText);
    }

    private void RebuildOffers(List<ShopOfferModel> offers)
    {
        _offers.Clear();
        SetDisplayed(_offersEmpty, offers.Count == 0);

        foreach (var offer in offers)
        {
            int index = offer.Index;
            bool buyable = !offer.Sold && offer.Affordable;

            var card = new VisualElement();
            card.AddToClassList("card");
            // Unaffordable stays on screen — the player has to see what they are short of.
            card.EnableInClassList("card--disabled", !buyable);
            if (buyable)
                card.RegisterCallback<ClickEvent>(_ => _actions.BuyOffer?.Invoke(index));

            card.Add(MakeLabel("eyebrow", offer.Kind));
            card.Add(MakeLabel("card-title", offer.Name));
            card.Add(MakeLabel("card-body", offer.Detail));
            card.Add(MakeLabel("gold-pill", offer.Price));

            // MODEL GAP: no model string labels the freeze toggle or its frozen state.
            var freeze = new Button(() => _actions.ToggleFreeze?.Invoke(index))
            {
                text = offer.Frozen ? "FROZEN" : "FREEZE",
            };
            freeze.AddToClassList("btn");
            freeze.AddToClassList(offer.Frozen ? "btn--primary" : "btn--ghost");
            // Freezing is not buying — keep the click off the card underneath. Freezing what you
            // cannot afford yet is the whole point, so this stays live on a disabled card.
            freeze.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            freeze.SetEnabled(!offer.Sold);
            card.Add(freeze);

            _offers.Add(card);
        }
    }

    private void RebuildInventory(List<InventoryItemModel> items)
    {
        _inventory.Clear();
        SetDisplayed(_inventoryEmpty, items.Count == 0);

        foreach (var item in items)
        {
            int index = item.Index;
            bool selected = item.Selected;

            var card = new VisualElement();
            card.AddToClassList("card");
            card.EnableInClassList("card--selected", selected);
            // Clicking the armed item disarms it: the player must be able to back out of an equip
            // they changed their mind about without buying or selling anything.
            card.RegisterCallback<ClickEvent>(_ => _actions.SelectItem?.Invoke(selected ? -1 : index));

            card.Add(MakeLabel("eyebrow", item.Kind));
            card.Add(MakeLabel("card-title", item.Name));
            card.Add(MakeLabel("card-body", item.Detail));

            if (!string.IsNullOrEmpty(item.SellLabel))
            {
                var sell = new Button(() => _actions.SellItem?.Invoke(index)) { text = item.SellLabel };
                sell.AddToClassList("btn");
                sell.AddToClassList("btn--ghost");
                // Selling is not selecting — keep the click off the card underneath.
                sell.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                card.Add(sell);
            }

            _inventory.Add(card);
        }
    }

    private void RebuildRoster(VisualElement grid, Label emptyNote, List<HeroCardModel> heroes)
    {
        grid.Clear();
        SetDisplayed(emptyNote, heroes.Count == 0);
        foreach (var hero in heroes)
            grid.Add(BuildHeroCard(hero));
    }

    /// <summary>Informs first — rank, weapon and stats — then offers whatever the controller says
    /// this hero can actually do right now. An action the model has not enabled is not drawn at
    /// all: a card of dead buttons reads as a broken shop, not as a locked one.</summary>
    private VisualElement BuildHeroCard(HeroCardModel hero)
    {
        var card = new VisualElement();
        card.AddToClassList("card");
        card.EnableInClassList("card--selected", hero.Selected);
        card.EnableInClassList("card--disabled", !hero.Interactable);

        if (!string.IsNullOrEmpty(hero.RankLabel))
            card.Add(MakeLabel("eyebrow", hero.RankLabel));
        card.Add(MakeLabel("card-title", hero.Name));
        card.Add(MakeLabel("card-role", hero.Role));
        if (!string.IsNullOrEmpty(hero.WeaponName))
            card.Add(MakeLabel("card-body", hero.WeaponName));

        if (hero.Stats.Count > 0)
        {
            var stats = new VisualElement();
            stats.AddToClassList("stat-row");
            foreach (var stat in hero.Stats)
                stats.Add(BuildStatChip(stat));
            card.Add(stats);
        }

        if (hero.Traits.Count > 0)
        {
            var traits = new VisualElement();
            traits.AddToClassList("trait-row");
            foreach (var trait in hero.Traits)
                traits.Add(MakeLabel("trait-pill", trait));
            card.Add(traits);
        }

        AddHeroActions(card, hero);

        return card;
    }

    /// <summary>
    /// The hero's own action row. `(InBench, Index)` is the address the controller answers to —
    /// the chassis id cannot be, since a warband may hold two of the same hero.
    /// </summary>
    private void AddHeroActions(VisualElement card, HeroCardModel hero)
    {
        bool inBench = hero.InBench;
        int index = hero.Index;

        var row = new VisualElement();
        row.AddToClassList("role-action-row");

        // MODEL GAP: no model string labels equip or unequip; reforge, move and sell are hydrated.
        if (hero.CanEquip)
            row.Add(MakeActionButton("EQUIP", () => _actions.EquipSelected?.Invoke(inBench, index)));

        if (hero.CanUnequip)
            row.Add(MakeActionButton("UNEQUIP", () => _actions.UnequipWeapon?.Invoke(inBench, index)));

        // Label-gated as well as flag-gated: a button with nothing written on it is as dead as one
        // that does nothing.
        if (hero.CanReforge && !string.IsNullOrEmpty(hero.ReforgeLabel))
            row.Add(MakeActionButton(hero.ReforgeLabel, () => _actions.Reforge?.Invoke(inBench, index)));

        if (!string.IsNullOrEmpty(hero.MoveLabel))
            row.Add(MakeActionButton(hero.MoveLabel, () => _actions.MoveHero?.Invoke(inBench, index)));

        if (!string.IsNullOrEmpty(hero.SellLabel))
            row.Add(MakeActionButton(hero.SellLabel, () => _actions.SellHero?.Invoke(inBench, index)));

        // An empty row still eats layout, so a hero with nothing legal to do gets no row.
        if (row.childCount > 0)
            card.Add(row);
    }

    private static Button MakeActionButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.AddToClassList("btn");
        button.AddToClassList("btn--ghost");
        return button;
    }

    private static VisualElement BuildStatChip(StatChipModel stat)
    {
        var chip = new MechanicStatTile("stat-chip", "stat-chip");
        chip.Bind(stat);
        return chip;
    }

    private static Label MakeLabel(string className, string text = "")
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private static void SetDisplayed(VisualElement element, bool displayed)
    {
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
