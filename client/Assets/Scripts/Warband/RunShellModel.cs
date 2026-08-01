using System.Collections.Generic;

/// <summary>
/// The run shell's view contract. Same law the Planning screen already follows: the controller
/// builds plain view-models, the views render them and own NO state. Nothing here references
/// Warband.Run types — a view can never reach past its model and mutate the run.
///
/// Extending: add a RunScreen case + a view implementing IRunScreenView + its model struct.
/// Nothing else changes; the router is data-driven.
///
/// Every player-facing string is HYDRATED by the controller through Warband.Sim.Lexicon /
/// Warband.Content.ContentLexicon. No raw content id (`bulwark.warden`) may reach a view — if a
/// view is formatting an id, the lookup belongs in the controller.
/// </summary>
internal enum RunScreen
{
    Menu,
    // Recruit and RevisionDraft retired (workbench-frame): muster and the first-revision
    // choice are Management states — the workbench is THE out-of-combat frame.
    Management,
    Wager,
    Planning,
    Map,
    Shop,
    Deploy,     // place the warband on the board before the fight
    Fight,      // delegated to ReplayPlayer
    RunOver,
}

internal enum RunOverTone { Victory, Defeat }

internal enum PlanningTab
{
    Muster,
    Market,
    Armory,
    Hourstone,
}

internal enum PlanningBeat
{
    Fight,
    RevisionUpgrade,
    Interlude,
    Boss,
    BossReward,
    /// <summary>The authored victory is already banked; choose whether to retire or carry this
    /// exact warband into Beyond the Hour.</summary>
    EndlessChoice,
    /// <summary>Beat #0: the first-revision choice, presented over the muster state the
    /// moment BEGIN RUN is pressed (workbench-frame). The run does not exist yet.</summary>
    StartingRevision,
}

/// <summary>One selectable/inspectable hero, wherever a hero is shown.</summary>
internal sealed class HeroCardModel
{
    public string Id = "";            // chassis id — for the controller's callbacks ONLY, never displayed
    public string Name = "";          // hydrated display name
    public string Role = "";          // one-line identity, from the lexicon
    public string Description = "";
    public string RankLabel = "";     // "Rank C" etc; empty before a run exists
    public string WeaponName = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string RoleIcon = "";
    public string Accent = "";
    public string AbilityIcon = "";
    public string AbilityTrigger = "";
    public string AbilityName = "";
    public string AbilitySummary = "";
    public string PassiveIcon = "";
    public string PassiveTrigger = "";
    public string PassiveName = "";
    public string PassiveSummary = "";
    public string WeaponSummary = "";
    public List<string> KeywordNotes = new List<string>();
    public bool Selected;
    public bool Interactable = true;
    public List<StatChipModel> Stats = new List<StatChipModel>();
    public List<string> Traits = new List<string>();   // hydrated spec-node names

    // Roster address — how the view names this hero back to the controller. Chassis id cannot
    // do it: a warband may hold two Bulwarks, and they are different heroes.
    public bool InBench;
    public int Index = -1;

    // Per-hero shop affordances. The controller decides legality; the view only renders it.
    public bool CanEquip;             // an inventory item is selected and this hero can take it
    public bool CanUnequip;
    public bool CanReforge;
    public string ReforgeLabel = "";
    public string MoveLabel = "";     // "TO BENCH" / "TO FIELD"; empty = move not legal
    public string SellLabel = "";     // "SELL 4"; empty = cannot sell
}

/// <summary>An owned, unequipped item. Selecting one arms the equip action.</summary>
internal sealed class InventoryItemModel
{
    public int Index;
    public string Kind = "";
    public string Name = "";
    public string Detail = "";
    public string SellLabel = "";
    public bool Selected;
}

/// <summary>
/// Deployment: the board IS the screen, so this model only carries the roster chips and the
/// instruction. Hex positions live on the board, not in the UI.
/// </summary>
internal sealed class DeployModel
{
    public string Heading = "";
    public string Instruction = "";
    public bool CanCommit;
    public string PrimaryText = "";
    public int Placed;
    public int Total;
    public List<HeroCardModel> Roster = new List<HeroCardModel>();  // Selected = awaiting a hex
    /// <summary>One row per enemy body. Structured, not a dot-separated string: the row carries
    /// the same facts the combat card does, so the two can never disagree.</summary>
    public List<DeployEnemyRowModel> Enemies = new List<DeployEnemyRowModel>();
    public string EncounterRuleName = "";
    public string EncounterRule = "";
    /// <summary>The shared unit card, bound to whichever enemy row is selected. Null = closed.</summary>
    public InspectorModel SelectedEnemy;
}

