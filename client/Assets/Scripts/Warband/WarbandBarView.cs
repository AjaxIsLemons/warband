using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shell-owned warband instrument. Its visual tree outlives individual run screens and only
/// updates changed tile state, which keeps portraits and layout resident during navigation.
/// </summary>
internal sealed class WarbandBarView : IDisposable
{
    private sealed class GearTarget
    {
        public long HeroId;
        public int Kind;
        public bool Armory;
    }

    private sealed class HeroTile
    {
        public readonly VisualElement Root = new VisualElement();
        public readonly VisualElement Portrait = new VisualElement();
        public readonly Label Fallback = new Label();
        public readonly Label Rank = new Label();
        public readonly Label ClassName = new Label();
        public readonly VisualElement Specs = new VisualElement();
        public readonly VisualElement Weapon = new VisualElement();
        public readonly Label WeaponIcon = new Label();
        public readonly Label WeaponTier = new Label();
        public readonly VisualElement Trinket = new VisualElement();
        public readonly Label TrinketIcon = new Label();
        public WarbandHeroModel Model = new WarbandHeroModel();
        public string SpecSignature = "";

        public HeroTile()
        {
            Root.AddToClassList("warband-hero");
            Root.focusable = true;
            Root.tabIndex = 0;

            Portrait.AddToClassList("warband-hero__portrait");
            Fallback.AddToClassList("warband-hero__fallback");
            Portrait.Add(Fallback);
            Root.Add(Portrait);

            Rank.AddToClassList("warband-hero__rank");
            Root.Add(Rank);
            Specs.AddToClassList("warband-hero__specs");
            Root.Add(Specs);
            ClassName.AddToClassList("warband-hero__class");
            Root.Add(ClassName);

            var gear = new VisualElement();
            gear.AddToClassList("warband-hero__gear");
            Weapon.AddToClassList("warband-gear");
            Weapon.AddToClassList("warband-gear--weapon");
            Weapon.focusable = true;
            Weapon.tabIndex = 0;
            WeaponIcon.AddToClassList("warband-gear__icon");
            WeaponTier.AddToClassList("warband-gear__tier");
            Weapon.Add(WeaponIcon);
            Weapon.Add(WeaponTier);
            Trinket.AddToClassList("warband-gear");
            Trinket.AddToClassList("warband-gear--trinket");
            Trinket.focusable = true;
            Trinket.tabIndex = 0;
            TrinketIcon.AddToClassList("warband-gear__icon");
            Trinket.Add(TrinketIcon);
            gear.Add(Weapon);
            gear.Add(Trinket);
            Root.Add(gear);
        }
    }

    private const float DragThreshold = 7f;

