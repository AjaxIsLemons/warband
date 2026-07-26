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
    Recruit,
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
    Interlude,
    Boss,
    BossReward,
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
    public string Feedback = "";
    public bool FeedbackIsError;
    public bool CanCommit;
    public string PrimaryText = "";
    public int Placed;
    public int Total;
    public List<HeroCardModel> Roster = new List<HeroCardModel>();  // Selected = awaiting a hex
    public List<string> EnemyPreview = new List<string>();
    public string EncounterRule = "";
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
    Reach,
    Cadence,
    ManaThreshold,
    ManaPerSwing,
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
}

internal enum MusterFactKind
{
    Health,
    Basic,
    Reach,
}

/// <summary>One of the three fixed scan facts on every opening-draft card.</summary>
internal sealed class MusterFactModel
{
    public PresentationFactId Id;
    public MusterFactKind Kind;
    public UiGlyphId Icon;
    public UiGlyphId SecondaryIcon;
    public string Value = "";
    public string SecondaryValue = "";
    public string AccessibleLabel = "";
    public string TooltipTitle = "";
    public string TooltipBody = "";
}

internal enum MusterRuleKind
{
    Signature,
    Passive,
}

/// <summary>
/// A named combat rule. The short keyword is for scanning; ExactRule is generated from the same
/// authored primitives the simulation reads and is disclosed in the portrait lens.
/// </summary>
internal sealed class MusterRuleModel
{
    public MusterRuleKind Kind;
    public UiGlyphId Icon;
    public UiGlyphId KeywordIcon;
    public string Name = "";
    public string Keyword = "";
    public string Context = "";
    public string ExactRule = "";
    public string AdvancedRule = "";
    public int ManaCost = -1;
    public List<string> KeywordNotes = new List<string>();
}

/// <summary>
/// Dedicated opening-draft projection. It deliberately cannot carry weapon footers, flavor
/// paragraphs, price, rank, or an arbitrary list of stats.
/// </summary>
internal sealed class MusterCardModel
{
    public string Key = "";
    public string Name = "";
    public string Role = "";
    public UiGlyphId RoleIcon;
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
    public bool Selected;
    public bool CanToggle = true;
    public string DisabledReason = "";
    public int SelectedSlot = -1;
    public List<MusterFactModel> Facts = new List<MusterFactModel>();
    public MusterRuleModel Signature = new MusterRuleModel
        { Kind = MusterRuleKind.Signature };
    public MusterRuleModel Passive = new MusterRuleModel
        { Kind = MusterRuleKind.Passive };
}

internal sealed class MusterSelectionSlotModel
{
    public int Index;
    public bool Filled;
    public string ChampionKey = "";
    public string Name = "";
    public string PortraitResource = "";
    public string PortraitFallback = "";
    public string Accent = "";
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
    public string Price = "";
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
/// The one visual card grammar used by draft, market, roster, rewards, and inspector-adjacent
/// lists. The controller decides every label and action; WarbandCard only renders this object.
/// </summary>
internal sealed class CardModel
{
    public string Key = "";               // UI selection address, never shown
    public string ContentId = "";         // Resources/presentation lookup, never shown
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
    public string Weapon = "";
    public string AbilityIcon = "";
    public string AbilityTrigger = "";
    public string AbilityName = "";
    public string AbilitySummary = "";
    public string InspectorAbilitySummary = "";
    public string PassiveIcon = "";
    public string PassiveTrigger = "";
    public string PassiveName = "";
    public string PassiveSummary = "";
    public string WeaponSummary = "";
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
    public string ComparisonTitle = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
    public List<ChoicePreviewModel> ChoicePreviews = new List<ChoicePreviewModel>();
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
    public bool Primary;
    public bool Enabled = true;
    public string DisabledReason = "";
}

/// <summary>Progressive disclosure for the selected card. Cards compare; this panel explains.</summary>
internal sealed class InspectorModel
{
    public bool Empty;
    public string Key = "";
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
    public string PassiveIcon = "";
    public string PassiveTrigger = "";
    public string PassiveName = "";
    public string PassiveSummary = "";
    public string WeaponIcon = "⚔";
    public string WeaponName = "";
    public string WeaponSummary = "";
    public string Price = "";
    public List<StatChipModel> Stats = new List<StatChipModel>();
    public List<string> Tags = new List<string>();
    public List<string> KeywordNotes = new List<string>();
    public List<InspectorActionModel> Actions = new List<InspectorActionModel>();
    public string ComparisonTitle = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
    public List<ChoicePreviewModel> ChoicePreviews = new List<ChoicePreviewModel>();
}

internal sealed class StatComparisonModel
{
    public string Label = "";
    public string Before = "";
    public string After = "";
    public string Tone = "";
}

internal sealed class ChoicePreviewModel
{
    public string Change = "";
    public string Name = "";
    public string Rule = "";
    public List<StatComparisonModel> Comparisons = new List<StatComparisonModel>();
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
}

internal sealed class PlanningModel
{
    public string Act = "";
    public string Beat = "";
    public string Sand = "";
    public string Capacity = "";
    public string Heading = "";
    public string Brief = "";
    public string Rule = "";
    public string Feedback = "";
    public bool FeedbackIsError;
    public PlanningBeat BeatKind;
    public PlanningTab ActiveTab;
    public bool CanReroll;
    public string RerollLabel = "";
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
}

internal sealed class HallStationModel
{
    public HallStation Station;
    public string Eyebrow = "";
    public string Name = "";
    public string Status = "";
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
    /// <summary>Why CONTINUE just failed, if it did — a discarded save must not fail silently.</summary>
    public string Notice = "";
    public string VersionLine = "";
}

internal sealed class RecruitModel
{
    public string Heading = "";
    public string Instruction = "";
    public string Feedback = "";
    public bool FeedbackIsError;
    public bool ReducedMotion;
    public int Picked;
    public int Capacity;
    public bool CanBegin;
    public string OfferGeneration = "";
    public List<MusterCardModel> Offer = new List<MusterCardModel>();
    public List<MusterSelectionSlotModel> Slots = new List<MusterSelectionSlotModel>();
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

/// <summary>The 1-of-2 rank-up choice. Blocking: while pending, every other shop action is
/// refused by the run layer, so the view must present it as a modal, not a side panel.</summary>
internal sealed class SpecChoiceModel
{
    public bool Pending;
    public string HeroName = "";
    public string RankLabel = "";
    public string OptionAName = "";
    public string OptionAText = "";
    public string OptionAChange = "";
    public List<StatComparisonModel> OptionAComparisons = new List<StatComparisonModel>();
    public string OptionBName = "";
    public string OptionBText = "";
    public string OptionBChange = "";
    public List<StatComparisonModel> OptionBComparisons = new List<StatComparisonModel>();
}

internal sealed class ShopModel
{
    public string Heading = "";
    public string Gold = "";
    public string RerollCost = "";
    public bool CanReroll;
    public string Feedback = "";
    public bool FeedbackIsError;
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
    public RecruitModel Recruit = new RecruitModel();
    public WagerModel Wager = new WagerModel();
    public PlanningModel Planning = new PlanningModel();
    public MapModel Map = new MapModel();
    public ShopModel Shop = new ShopModel();
    public DeployModel Deploy = new DeployModel();
    public RunOverModel RunOver = new RunOverModel();
    public ResultGateModel Result = new ResultGateModel();
}