/// <summary>One enemy in the deployment preview. Selecting it opens the shared unit card.</summary>
internal sealed class DeployEnemyRowModel
{
    public string Key = "";
    public string Name = "";
    public string Role = "";
    public int Row;
    public int MaxHp;
    public int Attack;
    public int Range;
    public int AttackIntervalTicks;
    public int ManaMax;
    public int ManaPerSwing;
    public int CritChance;
    public int CleavePct;
    public bool HealAutos;
    public string WeaponName = "";
    public string Behavior = "";
    public string Accent = "";
    public bool Selected;
}

/// <summary>A labelled number. Kept generic so new stats never need a new view.</summary>
internal sealed class StatChipModel
{
    public PresentationFactId Id;
    public string Label = "";
    public string Value = "";
    public string Tone = "";          // "" | "good" | "bad" | "warn" — styling hook only
    public string Tooltip = "";
    public string AdvancedTooltip = "";
    public int Priority;

    public StatChipModel() { }
    public StatChipModel(string label, string value, string tone = "",
                         PresentationFactId id = PresentationFactId.Unknown,
                         string tooltip = "", int priority = 0)
    {
        Label = label;
        Value = value;
        Tone = tone;
        Id = id;
        Tooltip = tooltip;
        Priority = priority;
    }
}

internal enum PresentationFactId
{
    Unknown,
    Hp,
    BasicPower,
    Restoration,
    Reach,
    Cadence,
    ManaThreshold,
    ManaPerSwing,
    Protection,
    CritChance,
    Cleave,
    Rank,
    RankDelta,
    FieldCapacity,
    ChoiceCount,
    Scope,
    Duration,
}

/// <summary>
/// Code-native semantic marks used by compact decision surfaces. Glyph ids are presentation
/// vocabulary, not content ids: changing an icon never changes a combat rule.
/// </summary>
internal enum UiGlyphId
{
    Unknown,
    Health,
    Damage,
    Heal,
    Cadence,
    Reach,
    Signature,
    Passive,
    Healer,
    Tank,
    Diver,
    Sniper,
    Zoner,
    Bruiser,
    Frontline,
    Captain,
    Area,
    Regen,
    Stun,
    Shield,
    Burst,
    Leap,
    Line,
    Distance,
    Glyph,
    Burn,
    Frenzy,
    LowHealth,
    Counter,
    Mana,
    Haste,
    Check,
    Lock,
    Hourstone,
}

/// <summary>
/// The Market has a dedicated scan grammar. These are presentation kinds, not run-domain offer
/// kinds: Capacity and Sold are deliberate Market objects even though neither is normal stock.
/// </summary>
internal enum MarketOfferKind
{
    Recruit,
    RankUp,
    Weapon,
    Trinket,
    Inscription,
    Capacity,
    Sold,
}

/// <summary>
/// One fast-reading Market offer. This is derived from the same CardModel used by the selection
/// tray and dossier, so progressive disclosure changes density rather than facts.
/// </summary>
internal sealed class MarketOfferCardModel
{
    public string Key = "";
    public string ContentId = "";
    public MarketOfferKind Kind;
    public string Classification = "";
    public string Title = "";
    public string Subtitle = "";
    public string ArtworkResource = "";
    public string ArtworkFallback = "";
    public string Accent = "";
    public string RuleLabel = "";
    public string RuleName = "";
    public string ExactRule = "";
    public string Qualifier = "";        // e.g. optional crit, kept out of the metric grid
    /// <summary>Unit offers only: "RANK C" / "C → B" + the B/A/S ladder for the commerce-row
    /// tier strip (workbench-refactor). Empty label hides the strip.</summary>
    public string TierLabel = "";
    public List<RankTierSlotModel> PathTiers = new List<RankTierSlotModel>();
    /// <summary>Muster only: 0-based slot this candidate is picked into, -1 = not picked.
    /// Picked cards show the slot badge + MUSTERED state instead of commerce.</summary>
    public int MusterSlot = -1;
    public string Price = "";
    public int CurrencyCost = -1;
    public int CurrencyBalance = -1;
    public string EconomyState = "";
    public bool Selected;
    public bool Affordable = true;
    public bool Frozen;
    public bool Sold;
    public bool Disabled;
    public List<StatChipModel> Metrics = new List<StatChipModel>();
    public CardModel Detail = new CardModel();
}