    private readonly RunShellActions _actions;
    private readonly VisualElement _shellRoot;
    private readonly VisualElement _root;
    private readonly Button _manage;
    private readonly Label _manageLabel;
    private readonly Label _capacity;
    private readonly VisualElement _scroll;
    private readonly VisualElement _field;
    private readonly VisualElement _reserveGroup;
    private readonly VisualElement _reserve;
    private readonly Label _reserveCount;
    private readonly Button _armory;
    private readonly Label _armoryCount;
    private readonly VisualElement _dragGhost;
    private readonly Label _dragGhostIcon;
    private readonly Label _dragGhostName;
    private readonly RuntimeTooltipService _tooltip;
    private readonly Dictionary<long, HeroTile> _tiles = new Dictionary<long, HeroTile>();
    private readonly List<VisualElement> _gearTargets = new List<VisualElement>();
    private readonly Dictionary<string, Texture2D> _portraitCache =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);

    private WarbandBarModel _model = new WarbandBarModel();
    private string _layoutSignature = "";
    private VisualElement _dragSource;
    private long _dragHeroId;
    private int _dragKind = -1;
    private int _dragPointerId = -1;
    private Vector2 _dragStart;
    private bool _dragging;
    private long _selectedSourceHeroId;
    private int _selectedKind = -1;
    private long _lastArmedInventoryItemInstanceId;

    public VisualElement Root => _root;

    public WarbandBarView(RunShellActions actions, VisualElement shellRoot,
                          RuntimeTooltipService tooltip)
    {
        _actions = actions;
        _shellRoot = shellRoot;

        _root = new VisualElement { name = "persistent-warband-bar" };
        _root.AddToClassList("warband-bar");
        _root.pickingMode = PickingMode.Position;
        _shellRoot.Add(_root);

        _manage = new Button(ManageFocused);
        _manage.AddToClassList("warband-bar__identity");
        var title = new Label("WARBAND");
        title.AddToClassList("warband-bar__eyebrow");
        _capacity = new Label();
        _capacity.AddToClassList("warband-bar__capacity");
        _manageLabel = new Label("MANAGE WARBAND");
        _manageLabel.AddToClassList("warband-bar__manage");
        _manage.Add(title);
        _manage.Add(_capacity);
        _manage.Add(_manageLabel);
        _root.Add(_manage);

        _scroll = new VisualElement();
        _scroll.AddToClassList("warband-bar__scroll");
        var strip = new VisualElement();
        strip.AddToClassList("warband-bar__strip");
        _field = new VisualElement();
        _field.AddToClassList("warband-bar__slots");
        strip.Add(_field);

        _reserveGroup = new VisualElement();
        _reserveGroup.AddToClassList("warband-bar__reserve");
        _reserveCount = new Label();
        _reserveCount.AddToClassList("warband-bar__section");
        _reserveGroup.Add(_reserveCount);
        _reserve = new VisualElement();
        _reserve.AddToClassList("warband-bar__slots");
        _reserveGroup.Add(_reserve);
        strip.Add(_reserveGroup);
        _scroll.Add(strip);
        _root.Add(_scroll);

        _armory = new Button(UseArmory);
        _armory.AddToClassList("warband-bar__armory");
        _armory.userData = new GearTarget { Armory = true };
        var armoryTitle = new Label("ARMORY");
        armoryTitle.AddToClassList("warband-bar__section");
        _armoryCount = new Label();
        _armoryCount.AddToClassList("warband-bar__armory-count");
        var armoryHint = new Label("DROP TO UNEQUIP");
        armoryHint.AddToClassList("warband-bar__armory-hint");
        _armory.Add(armoryTitle);
        _armory.Add(_armoryCount);
        _armory.Add(armoryHint);
        _root.Add(_armory);

        _dragGhost = new VisualElement();
        _dragGhost.AddToClassList("warband-drag-ghost");
        _dragGhost.pickingMode = PickingMode.Ignore;
        _dragGhostIcon = new Label();
        _dragGhostIcon.AddToClassList("warband-drag-ghost__icon");
        _dragGhostName = new Label();
        _dragGhostName.AddToClassList("warband-drag-ghost__name");
        _dragGhost.Add(_dragGhostIcon);
        _dragGhost.Add(_dragGhostName);
        _shellRoot.Add(_dragGhost);
        SetDisplayed(_dragGhost, false);

        _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        SetDisplayed(_root, false);
    }

    public void Bind(WarbandBarModel model)
    {
        _model = model ?? new WarbandBarModel();
        if (_model.ArmedInventoryItemInstanceId > 0 &&
            _model.ArmedInventoryItemInstanceId != _lastArmedInventoryItemInstanceId)
            UiPolishSignals.Emit(UiPolishSignals.Cue.SocketWake,
                sourceId: "workbench-armory", targetId: "workbench-dossier",
                tone: UiFeedbackTone.Positive);
        _lastArmedInventoryItemInstanceId = _model.ArmedInventoryItemInstanceId;
        bool shown = _model.Mode != WarbandBarMode.Hidden;
        SetDisplayed(_root, shown);
        _shellRoot.EnableInClassList("shell-has-warband-bar", shown);
        if (!shown)
        {
            CancelDrag();
            _tooltip.Hide();
            return;
        }

        _root.EnableInClassList("warband-bar--editable", _model.CanEdit);
        _root.EnableInClassList("warband-bar--compact", _model.Compact);
        _root.EnableInClassList("warband-bar--deployment",
            _model.Mode == WarbandBarMode.DeploymentSelect);
        _root.EnableInClassList("warband-bar--readonly", !_model.CanEdit);
        if (_model.Compact)
        {
            _root.Insert(0, _scroll);
            _root.Insert(1, _manage);
        }
        else
        {
            _root.Insert(0, _manage);
            _root.Insert(1, _scroll);
        }
        _capacity.text = $"FIELD  {_model.FieldCount} / {_model.FieldCapacity}";
        _reserveCount.text = $"RESERVE  {_model.ReserveCount} / {_model.ReserveCapacity}";
        _armoryCount.text = _model.StoredItems == 1 ? "1 STORED" : $"{_model.StoredItems} STORED";
        _manageLabel.text = _model.CanManage ? "MANAGE WARBAND" : "WAR BAND";
        _manage.SetEnabled(_model.CanManage);

        string signature = LayoutSignature(_model);
        if (!string.Equals(signature, _layoutSignature, StringComparison.Ordinal))
        {
            RebuildTiles();
            _layoutSignature = signature;
        }

        _selectedSourceHeroId = 0;
        _selectedKind = -1;
        foreach (WarbandHeroModel hero in _model.Field) UpdateTile(hero);
        foreach (WarbandHeroModel hero in _model.Reserve) UpdateTile(hero);
        SetDisplayed(_reserveGroup, !_model.Compact && _model.Reserve.Count > 0);
        SetDisplayed(_armory, !_model.Compact || HasSelectedEquipment());
        _armory.SetEnabled(_model.CanManage || HasSelectedEquipment());
        _armory.EnableInClassList("warband-bar__armory--drop-target",
            _model.CanEdit && HasSelectedEquipment());
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string EditorResolvedLayoutReport()
    {
        var report = new UiLayoutReport("Permanent warband rail");
        UiLayoutContract.RequireResolved(report, _root, "root");
        UiLayoutContract.RequireInside(report, _manage, _root, "warband command", 4f);
        UiLayoutContract.RequireInside(report, _scroll, _root, "warband strip", 4f);
        if (_armory.resolvedStyle.display != DisplayStyle.None)
            UiLayoutContract.RequireInside(report, _armory, _root, "armory command", 4f);
        UiLayoutContract.RequireClassInside(
            report, _root, "warband-hero", _scroll, tolerance: 5f);
        UiLayoutContract.RequireNoScrollView(report, _root, "Permanent warband rail");
        UiLayoutContract.RequireMinimumFont(report, _root, "warband-hero__class", 13f);
        UiLayoutContract.RequireMinimumFont(report, _root, "warband-bar__manage", 13f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, _root, "warband-hero__class", 10f);
        UiLayoutContract.RequireSingleLineTextFits(
            report, _root, "warband-hero__class", 3f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _root, "warband-bar__manage", 3f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _root, "warband-bar__armory-hint", 3f);
        return report.ToString();
    }
#endif

    private void RebuildTiles()
    {
        CancelDrag();
        _tiles.Clear();
        _gearTargets.Clear();
        _field.Clear();
        _reserve.Clear();
        foreach (WarbandHeroModel hero in _model.Field)
            AddTile(hero, _field);
        foreach (WarbandHeroModel hero in _model.Reserve)
            AddTile(hero, _reserve);
        SetDisplayed(_reserveGroup, !_model.Compact && _model.Reserve.Count > 0);
    }

    private void AddTile(WarbandHeroModel model, VisualElement parent)
    {
        var tile = new HeroTile { Model = model };
        tile.Root.RegisterCallback<ClickEvent>(evt =>
        {
            var target = evt.target as VisualElement;
            if (target != null &&
                (target == tile.Weapon || target == tile.Trinket ||
                 tile.Weapon.Contains(target) || tile.Trinket.Contains(target)))
                return;
            if (tile.Model.HeroInstanceId > 0)
                _actions.FocusWarbandHero?.Invoke(tile.Model.HeroInstanceId);
        });
        tile.Root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
            if (tile.Model.HeroInstanceId > 0)
                _actions.FocusWarbandHero?.Invoke(tile.Model.HeroInstanceId);
            evt.StopPropagation();
        });
        AttachTooltip(tile.Root, () => HeroTooltip(tile.Model));
        ConfigureGear(tile, tile.Weapon, 0);
        ConfigureGear(tile, tile.Trinket, 1);
        if (model.HeroInstanceId <= 0)
        {
            tile.Root.EnableInClassList("warband-hero--empty", model.Empty);
            tile.Root.EnableInClassList("warband-hero--locked", model.Locked);
            tile.Fallback.text = model.Locked ? "◇" : "+";
            tile.ClassName.text = model.Locked ? "LOCKED" : "OPEN";
            tile.Rank.text = (model.FieldIndex + 1).ToString();
            SetDisplayed(tile.Weapon, false);
            SetDisplayed(tile.Trinket, false);
        }
        parent.Add(tile.Root);
        if (model.HeroInstanceId > 0) _tiles[model.HeroInstanceId] = tile;
    }

    private void ConfigureGear(HeroTile tile, VisualElement element, int kind)
    {
        element.userData = new GearTarget
        {
            HeroId = tile.Model.HeroInstanceId,
            Kind = kind,
        };
        _gearTargets.Add(element);
        element.RegisterCallback<PointerDownEvent>(evt => BeginGearDrag(evt, tile, element, kind));
        element.RegisterCallback<PointerMoveEvent>(evt => MoveGearDrag(evt, element));
        element.RegisterCallback<PointerUpEvent>(evt => EndGearDrag(evt, tile, element, kind));
        element.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
            ActivateGear(tile, kind);
            evt.StopPropagation();
        });
        AttachTooltip(element, () => GearTooltip(tile.Model, kind));
    }

    private void UpdateTile(WarbandHeroModel model)
    {
        if (model.HeroInstanceId <= 0) return;
        if (!_tiles.TryGetValue(model.HeroInstanceId, out HeroTile tile)) return;
        tile.Model = model;
        tile.Root.EnableInClassList("warband-hero--selected", model.Selected);
        tile.Root.EnableInClassList("warband-hero--reserve", model.Reserve);
        tile.Root.EnableInClassList("warband-hero--placed", model.Placed);
        tile.Root.EnableInClassList("warband-hero--locked", model.Locked);
        tile.Root.EnableInClassList("accent--" + model.Accent, true);
        tile.Rank.text = model.Rank;
        tile.ClassName.text = model.ClassName.ToUpperInvariant();
        tile.Fallback.text = model.PortraitFallback;
        Texture2D portrait = LoadPortrait(model.PortraitResource);
        tile.Portrait.style.backgroundImage = portrait == null
            ? new StyleBackground(StyleKeyword.None)
            : new StyleBackground(Background.FromTexture2D(portrait));
        SetDisplayed(tile.Fallback, portrait == null);

        string specSignature = SpecSignature(model.Specs);
        if (!string.Equals(tile.SpecSignature, specSignature, StringComparison.Ordinal))
        {
            tile.Specs.Clear();
            foreach (WarbandSpecBadgeModel spec in model.Specs)
            {
                var badge = new Label(spec.Icon);
                badge.AddToClassList("warband-spec");
                badge.AddToClassList("accent--" + spec.Accent);
                badge.focusable = true;
                badge.tabIndex = 0;
                AttachTooltip(badge, () => new RuntimeTooltipModel
                {
                    Kind = RuntimeTooltipKind.General,
                    Family = MechanicPresentation.Family(spec.Name),
                    Eyebrow = $"RANK {spec.Rank} SPECIALIZATION",
                    Title = spec.Name,
                    Domain = "SPECIALIZATION",
                    Body = spec.Rule,
                    Context = tile.Model.ClassName,
                });
                tile.Specs.Add(badge);
            }
            tile.SpecSignature = specSignature;
        }

        UpdateGear(tile.Weapon, tile.WeaponIcon, tile.WeaponTier, model.Weapon);
        UpdateGear(tile.Trinket, tile.TrinketIcon, null, model.Trinket);
        tile.Weapon.userData = new GearTarget { HeroId = model.HeroInstanceId, Kind = 0 };
        tile.Trinket.userData = new GearTarget { HeroId = model.HeroInstanceId, Kind = 1 };
        if (model.Weapon.Selected)
        {
            _selectedSourceHeroId = model.HeroInstanceId;
            _selectedKind = 0;
        }
        if (model.Trinket.Selected)
        {
            _selectedSourceHeroId = model.HeroInstanceId;
            _selectedKind = 1;
        }
    }

    private static void UpdateGear(VisualElement root, Label icon, Label tier,
                                   WarbandEquipmentModel model)
    {
        icon.text = model.Icon;
        if (tier != null) tier.text = model.Tier;
        root.EnableInClassList("warband-gear--empty", model.Empty);
        root.EnableInClassList("warband-gear--starter", model.Starter);
        root.EnableInClassList("warband-gear--selected", model.Selected);
        root.EnableInClassList("warband-gear--valid", model.ValidTarget);
        root.EnableInClassList("warband-gear--transferable", model.Transferable);
    }

    private void BeginGearDrag(PointerDownEvent evt, HeroTile tile, VisualElement element, int kind)
    {
        if (evt.button != 0 || _dragPointerId >= 0 || !_model.CanEdit) return;
        WarbandEquipmentModel gear = kind == 0 ? tile.Model.Weapon : tile.Model.Trinket;
        if (!gear.Transferable) return;
        _dragSource = element;
        _dragHeroId = tile.Model.HeroInstanceId;
        _dragKind = kind;
        _dragStart = new Vector2(evt.position.x, evt.position.y);
        _dragPointerId = evt.pointerId;
        _dragging = false;
        _dragGhostIcon.text = gear.Icon;
        _dragGhostName.text = gear.Name;
        element.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void MoveGearDrag(PointerMoveEvent evt, VisualElement element)
    {
        if (evt.pointerId != _dragPointerId || !ReferenceEquals(element, _dragSource)) return;
        Vector2 position = new Vector2(evt.position.x, evt.position.y);
        if (!_dragging && Vector2.Distance(position, _dragStart) >= DragThreshold)
        {
            _dragging = true;
            _tooltip.Hide();
            if (_model.Compact) SetDisplayed(_armory, true);
            SetDropHighlights(true);
        }
        if (_dragging)
        {
            _dragGhost.style.left = position.x + 14f;
            _dragGhost.style.top = position.y + 14f;
            SetDisplayed(_dragGhost, true);
        }
        evt.StopPropagation();
    }

    private void EndGearDrag(PointerUpEvent evt, HeroTile tile, VisualElement element, int kind)
    {
        if (evt.pointerId != _dragPointerId || !ReferenceEquals(element, _dragSource)) return;
        Vector2 position = new Vector2(evt.position.x, evt.position.y);
        if (element.HasPointerCapture(evt.pointerId)) element.ReleasePointer(evt.pointerId);

        if (_dragging)
        {
            GearTarget target = PickTarget(position);
            long source = _dragHeroId;
            int sourceKind = _dragKind;
            CancelDrag();
            if (target?.Armory == true)
                _actions.UnequipWarbandEquipment?.Invoke(source, sourceKind);
            else if (target != null && target.Kind == sourceKind && target.HeroId != source)
                _actions.TransferWarbandEquipment?.Invoke(source, sourceKind, target.HeroId);
        }
        else
        {
            CancelDrag();
            ActivateGear(tile, kind);
        }
        evt.StopPropagation();
    }

    private void ActivateGear(HeroTile tile, int kind)
    {
        if (!_model.CanEdit) return;
        WarbandEquipmentModel gear = kind == 0 ? tile.Model.Weapon : tile.Model.Trinket;
        if (_model.ArmedInventoryItemInstanceId > 0 &&
            _model.ArmedInventoryKind == kind)
        {
            UiPolishSignals.Emit(UiPolishSignals.Cue.ProjectedTarget,
                sourceId: "workbench-armory", targetId: "workbench-dossier",
                tone: UiFeedbackTone.Positive,
                transaction: UiTransactionKind.Equip);
            _actions.EquipSelectedWarbandItem?.Invoke(tile.Model.HeroInstanceId);
            return;
        }
        if (_selectedSourceHeroId > 0 && _selectedKind == kind &&
            _selectedSourceHeroId != tile.Model.HeroInstanceId)
        {
            _actions.TransferWarbandEquipment?.Invoke(
                _selectedSourceHeroId, kind, tile.Model.HeroInstanceId);
            return;
        }
        if (gear.Transferable)
        {
            UiPolishSignals.Emit(
                gear.Selected ? UiPolishSignals.Cue.Unpin : UiPolishSignals.Cue.Pin,
                targetId: "workbench-dossier", tone: UiFeedbackTone.Preview);
            _actions.SelectWarbandEquipment?.Invoke(tile.Model.HeroInstanceId, kind);
        }
    }

    private GearTarget PickTarget(Vector2 panelPosition)
    {
        VisualElement picked = _shellRoot.panel?.Pick(panelPosition);
        while (picked != null)
        {
            if (picked.userData is GearTarget target) return target;
            picked = picked.parent;
        }
        return null;
    }

    private void SetDropHighlights(bool enabled)
    {
        foreach (VisualElement target in _gearTargets)
        {
            bool legal = false;
            if (enabled && target.userData is GearTarget data)
                legal = data.Kind == _dragKind && data.HeroId != _dragHeroId;
            target.EnableInClassList("warband-gear--drop-target", legal);
        }
        _armory.EnableInClassList("warband-bar__armory--drop-target", enabled);
    }

    private void CancelDrag()
    {
        if (_dragSource != null && _dragPointerId >= 0 &&
            _dragSource.HasPointerCapture(_dragPointerId))
            _dragSource.ReleasePointer(_dragPointerId);
        _dragSource = null;
        _dragHeroId = 0;
        _dragKind = -1;
        _dragPointerId = -1;
        _dragging = false;
        SetDisplayed(_dragGhost, false);
        SetDropHighlights(false);
        if (_model.Compact && !HasSelectedEquipment()) SetDisplayed(_armory, false);
    }

    private void UseArmory()
    {
        if (_selectedSourceHeroId > 0 && _selectedKind >= 0 && _model.CanEdit)
        {
            _actions.UnequipWarbandEquipment?.Invoke(_selectedSourceHeroId, _selectedKind);
            return;
        }
        ManageFocused();
    }

    private void ManageFocused()
    {
        if (!_model.CanManage) return;
        _actions.ManageWarbandHero?.Invoke(_model.FocusedHeroInstanceId);
    }

    private bool HasSelectedEquipment() =>
        _selectedSourceHeroId > 0 || _model.ArmedInventoryItemInstanceId > 0;

    private Texture2D LoadPortrait(string resource)
    {
        if (string.IsNullOrEmpty(resource)) return null;
        if (_portraitCache.TryGetValue(resource, out Texture2D cached)) return cached;
        Texture2D loaded = Resources.Load<Texture2D>(resource);
        _portraitCache[resource] = loaded;
        return loaded;
    }

    private void AttachTooltip(VisualElement anchor, Func<RuntimeTooltipModel> copy) =>
        _tooltip.Attach(anchor, copy);

    private static RuntimeTooltipModel HeroTooltip(WarbandHeroModel hero)
    {
        var body = new StringBuilder();
        body.Append(hero.Role);
        body.Append("\nWeapon: ").Append(hero.Weapon.Name);
        body.Append("\nTrinket: ").Append(hero.Trinket.Empty ? "Empty" : hero.Trinket.Name);
        if (hero.Specs.Count > 0)
        {
            body.Append("\nSpecializations: ");
            for (int i = 0; i < hero.Specs.Count; i++)
            {
                if (i > 0) body.Append(" · ");
                body.Append(hero.Specs[i].Name);
            }
        }
        return new RuntimeTooltipModel
        {
            Kind = RuntimeTooltipKind.General,
            Family = MechanicPresentation.Family(hero.Role),
            Eyebrow = $"RANK {hero.Rank} · {hero.Role}",
            Title = hero.ClassName,
            Domain = "CHAMPION",
            Body = body.ToString(),
            Footer = "Select for full dossier",
        };
    }

    private static RuntimeTooltipModel GearTooltip(WarbandHeroModel hero, int kind)
    {
        WarbandEquipmentModel gear = kind == 0 ? hero.Weapon : hero.Trinket;
        RuntimeTooltipModel tooltip = RuntimeTooltipModel.Equipment(
            kind == 0
                ? (gear.Starter ? "CHASSIS STARTER" : $"{gear.Tier} WEAPON")
                : "TRINKET",
            gear.Empty ? "Empty socket" : gear.Name,
            gear.Empty ? "No trinket equipped." : gear.Rule,
            gear.Facts,
            gear.Empty
                ? $"{hero.ClassName} · EMPTY"
                : $"{hero.ClassName} · EQUIPPED · ACTIVE");
        tooltip.Footer = gear.Empty
            ? "Open Armory to assign a compatible item."
            : "Select to pin the full dossier · Drag to transfer";
        return tooltip;
    }

    private static string LayoutSignature(WarbandBarModel model)
    {
        var text = new StringBuilder();
        text.Append(model.Compact ? "compact|" : "expanded|");
        AppendLayout(text, model.Field);
        text.Append('/');
        AppendLayout(text, model.Reserve);
        return text.ToString();
    }

    private static string SpecSignature(IReadOnlyList<WarbandSpecBadgeModel> specs)
    {
        var text = new StringBuilder();
        foreach (WarbandSpecBadgeModel spec in specs)
            text.Append(spec.Rank).Append(':')
                .Append(spec.Icon).Append(':')
                .Append(spec.Name).Append(':')
                .Append(spec.Accent).Append('|');
        return text.ToString();
    }

    private static void AppendLayout(StringBuilder text, IReadOnlyList<WarbandHeroModel> heroes)
    {
        foreach (WarbandHeroModel hero in heroes)
            text.Append(hero.HeroInstanceId).Append(':')
                .Append(hero.Empty ? 'e' : hero.Locked ? 'l' : 'o').Append('|');
    }

    public void Dispose()
    {
        CancelDrag();
        _dragGhost.RemoveFromHierarchy();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool EditorShowFirstEquipmentTooltip()
    {
        foreach (HeroTile tile in _tiles.Values)
        {
            if (tile.Model?.HeroInstanceId <= 0) continue;
            _tooltip.EditorShow(tile.Weapon, GearTooltip(tile.Model, 0));
            return true;
        }
        return false;
    }
#endif

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