/// <summary>
/// The one card-data grammar used by draft, market, roster, rewards, and inspector-adjacent
/// lists. The controller decides every label and action; views only render this object.
/// </summary>
internal sealed class CardModel
{
    public string Key = "";               // UI selection address, never shown
    public string ContentId = "";         // Resources/presentation lookup, never shown
    /// <summary>Stable identity for an Armory item. Inventory indices shift after every equip,
    /// so pointer gestures must never carry only <see cref="Key"/>.</summary>
    public long ItemInstanceId;
    /// <summary>Warband.Run.ItemKind for Armory cards; -1 for every non-equipment card.</summary>
    public int EquipmentKind = -1;
    public string Eyebrow = "";
    public string Title = "";
    public string Subtitle = "";
    public string InspectorSubtitle = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string RoleIcon = "";
    public string Accent = "";
    public string Rank = "";
    public string Price = "";
    public int CurrencyCost = -1;
    public string Weapon = "";
    public string AbilityIcon = "";
    public string AbilityTrigger = "";
    public string AbilityName = "";
    public string AbilitySummary = "";
    public string InspectorAbilitySummary = "";
    public int AbilityManaCost = -1;
    public string PassiveIcon = "";
    public string PassiveTrigger = "";
    public string PassiveName = "";
    public string PassiveSummary = "";
    public string WeaponSummary = "";
    /// <summary>The Weapon property/mastery paired with the composed attack facts. The panel
    /// keeps its concise rule visible and uses the exact rule for disclosure.</summary>
    public RuleDeltaModel WeaponProperty;
    public List<string> KeywordNotes = new List<string>();
    public bool Selected;
    /// <summary>
    /// Secondary station selection which remains visibly seated while another card owns focus.
    /// Armory uses this to keep the chosen item pinned while the player chooses its recipient.
    /// </summary>
    public bool Pinned;
    public bool Disabled;
    public bool Sold;
    public bool Frozen;
    public List<StatChipModel> Stats = new List<StatChipModel>();
    public List<string> Tags = new List<string>();
    public List<WarbandSpecBadgeModel> Traits = new List<WarbandSpecBadgeModel>();
    /// <summary>The B/A/S ladder with chosen picks + awaiting slots — the PATH section and the offer tier strips read this (workbench-refactor).</summary>
    public List<RankTierSlotModel> PathTiers = new List<RankTierSlotModel>();
    public string ComparisonTitle = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
    public List<ChoicePreviewModel> ChoicePreviews = new List<ChoicePreviewModel>();
    public RankUpDetailModel RankUpDetail;
    /// <summary>
    /// Structured unit-sheet data. Null keeps non-unit cards on the generic dossier grammar.
    /// Workbench and combat both feed this contract so renderer changes cannot make them drift.
    /// </summary>
    public UnitSheetModel UnitSheet;
}

internal enum HallActionId
{
    Buy,
    Freeze,
    BuySlot,
    Deploy,
    Equip,
    Unequip,
    Reforge,
    Move,
    SellHero,
    SellItem,
    EquipNow,
    KeepShopping,
}

internal sealed class InspectorActionModel
{
    public HallActionId Id;
    public string Label = "";
    public int CurrencyCost = -1;
    public string CurrencySuffix = "";
    public int CurrencyBalance = -1;
    public bool CurrencyGain;
    public bool Primary;
    public bool Enabled = true;
    public string DisabledReason = "";
}

internal enum DecisionDetailKind
{
    Champion,
    Recruit,
    RankUp,
    Weapon,
    Trinket,
    Inscription,
    Capacity,
    Combatant,
}

internal enum InspectorSectionKind
{
    Rule,
    Comparison,
    Choices,
    Capacity,
}

/// <summary>
/// Content role, not geometry: Primary renders in full; Deferred renders as one compact
/// line whose full rule lives on hover. Width decides layout, never what exists
/// (Design/workbench-dossier.md).
/// </summary>
internal enum InspectorSectionRole
{
    Primary,
    Deferred,
}

/// <summary>
/// One semantically named block in a decision detail. Item and run-law details deliberately use
/// this instead of being squeezed into the champion-only Basic / Signature / Passive skeleton.
/// </summary>
internal sealed class InspectorSectionModel
{
    public InspectorSectionKind Kind = InspectorSectionKind.Rule;
    public InspectorSectionRole Role = InspectorSectionRole.Primary;
    public string Label = "";
    public string Icon = "";
    public string Name = "";
    public string Summary = "";
    public UiGlyphId LabelGlyph = UiGlyphId.Unknown;
    public string LabelValue = "";
    public int CapacityBefore;
    public int CapacityAfter;
    public int CapacityMax;
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
    public List<ChoicePreviewModel> Choices = new List<ChoicePreviewModel>();
}

/// <summary>One visible live state chip in the compact combat footer.</summary>
internal sealed class UnitStatusModel
{
    public string Label = "";
    public string Tooltip = "";
    public string Tone = "";
}

/// <summary>
/// The shared hero/enemy sheet contract. Every variable-arity region is an ordered collection:
/// adding a Weapon fact/property or Passive changes an adapter, never the renderer.
/// </summary>
internal sealed class UnitSheetModel
{
    public bool Combat;
    public bool Enemy;
    public List<StatChipModel> CoreFacts = new List<StatChipModel>();
    public string WeaponIcon = "⚔";
    public string WeaponName = "";
    public List<StatChipModel> WeaponFacts = new List<StatChipModel>();
    public List<RuleDeltaModel> WeaponProperties = new List<RuleDeltaModel>();
    public InspectorSectionModel Signature;
    public string PassivesLabel = "PASSIVES";
    public List<InspectorSectionModel> Passives = new List<InspectorSectionModel>();
    public List<RankTierSlotModel> Specs = new List<RankTierSlotModel>();
    public string Targeting = "";
    public List<UnitStatusModel> Statuses = new List<UnitStatusModel>();
}

internal enum SpecRuleContext
{
    Passive,
    BasicAttack,
    Signature,
}

/// <summary>Progressive disclosure for the selected card. Cards compare; this panel explains.</summary>
internal sealed class InspectorModel
{
    public bool Empty;
    /// <summary>Instructional copy for the empty state — the old header brief's new home
    /// (workbench-refactor approval). Blank keeps the panel's default line.</summary>
    public string EmptyHint = "";
    public string Key = "";
    public DecisionDetailKind Kind = DecisionDetailKind.Champion;
    public string Eyebrow = "";
    public string Title = "";
    public string Subtitle = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
    public string AbilityIcon = "";
    public string AbilityTrigger = "";
    public string AbilityName = "";
    public string AbilitySummary = "";
    public int AbilityManaCost = -1;
    public string PassiveIcon = "";
    public string PassiveTrigger = "";
    public string PassiveName = "";
    public string PassiveSummary = "";
    /// <summary>The targeting rule, already phrased for display ("Farthest, held at 5 hexes").
    /// Blank hides the row — only a live combatant has one.</summary>
    public string Targeting = "";
    /// <summary>C / B / A / S. Drives the rank badge's escalation; blank hides it.</summary>
    public string Rank = "";
    public string WeaponIcon = "⚔";
    public string WeaponName = "";
    public string WeaponSummary = "";
    public RuleDeltaModel WeaponProperty;
    public string Price = "";
    public int CurrencyCost = -1;
    public int CurrencyBalance = -1;
    public List<StatChipModel> Stats = new List<StatChipModel>();
    public List<string> Tags = new List<string>();
    public List<WarbandSpecBadgeModel> Traits = new List<WarbandSpecBadgeModel>();
    /// <summary>The B/A/S ladder with chosen picks + awaiting slots — the PATH section and the offer tier strips read this (workbench-refactor).</summary>
    public List<RankTierSlotModel> PathTiers = new List<RankTierSlotModel>();
    public List<string> KeywordNotes = new List<string>();
    public List<InspectorActionModel> Actions = new List<InspectorActionModel>();
    public string ComparisonTitle = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
    public List<ChoicePreviewModel> ChoicePreviews = new List<ChoicePreviewModel>();
    public List<InspectorSectionModel> Sections = new List<InspectorSectionModel>();
    public EquipmentPreviewModel EquipmentPreview;
    public RankUpDetailModel RankUpDetail;
    public UnitSheetModel UnitSheet;
}

internal enum RankTierSlotState
{
    Selected,
    Pending,
    Locked,
}

/// <summary>One durable specialization tier in the C → B → A → S hero progression.</summary>
internal sealed class RankTierSlotModel
{
    public string Rank = "";
    public RankTierSlotState State = RankTierSlotState.Locked;
    public string Icon = "◇";
    public string Name = "";
    public string Summary = "";
    public string Rule = "";
    public string Accent = "";
}

/// <summary>
/// Typed one-page projection for the rank-up decision. The generic dossier section/page model
/// cannot represent this without splitting one purchase decision into implementation pages.
/// </summary>
internal sealed class RankUpDetailModel
{
    public string CurrentRank = "";
    public string NextRank = "";
    public List<RankTierSlotModel> Tiers = new List<RankTierSlotModel>();
    public List<ChoicePreviewModel> Options = new List<ChoicePreviewModel>();
}

internal enum DeltaDirection
{
    Positive,
    Negative,
    Neutral,
    Contextual,
}

internal sealed class StatComparisonModel
{
    public string Label = "";
    public string Before = "";
    public string After = "";
    public string Tone = "";
    public DeltaDirection Direction = DeltaDirection.Neutral;
    public string Explanation = "";
}

internal sealed class RuleDeltaModel
{
    public string RuleName = "";
    /// <summary>Optional compact scan label. RuleName remains the exact tooltip title.</summary>
    public string DisplayName = "";
    public string ShortSummary = "";
    public string FullDescription = "";
    public string Icon = "◇";
    public bool Applies = true;
}

internal sealed class RecipientPreviewModel
{
    public string HeroKey = "";
    public string DisplayName = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string RankText = "";
    public string CurrentItemName = "";
    public bool IsEligible = true;
    public string IneligibleReason = "";
    public bool IsSelected;
}

internal sealed class EquipmentPreviewModel
{
    public string OfferedItemName = "";
    public string SelectedRecipientHeroKey = "";
    public string CurrentItemName = "";
    public List<RecipientPreviewModel> Recipients = new List<RecipientPreviewModel>();
    public List<StatComparisonModel> StatDeltas = new List<StatComparisonModel>();
    public RuleDeltaModel LostRule;
    public RuleDeltaModel GainedRule;
}

internal sealed class ChoicePreviewModel
{
    public string Change = "";
    public string Name = "";
    /// <summary>The authored one-liner (ContentLexicon) shown at the preview tier; machine
    /// prose in Rule is too long to glance. Empty falls back to Rule's first sentence.</summary>
    public string Summary = "";
    public string Rule = "";
    public string Accent = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
}

internal enum PartySlotState
{
    Occupied,
    Empty,
    Locked,
}

internal sealed class PartySlotModel
{
    public string Key = "";
    public int Index;
    public bool Reserve;
    public PartySlotState State;
    public string Name = "";
    public string Rank = "";
    public string Role = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
    public string Weapon = "";
    public string Trinket = "";
    public bool Focused;
    public bool Previewed;
}

internal sealed class StoredItemSummaryModel
{
    public string Key = "";
    public string Name = "";
    public string Kind = "";
    public string Icon = "";
    public string Accent = "";
    public bool Selected;
}

/// <summary>
/// The Hall's persistent, presentation-only roster projection. Six potential field addresses
/// remain visible even when some are locked, so capacity is spatial rather than just a counter.
/// </summary>
internal sealed class PartyShelfModel
{
    public int FieldCapacity;
    public int FieldCount;
    public int MaxFieldCapacity;
    public int ReserveCount;
    public int ReserveCapacity;
    public bool Expanded;
    public string FocusedHeroKey = "";
    public List<PartySlotModel> Field = new List<PartySlotModel>();
    public List<PartySlotModel> Reserve = new List<PartySlotModel>();
    public List<StoredItemSummaryModel> StoredItems = new List<StoredItemSummaryModel>();
    public InspectorModel LoadoutInspector = new InspectorModel { Empty = true };
    public List<CardModel> LoadoutInventory = new List<CardModel>();
}

internal enum WarbandBarMode
{
    Hidden,
    HallEditable,
    WagerReadOnly,
    DeploymentSelect,
    ResultReadOnly,
}

/// <summary>Shared UI-only payload for a champion's equipment socket. Both the persistent
/// warband rail and the Workbench Armory inspect this marker while resolving a drop.</summary>
internal sealed class WarbandEquipmentDropTarget
{
    public long HeroInstanceId;
    public int Kind;
    public bool Armory;
}

/// <summary>Shared UI-only payload for a whole champion card. Gear may be dropped anywhere on
/// the portrait, while roster drags still use the zone and slot fields.</summary>
internal sealed class WarbandRosterDropTarget
{
    public long HeroInstanceId;
    public bool Reserve;
    public int SlotIndex;
    public bool Locked;
}

internal sealed class WarbandEquipmentModel
{
    public int Kind;                         // Warband.Run.ItemKind, kept out of the view contract
    public long ItemInstanceId;
    public string Icon = "";
    public string Name = "";
    public string Tier = "";
    public string Rule = "";
    public bool Empty;
    public bool Starter;
    public bool Transferable;
    public bool Selected;
    public bool ValidTarget;
    public List<StatChipModel> Facts = new List<StatChipModel>();
}

internal sealed class WarbandSpecBadgeModel
{
    public string Rank = "";
    public string Icon = "";
    public string Name = "";
    public string Summary = "";   // authored one-liner (ContentLexicon); Rule is the contract
    public string Rule = "";
    public string Accent = "";
    public SpecRuleContext Context = SpecRuleContext.Passive;
}

internal sealed class WarbandHeroModel
{
    public long HeroInstanceId;
    public int FieldIndex = -1;
    public int SlotIndex = -1;
    public bool Reserve;
    public bool Empty;
    public bool Locked;
    /// <summary>Muster only: the next seat to fill — an invitation, not a locked slot
    /// (workbench-frame). Renders gold-dashed with <see cref="AwaitingLabel"/>.</summary>
    public bool Awaiting;
    public string AwaitingLabel = "";
    public bool Selected;
    public bool Placed;
    public string ClassName = "";
    public string Role = "";
    public string Rank = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
    public WarbandEquipmentModel Weapon = new WarbandEquipmentModel();
    public WarbandEquipmentModel Trinket = new WarbandEquipmentModel();
    public List<WarbandSpecBadgeModel> Specs = new List<WarbandSpecBadgeModel>();
    /// <summary>Kit-row disclosure slot (workbench-refactor): the active ability. Never an
    /// equip target — the tooltip carries the exact rule; forks keep it honest upstream.</summary>
    public string SignatureIcon = "";
    public string SignatureName = "";
    public string SignatureRule = "";
    public int SignatureMana = -1;
}

/// <summary>
/// One shell-owned projection shared by Hall, Wager, Deployment, and the frozen result gate.
/// Screen views never clone it, so portrait and layout trees survive navigation.
/// </summary>
internal sealed class WarbandBarModel
{
    public WarbandBarMode Mode;
    public bool Compact;
    /// <summary>Pre-run muster: seats fill as candidates are picked; the armory chip and
    /// every manage/equip affordance are hidden (nothing exists to store or move).</summary>
    public bool MusterMode;
    /// <summary>The rank-up modal owns the stage (workbench-frame): the shell-owned rail
    /// sits outside the workbench scrim, so it dims itself while the choice is pending.</summary>
    public bool Dimmed;
    public int FieldCount;
    public int FieldCapacity;
    public int MaxFieldCapacity;
    public int ReserveCount;
    public int ReserveCapacity;
    public int StoredItems;
    /// <summary>The Armory chip doubles as the drawer toggle; its hint copy flips on this.</summary>
    public bool ArmoryDrawerOpen;
    public bool CanManage;
    public bool CanEdit;
    public long FocusedHeroInstanceId;
    public long ArmedInventoryItemInstanceId;
    public int ArmedInventoryKind = -1;
    public List<WarbandHeroModel> Field = new List<WarbandHeroModel>();
    public List<WarbandHeroModel> Reserve = new List<WarbandHeroModel>();
}

internal sealed class PlanningTrackNodeModel
{
    public string Label = "";
    public string Kind = "";
    public string State = "";             // past | current | future
}

internal sealed class InterludeChoiceModel
{
    public int Path;
    public int Option;
    public CardModel Card = new CardModel();
    /// <summary>Optional explicit footer used when the whole card is a consequential action.</summary>
    public string ActionLabel = "";
    /// <summary>Optional compact public facts. Ordinary Interludes leave this empty.</summary>
    public List<string> Facts = new List<string>();
}

internal sealed class PlanningModel
{
    /// <summary>Header title. The workbench is THE out-of-combat frame (workbench-frame):
    /// run-play reads WORKBENCH, the pre-run state reads MUSTER YOUR WARBAND.</summary>
    public string Title = "WORKBENCH";
    /// <summary>Pre-run muster state: market = candidates, no prices, no hourstones, no
    /// armory; the continue button is BEGIN RUN.</summary>
    public bool MusterMode;
    public string Act = "";
    public string Beat = "";
    public string Sand = "";
    public string Capacity = "";
    public string Heading = "";
    public string Brief = "";
    public string Rule = "";
    public PlanningBeat BeatKind;
    public PlanningTab ActiveTab;
    public bool CanReroll;
    public string RerollLabel = "";
    public int RerollCost = -1;
    public int CurrencyBalance;
    public bool CanCommit;
    public string CommitLabel = "";
    public bool ShowRisk;
    public List<PlanningTrackNodeModel> Track = new List<PlanningTrackNodeModel>();
    public List<TierChoiceModel> Risks = new List<TierChoiceModel>();
    public List<CardModel> Enemies = new List<CardModel>();
    public List<CardModel> Field = new List<CardModel>();
    public List<CardModel> Bench = new List<CardModel>();
    public List<CardModel> Market = new List<CardModel>();
    public List<MarketOfferCardModel> MarketOffers = new List<MarketOfferCardModel>();
    public List<CardModel> Armory = new List<CardModel>();
    public List<CardModel> Inscriptions = new List<CardModel>();
    public List<InterludeChoiceModel> Interlude = new List<InterludeChoiceModel>();
    public InspectorModel Inspector = new InspectorModel();
    public bool InspectorOpen;
    public SpecChoiceModel SpecChoice = new SpecChoiceModel();
    public bool SlotOfferOpen;
    public string SlotOfferText = "";
    public bool SlotAffordable;
    public bool HallOverview;
    public HallStation ActiveStation = HallStation.Overview;
    public HallStation RecommendedStation = HallStation.Market;
    public bool ReducedMotion;
    public bool ForcePhoneLayout;
    public List<HallStationModel> Stations = new List<HallStationModel>();
    public PartyShelfModel PartyShelf = new PartyShelfModel();
}

internal sealed class HallStationModel
{
    public HallStation Station;
    public string Eyebrow = "";
    public string Name = "";
    public string Status = "";
    public string Action = "";
    public string Sigil = "";
    public bool Attention;
    public bool Enabled = true;
}

internal sealed class ResultStatModel
{
    public string Label = "";
    public string Value = "";
    public string Tone = "";
}

internal sealed class ResultGateModel
{
    public bool Open;
    public bool Victory;
    public string Eyebrow = "";
    public string Heading = "";
    public string Summary = "";
    public string ContinueLabel = "";
    public string Recommendation = "";
    public bool CanWatchAgain;
    public List<ResultStatModel> Stats = new List<ResultStatModel>();
    public List<string> Deaths = new List<string>();

    /// <summary>The post-fight charts. This is the one model here that is a Warband.Sim type,
    /// and it qualifies for the same reason the rest of this file is plain: CombatRecap IS a
    /// view-model — a fold over the event log with its strings already hydrated through
    /// Lexicon — not live battle state. Re-declaring its four rows client-side would move
    /// tested arithmetic into an untestable assembly. Null until a fight resolves.</summary>
    public Warband.Sim.CombatRecap Recap;
}

/// <summary>
/// The wager is a commitment screen, not an encounter browser. It exposes the stakes and reward
/// curve first; the concrete opposing formation is revealed only after the wager is locked.
/// </summary>
internal sealed class WagerModel
{
    public string Act = "";
    public string Beat = "";
    public string Sand = "";
    public string Heading = "";
    public string Brief = "";
    public string Disclosure = "";
    public string ContinueLabel = "";
    public bool CanContinue;
    public List<PlanningTrackNodeModel> Track = new List<PlanningTrackNodeModel>();
    public List<TierChoiceModel> Risks = new List<TierChoiceModel>();
}

internal sealed class MenuModel
{
    public string Title = "WARBAND";
    public string Tagline = "";
    public string SeedLabel = "";
    public bool CanContinue;          // a run is in progress, in memory or on disk
    public string VersionLine = "";
}

/// <summary>One node on the act track. The whole act is shown up front: ADR 0016 / pve-encounters
/// says the player sees what is coming — nothing about the route is hidden.</summary>
internal sealed class MapNodeModel
{
    public string Label = "";
    public string Kind = "";          // "Fight" | "Event" | "Boss"
    public bool IsCurrent;
    public bool IsPast;
}

/// <summary>A wager tier choice. PLACEHOLDER economy (ADR 0007) — surfaced honestly rather than
/// hidden, so the shell shows what the run layer actually does today.</summary>
internal sealed class TierChoiceModel
{
    public int Index;
    public string Name = "";
    public string Risk = "";
    public string Reward = "";
    public int CurrencyReward = -1;
    public bool Selected;
}

internal sealed class MapModel
{
    public string ActLabel = "";
    public string NodeLabel = "";
    public string Gold = "";
    public string NodeKind = "";
    public string NodeHeading = "";
    public string NodeBlurb = "";
    public string PrimaryText = "";
    public bool PrimaryEnabled = true;
    public bool ShowTiers;
    public List<TierChoiceModel> Tiers = new List<TierChoiceModel>();
    public List<MapNodeModel> Track = new List<MapNodeModel>();
    public List<HeroCardModel> Warband = new List<HeroCardModel>();
    /// <summary>Boss/encounter preview — always populated for a Boss node (formations are
    /// previewed before deployment, pve-encounters law).</summary>
    public List<string> EnemyPreview = new List<string>();
    public string EncounterRule = "";
}

internal sealed class ShopOfferModel
{
    public int Index;
    public string Kind = "";
    public string Name = "";
    public string Detail = "";
    public string Price = "";
    public bool Affordable;
    public bool Frozen;
    public bool Sold;
}

/// <summary>One card in a rank-up offer. Index is the argument to RunController.ChooseSpec.</summary>
internal sealed class SpecOptionModel
{
    public string Name = "";
    public string Text = "";
    public string Change = "";
    /// <summary>Glyph shown on the option tile and previewed inside the hero card's
    /// awaiting path slot while the option is hovered (workbench-frame modal).</summary>
    public string Icon = "◆";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
}

/// <summary>The rank-up choice. Blocking: while pending, every other shop action is
/// refused by the run layer, so the view must present it as a modal, not a side panel.
/// Options is however many the run layer drew — views must render the list, never assume
/// two, because a non-fork rank draws a subset of a pool that may be wider.
/// Presented by the workbench rank-up modal (workbench-frame approval): the hero's
/// progression card sits center-stage and the bind fills its awaiting path slot.</summary>
internal sealed class SpecChoiceModel
{
    public bool Pending;
    public string HeroName = "";
    public string RankLabel = "";
    /// <summary>True when this rank is the chassis' fork — the eyebrow names THE FORK.</summary>
    public bool Fork;
    public string FromRank = "";
    public string ToRank = "";
    /// <summary>The flat bump this rank granted (applied at purchase), e.g.
    /// "+30 HEALTH · +2 POWER". Empty hides the line.</summary>
    public string BumpText = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
    public string SignatureIcon = "";
    public bool WeaponFilled;
    public bool TrinketFilled;
    /// <summary>The hero's B/A/S ladder with the pending rank in the Pending state — the
    /// modal card's awaiting slot reads this.</summary>
    public List<RankTierSlotModel> PathTiers = new List<RankTierSlotModel>();
    public List<SpecOptionModel> Options = new List<SpecOptionModel>();
}

internal sealed class ShopModel
{
    public string Heading = "";
    public string Gold = "";
    public string RerollCost = "";
    public bool CanReroll;
    public List<ShopOfferModel> Offers = new List<ShopOfferModel>();
    public List<HeroCardModel> Field = new List<HeroCardModel>();
    public List<HeroCardModel> Bench = new List<HeroCardModel>();
    public SpecChoiceModel SpecChoice = new SpecChoiceModel();
    public List<InventoryItemModel> Inventory = new List<InventoryItemModel>();
    public int SelectedItemIndex = -1;   // -1 = nothing armed for equipping
    public string InventoryHint = "";
    public bool SlotOfferOpen;
    public string SlotOfferText = "";
    public bool SlotAffordable;
    public string ContinueText = "";
}

internal sealed class RunOverModel
{
    public RunOverTone Tone;
    public string Heading = "";
    public string Summary = "";
    public List<StatChipModel> Stats = new List<StatChipModel>();
    public List<HeroCardModel> FinalWarband = new List<HeroCardModel>();
}

/// <summary>The whole shell state for one frame. One object in, one render out.</summary>
internal sealed class RunShellModel
{
    public RunScreen Screen = RunScreen.Menu;
    public MenuModel Menu = new MenuModel();
    public WagerModel Wager = new WagerModel();
    public PlanningModel Planning = new PlanningModel();
    public MapModel Map = new MapModel();
    public ShopModel Shop = new ShopModel();
    public DeployModel Deploy = new DeployModel();
    public RunOverModel RunOver = new RunOverModel();
    public ResultGateModel Result = new ResultGateModel();
    public WarbandBarModel WarbandBar = new WarbandBarModel();
}
