using System;
using System.Collections.Generic;
using Warband.Content;
using Warband.Sim;

/// <summary>
/// Deterministic, presentation-only Workbench states. They never enter RunController and never
/// save; editor QA binds them directly to the retained views so every viewport sees identical
/// worst-case copy, inventory, roster, and commerce states.
/// </summary>
public static class WorkbenchFixtures
{
    internal readonly struct Fixture
    {
        public readonly string Id;
        public readonly RunShellModel Shell;
        public readonly bool ShowKeywordTooltip;
        public readonly bool ShowEquipmentTooltip;
        public readonly bool ShowRankTierTooltip;

        public Fixture(string id, RunShellModel shell,
                       bool showKeywordTooltip = false,
                       bool showEquipmentTooltip = false,
                       bool showRankTierTooltip = false)
        {
            Id = id;
            Shell = shell;
            ShowKeywordTooltip = showKeywordTooltip;
            ShowEquipmentTooltip = showEquipmentTooltip;
            ShowRankTierTooltip = showRankTierTooltip;
        }
    }

    public static readonly string[] Ids =
    {
        "muster-state",
        "muster-phalanx",
        "rankup-modal",
        "beyond-the-hour",
        "market-recruit",
        "market-rankup-b",
        "market-rankup-long",
        "market-rankup-s",
        "market-weapon",
        "market-inscription",
        "armory-empty",
        "armory-full",
        "rail-full",
        "rail-open",
        "tooltip-keyword",
        "tooltip-equipment",
        "tooltip-weapon-fact",
        "tooltip-weapon-property",
        "tooltip-unit-spec",
        "tooltip-rank-tier",
    };

    internal static Fixture Build(
        string requestedId, bool expandedText = false, bool reducedMotion = false)
    {
        string id = Normalize(requestedId);
        var shell = BaseShell(expandedText, reducedMotion);
        switch (id)
        {
            case "beyond-the-hour":
                ApplyBeyondTheHour(shell);
                break;
            case "market-rankup-b":
                SelectRankUp(shell, RankUpInspector(expandedText, 1));
                break;
            case "market-rankup-long":
                SelectRankUp(shell, PhalanxRankUpInspector(expandedText));
                break;
            case "market-rankup-s":
                SelectRankUp(shell, RankUpInspector(expandedText, 3));
                break;
            case "tooltip-rank-tier":
                SelectRankUp(shell, RankUpInspector(expandedText, 3));
                break;
            case "tooltip-weapon-fact":
            case "tooltip-weapon-property":
            case "tooltip-unit-spec":
                shell.Planning.Inspector = HeroInspector(expandedText);
                break;
            case "market-weapon":
                Select(shell, "fixture:weapon", WeaponInspector(expandedText));
                break;
            case "market-inscription":
                Select(shell, "fixture:inscription", InscriptionInspector(expandedText));
                break;
            case "armory-empty":
                shell.Planning.PartyShelf.Expanded = true;
                shell.WarbandBar.ArmoryDrawerOpen = true;
                shell.WarbandBar.StoredItems = 0;
                shell.Planning.PartyShelf.LoadoutInventory.Clear();
                shell.Planning.PartyShelf.LoadoutInspector =
                    CompactLoadout(HeroInspector(expandedText));
                break;
            case "armory-full":
                shell.Planning.PartyShelf.Expanded = true;
                shell.WarbandBar.ArmoryDrawerOpen = true;
                shell.Planning.PartyShelf.LoadoutInventory = Inventory(expandedText);
                shell.Planning.PartyShelf.LoadoutInspector = ProjectedHeroInspector(expandedText);
                shell.WarbandBar.ArmedInventoryItemInstanceId = 1000;
                shell.WarbandBar.ArmedInventoryKind = 0;
                foreach (WarbandHeroModel hero in shell.WarbandBar.Field)
                    hero.Weapon.ValidTarget = true;
                foreach (WarbandHeroModel hero in shell.WarbandBar.Reserve)
                    hero.Weapon.ValidTarget = true;
                break;
            case "rail-full":
                shell.Planning.Inspector = HeroInspector(expandedText);
                break;
            case "rail-open":
                shell.Planning.Inspector = HeroInspector(expandedText);
                shell.WarbandBar = OpenWarbandBar();
                break;
            case "muster-state":
                ApplyMusterState(shell, expandedText);
                break;
            case "muster-phalanx":
                ApplyMusterState(shell, expandedText);
                ApplyPhalanxMusterReview(shell, expandedText);
                break;
            case "rankup-modal":
                shell.Planning.SpecChoice = RankUpModalChoice(expandedText);
                shell.WarbandBar.Dimmed = true;
                break;
            case "tooltip-keyword":
                shell.Planning.Inspector = KeywordInspector(expandedText);
                break;
            case "tooltip-equipment":
                shell.Planning.Inspector = HeroInspector(expandedText);
                break;
            default:
                id = "market-recruit";
                Select(shell, "fixture:recruit", RecruitInspector(expandedText));
                break;
        }
        // The running Workbench projects owned champions through UnitSheetModel. Keep the
        // presentation-only fixtures on that same renderer so the matrix cannot pass or fail
        // against the retired one-column champion anatomy. Muster and rank-up deliberately keep
        // their bespoke approved layouts.
        if (!shell.Planning.MusterMode &&
            shell.Planning.Inspector != null &&
            shell.Planning.Inspector.Kind == DecisionDetailKind.Champion &&
            shell.Planning.Inspector.RankUpDetail == null &&
            shell.Planning.Inspector.UnitSheet == null)
            shell.Planning.Inspector.UnitSheet =
                FixtureUnitSheet(shell.Planning.Inspector);
        MarketOfferPresentationContract.Validate(shell.Planning.MarketOffers);
        return new Fixture(
            id, shell,
            id == "tooltip-keyword",
            id == "tooltip-equipment",
            id == "tooltip-rank-tier" || id == "tooltip-unit-spec");
    }

    private static UnitSheetModel FixtureUnitSheet(InspectorModel inspector)
    {
        var sheet = new UnitSheetModel
        {
            Specs = new List<RankTierSlotModel>(inspector.PathTiers),
        };
        foreach (StatChipModel fact in inspector.Stats)
        {
            if (fact.Id == PresentationFactId.Hp)
                sheet.CoreFacts.Add(fact);
            else
                sheet.WeaponFacts.Add(fact);
        }
        if (inspector.WeaponProperty != null)
            sheet.WeaponProperties.Add(inspector.WeaponProperty);

        foreach (InspectorSectionModel section in inspector.Sections)
        {
            if (section == null) continue;
            if (string.Equals(section.Label, "SIGNATURE", StringComparison.OrdinalIgnoreCase))
            {
                sheet.Signature = section;
                continue;
            }
            if (string.Equals(section.Label, "WEAPON", StringComparison.OrdinalIgnoreCase))
            {
                sheet.WeaponName = section.Name;
                if (sheet.WeaponProperties.Count == 0)
                    sheet.WeaponProperties.Add(new RuleDeltaModel
                    {
                        RuleName = section.Name,
                        DisplayName = section.Name,
                        ShortSummary = section.Summary,
                        FullDescription = section.Summary,
                        Applies = true,
                    });
                continue;
            }
            sheet.Passives.Add(section);
        }
        if (string.IsNullOrWhiteSpace(sheet.WeaponName))
            sheet.WeaponName = string.IsNullOrWhiteSpace(inspector.WeaponName)
                ? "BASIC ATTACK"
                : inspector.WeaponName;
        return sheet;
    }

    private static RunShellModel BaseShell(bool expanded, bool reducedMotion)
    {
        var shell = new RunShellModel
        {
            Screen = RunScreen.Management,
            Planning = new PlanningModel
            {
                Act = "ACT II · THE GILDED BREACH",
                Beat = "HOUR 6 OF 9",
                // The header node map is contract-checked (workbench-refactor): fixtures
                // carry a representative act track like the live model does.
                Track = new List<PlanningTrackNodeModel>
                {
                    new PlanningTrackNodeModel { Label = "1", Kind = "Fight", State = "past" },
                    new PlanningTrackNodeModel { Label = "2", Kind = "Interlude", State = "past" },
                    new PlanningTrackNodeModel { Label = "3", Kind = "Fight", State = "past" },
                    new PlanningTrackNodeModel { Label = "4", Kind = "Fight", State = "current" },
                    new PlanningTrackNodeModel { Label = "5", Kind = "Fight", State = "future" },
                    new PlanningTrackNodeModel { Label = "BOSS", Kind = "Boss", State = "future" },
                },
                Brief = Expand(
                    "Prepare your warband. Changes remain reversible until you commit " +
                    "the Hourstone.", expanded),
                Sand = "31",
                CurrencyBalance = 31,
                RerollCost = 3,
                RerollLabel = "REROLL",
                CanReroll = true,
                CanCommit = true,
                CommitLabel = "TO THE BREACH",
                ReducedMotion = reducedMotion,
                PartyShelf = PartyShelf(),
            },
            WarbandBar = FullWarbandBar(),
        };
        shell.Planning.MarketOffers = MarketOffers(expanded);
        shell.Planning.Inspector = RecruitInspector(expanded);
        shell.Planning.PartyShelf.LoadoutInventory = Inventory(expanded);
        shell.Planning.PartyShelf.LoadoutInspector = HeroInspector(expanded);
        return shell;
    }

    private static void ApplyBeyondTheHour(RunShellModel shell)
    {
        PlanningModel planning = shell.Planning;
        planning.Act = "VICTORY BANKED";
        planning.Beat = "THE WANING CROWN HAS FALLEN";
        planning.BeatKind = PlanningBeat.EndlessChoice;
        planning.Heading = "THE HOUR HELD";
        planning.Brief =
            "The authored run is won. Leave with the victory, or carry this exact warband " +
            "into escalating cycles until a deeper Hour finally breaks it.";
        planning.Rule = "Victory is already preserved.";
        planning.CanReroll = false;
        planning.CanCommit = false;
        planning.CommitLabel = "";
        planning.Track = new List<PlanningTrackNodeModel>
        {
            new PlanningTrackNodeModel { Label = "1", Kind = "Fight", State = "past" },
            new PlanningTrackNodeModel { Label = "2", Kind = "Interlude", State = "past" },
            new PlanningTrackNodeModel { Label = "3", Kind = "Fight", State = "past" },
            new PlanningTrackNodeModel { Label = "4", Kind = "Fight", State = "past" },
            new PlanningTrackNodeModel { Label = "BOSS", Kind = "Boss", State = "past" },
        };
        planning.Interlude = new List<InterludeChoiceModel>
        {
            new InterludeChoiceModel
            {
                Path = -1,
                Option = 0,
                ActionLabel = "RETIRE WITH VICTORY",
                Facts = new List<string> { "3 ACTS CLEARED", "VICTORY PRESERVED" },
                Card = new CardModel
                {
                    Eyebrow = "SEAL THE HOUR",
                    Title = "Retire with victory",
                    AbilitySummary =
                        "End the expedition here. The completed run and final warband become the record.",
                    Accent = "sand",
                },
            },
            new InterludeChoiceModel
            {
                Path = -1,
                Option = 1,
                ActionLabel = "CONTINUE BEYOND THE HOUR",
                Facts = new List<string> { "CYCLE 1", "ACT 3 POOL", "CROWN +25%" },
                Card = new CardModel
                {
                    Eyebrow = "BEYOND THE HOUR",
                    Title = "Continue with this warband",
                    AbilitySummary =
                        "Enter three escalating fights and face the Waning Crown again. " +
                        "Defeat cannot erase the victory already earned.",
                    Accent = "tempo",
                },
            },
        };
    }

    private static void Select(RunShellModel shell, string key, InspectorModel inspector)
    {
        shell.Planning.Inspector = inspector;
        for (int i = 0; i < shell.Planning.MarketOffers.Count; i++)
            shell.Planning.MarketOffers[i].Selected =
                shell.Planning.MarketOffers[i].Key == key;
    }

    private static void SelectRankUp(RunShellModel shell, InspectorModel inspector)
    {
        Select(shell, "fixture:rankup", inspector);
        for (int i = 0; i < shell.Planning.MarketOffers.Count; i++)
        {
            MarketOfferCardModel offer = shell.Planning.MarketOffers[i];
            if (offer.Key != "fixture:rankup") continue;
            offer.Title = inspector.Title;
            offer.ArtworkResource = inspector.PortraitResource;
            offer.ArtworkFallback = inspector.PortraitFallback;
            offer.Accent = inspector.Accent;
            offer.CurrencyCost = inspector.CurrencyCost;
            offer.Metrics = new List<StatChipModel>(inspector.Stats);
            if (offer.Detail != null) offer.Detail.Title = inspector.Title;
            break;
        }
    }

    private static List<MarketOfferCardModel> MarketOffers(bool expanded)
    {
        return new List<MarketOfferCardModel>
        {
            Offer("recruit", MarketOfferKind.Recruit, "RECRUIT", "Banneret",
                "COMMAND AURA", Expand(
                    "At battle start, adjacent allies gain 18 Protection for 4 seconds.",
                    expanded), 12, "precision",
                new List<StatChipModel>
                {
                    Fact("HP", "164", PresentationFactId.Hp),
                    Fact("POWER", "14", PresentationFactId.BasicPower),
                    Fact("REACH", "2", PresentationFactId.Reach),
                    Fact("CADENCE", "1.1", PresentationFactId.Cadence),
                }),
            Offer("rankup", MarketOfferKind.RankUp, "RANK UP", "Cleric · Rank A",
                "AWAKENING", Expand(
                    "Gain the Rank A stat package, then choose one of two permanent " +
                    "specializations before leaving the Workbench.", expanded), 16, "mending",
                new List<StatChipModel>
                {
                    Fact("RANK", "B → A", PresentationFactId.Rank),
                    Fact("HP", "+24", PresentationFactId.Hp),
                    Fact("POWER", "+4", PresentationFactId.BasicPower),
                    Fact("CHOICE", "1 OF 2", PresentationFactId.ChoiceCount),
                }),
            Offer("weapon", MarketOfferKind.Weapon, "WEAPON", "Sunforged Glaive",
                "WEAPON", Expand(
                    "Deal 19 damage at Reach 2. Each swing grants 2 Mana to its wielder.",
                    expanded), 9, "power",
                new List<StatChipModel>
                {
                    Fact("POWER", "19", PresentationFactId.BasicPower),
                    Fact("REACH", "2", PresentationFactId.Reach),
                    Fact("CADENCE", "1.0", PresentationFactId.Cadence),
                    Fact("MANA/HIT", "2", PresentationFactId.ManaPerSwing),
                }),
            Offer("trinket", MarketOfferKind.Trinket, "TRINKET", "Oathkeeper Seal",
                "PASSIVE", Expand(
                    "The first time this unit falls below half Health, gain 30 Protection.",
                    expanded), 7, "ward",
                new List<StatChipModel>
                {
                    Fact("HP", "+18", PresentationFactId.Hp),
                    Fact("WARD", "30", PresentationFactId.Protection),
                    Fact("TRIGGER", "50%", PresentationFactId.Unknown),
                }),
            Offer("inscription", MarketOfferKind.Inscription, "INSCRIPTION",
                "Edict of Long Reach", "WARBAND RULE", Expand(
                    "All allied ranged attacks gain +1 Reach for the remainder of this run.",
                    expanded), 11, "utility",
                new List<StatChipModel>
                {
                    Fact("SCOPE", "WARBAND", PresentationFactId.Scope),
                    Fact("DURATION", "THIS RUN", PresentationFactId.Duration),
                    Fact("REACH", "+1", PresentationFactId.Reach),
                }),
        };
    }

    private static MarketOfferCardModel Offer(
        string id, MarketOfferKind kind, string classification, string title,
        string ruleName, string exactRule, int cost, string accent,
        List<StatChipModel> metrics)
    {
        string key = "fixture:" + id;
        return new MarketOfferCardModel
        {
            Key = key,
            ContentId = key,
            Kind = kind,
            Classification = classification,
            Title = title,
            Subtitle = "LIVE MARKET",
            ArtworkResource = kind == MarketOfferKind.Recruit ||
                              kind == MarketOfferKind.RankUp
                ? "UI/Portraits/" +
                  (kind == MarketOfferKind.Recruit ? "banneret" : "cleric")
                : "",
            ArtworkFallback = kind == MarketOfferKind.Recruit ? "BN" : "◇",
            Accent = accent,
            RuleLabel = kind == MarketOfferKind.Inscription ? "WARBAND RULE" : "EXACT RULE",
            RuleName = ruleName,
            ExactRule = exactRule,
            CurrencyCost = cost,
            CurrencyBalance = 31,
            Affordable = true,
            Selected = id == "recruit",
            Metrics = metrics,
            Detail = new CardModel
            {
                Key = key,
                Title = title,
                AbilityName = ruleName,
                AbilitySummary = exactRule,
                Accent = accent,
            },
        };
    }

    private static InspectorModel RecruitInspector(bool expanded)
    {
        var inspector = new InspectorModel
        {
            Key = "fixture:recruit",
            Kind = DecisionDetailKind.Recruit,
            // Rank drives the badge's escalation (r4-rank-ladder). Fixtures carry it so the QA
            // matrix guards the treatment instead of it only existing in a live run.
            Rank = "C",
            PathTiers = EmptyPathTiers(),
            Eyebrow = "LIVE MARKET · RECRUIT",
            Title = "Banneret",
            Subtitle = "Captain · Rank C · joins the field",
            PortraitResource = "UI/Portraits/banneret",
            PortraitFallback = "BN",
            Accent = "precision",
            CurrencyCost = 12,
            CurrencyBalance = 31,
            Stats = CombatStats("164", "14", "2", "1.1"),
            WeaponProperty = WeaponProperty(
                "COMPANY MUSTER", "MUSTER",
                "Start: adjacent allies +10% Speed",
                "At combat start, adjacent allies gain 10% Attack Speed."),
            KeywordNotes = new List<string>
            {
                "PROTECTION · Absorbs incoming damage before Health.",
                "COMMAND · A rule that strengthens nearby allies.",
            },
            Sections = new List<InspectorSectionModel>
            {
                Rule("SIGNATURE", "HOLD THE LINE",
                    Expand("Grant 24 Protection to the two lowest-Health allies for 5 seconds.",
                        expanded), UiGlyphId.Mana, "70"),
                Rule("WEAPON", "COMPANY STANDARD",
                    Expand("Deal 14 damage to the nearest enemy at Reach 2.", expanded)),
                Rule("PASSIVE", "COMMAND AURA",
                    Expand("At battle start, adjacent allies gain 18 Protection for 4 seconds.",
                        expanded), role: InspectorSectionRole.Deferred),
            },
            Actions = BuyActions("RECRUIT", 12),
        };
        return inspector;
    }

    private static InspectorModel RankUpInspector(bool expanded, int nextTier)
    {
        string current = nextTier == 1 ? "C" : "A";
        string next = nextTier == 1 ? "B" : "S";
        var traits = new List<WarbandSpecBadgeModel>();
        if (nextTier >= 2)
            traits.Add(new WarbandSpecBadgeModel
            {
                Rank = "B",
                Icon = "✦",
                Name = "Sanctified Pyre",
                Rule = "After restoring Health, scorch the nearest enemy for 8 damage.",
                Accent = "mending",
            });
        if (nextTier >= 3)
            traits.Add(new WarbandSpecBadgeModel
            {
                Rank = "A",
                Icon = "⬡",
                Name = "Last Light",
                Rule = "The first allied defeat each battle is prevented; that ally returns " +
                       "with 28 Health.",
                Accent = "ward",
            });

        var options = RankUpOptions(expanded, nextTier);
        var model = new InspectorModel
        {
            Key = "fixture:rankup",
            Kind = DecisionDetailKind.RankUp,
            Rank = "S",
            Eyebrow = "LIVE MARKET · RANK UP",
            Title = $"Cleric · Rank {next}",
            Subtitle = "Guaranteed growth · specialization follows purchase",
            PortraitResource = "UI/Portraits/cleric",
            PortraitFallback = "CL",
            Accent = "mending",
            CurrencyCost = 16,
            CurrencyBalance = 31,
            Stats = new List<StatChipModel>
            {
                Fact("RANK", $"{current} → {next}", PresentationFactId.Rank),
                Fact("HP", "+24", PresentationFactId.Hp),
                Fact("POWER", "+4", PresentationFactId.BasicPower),
                Fact("CHOICE", "1 OF 2", PresentationFactId.ChoiceCount),
            },
            Traits = traits,
            Comparisons = new List<StatComparisonModel>
            {
                Compare("HP", "164", "188", DeltaDirection.Positive),
                Compare("POWER", "14", "18", DeltaDirection.Positive),
            },
            RankUpDetail = new RankUpDetailModel
            {
                CurrentRank = current,
                NextRank = next,
                Tiers = RankTierFixtures(traits, next),
                Options = options,
            },
            Actions = BuyActions("RANK UP", 16),
        };
        return model;
    }

    private static List<ChoicePreviewModel> RankUpOptions(bool expanded, int nextTier)
    {
        if (nextTier == 1)
            return new List<ChoicePreviewModel>
            {
                Choice("DEEPEN · BRUISER", "Warpriest",
                    "Sanctified Pyre reaches farther and applies Burn to enemies.", "power",
                    expanded),
                Choice("SWAP · BACKLINE", "Lifebinder",
                    "Sanctified Pyre becomes a long-range restore for the lowest-Health ally.",
                    "mending", expanded),
            };
        return new List<ChoicePreviewModel>
        {
            Choice("CROWN · PROTECTION", "Great Chorus",
                "Sanctuary resolves twice; the second pulse finds the new lowest-Health ally.",
                "ward", expanded),
            Choice("CROWN · RECOVERY", "Sanctuary",
                "Allies restored by the signature leave consecrated ground beneath them.",
                "mending", expanded),
        };
    }

    /// <summary>
    /// The fork rank-up exactly as the live shop builds it — real catalog nodes, real
    /// generated prose. Pikewall's machine rule runs 170+ characters without a sentence
    /// break, which is what forced the authored-summary preview tier; this fixture is its
    /// regression stage.
    /// </summary>
    private static InspectorModel PhalanxRankUpInspector(bool expanded)
    {
        return new InspectorModel
        {
            Key = "fixture:rankup",
            Kind = DecisionDetailKind.RankUp,
            Rank = "B",
            Eyebrow = "LIVE MARKET · RANK UP",
            Title = "Phalanx · Rank B",
            Subtitle = "Guaranteed growth · specialization follows purchase",
            PortraitResource = "UI/Portraits/phalanx",
            PortraitFallback = "PH",
            Accent = "reaction",
            CurrencyCost = 5,
            CurrencyBalance = 31,
            Stats = new List<StatChipModel>
            {
                Fact("RANK", "C → B", PresentationFactId.Rank),
                Fact("HP", "+30", PresentationFactId.Hp),
                Fact("POWER", "+2", PresentationFactId.BasicPower),
                Fact("CHOICE", "1 OF 2", PresentationFactId.ChoiceCount),
            },
            Comparisons = new List<StatComparisonModel>
            {
                Compare("HP", "150", "180", DeltaDirection.Positive),
                Compare("POWER", "9", "11", DeltaDirection.Positive),
            },
            RankUpDetail = new RankUpDetailModel
            {
                CurrentRank = "C",
                NextRank = "B",
                Tiers = RankTierFixtures(new List<WarbandSpecBadgeModel>(), "B"),
                Options = new List<ChoicePreviewModel>
                {
                    RealNodeChoice("phalanx.pikewall", "reaction", expanded),
                    RealNodeChoice("phalanx.lancer", "power", expanded),
                },
            },
            Actions = BuyActions("RANK UP", 5),
        };
    }

    /// <summary>One option exactly as RunShell.RankChoicePreview builds it: the authored
    /// lexicon one-liner at the glance tier, the full generated prose on hover.</summary>
    private static ChoicePreviewModel RealNodeChoice(
        string nodeId, string accent, bool expanded)
    {
        string chassisId = nodeId.Substring(0, nodeId.IndexOf('.'));
        UnitDef before = Loadout.Compose(Kits.Chassis[chassisId]).Def;
        UnitDef after = Loadout.Compose(
            Kits.Chassis[chassisId],
            nodes: new[] { Kits.Nodes[nodeId] }).Def;
        SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
            chassisId, nodeId, before, after);
        return new ChoicePreviewModel
        {
            Change = rule.Change.ToString().ToUpperInvariant(),
            Name = rule.Name,
            Summary = rule.Choice,
            Rule = Expand(rule.Full, expanded),
            Accent = accent,
        };
    }

    private static ChoicePreviewModel Choice(
        string change, string name, string rule, string accent, bool expanded) =>
        new ChoicePreviewModel
        {
            Change = change,
            Name = name,
            Rule = Expand(rule, expanded),
            Accent = accent,
        };

    private static List<RankTierSlotModel> RankTierFixtures(
        IReadOnlyList<WarbandSpecBadgeModel> selected, string pendingRank)
    {
        var result = new List<RankTierSlotModel>();
        foreach (string rank in new[] { "B", "A", "S" })
        {
            WarbandSpecBadgeModel chosen = null;
            for (int i = 0; i < selected.Count; i++)
                if (selected[i].Rank == rank) chosen = selected[i];
            bool pending = rank == pendingRank;
            result.Add(new RankTierSlotModel
            {
                Rank = rank,
                State = chosen != null
                    ? RankTierSlotState.Selected
                    : pending
                        ? RankTierSlotState.Pending
                        : RankTierSlotState.Locked,
                Icon = chosen?.Icon ?? (pending ? "▲" : "◇"),
                Name = chosen?.Name ?? (pending ? "CHOOSE 1 OF 2" : "LOCKED"),
                Rule = chosen?.Rule ?? (pending
                    ? $"Purchase, then choose the Rank {rank} specialization."
                    : $"Reach Rank {rank} to unlock this specialization tier."),
                Accent = chosen?.Accent ?? "",
            });
        }
        return result;
    }

    private static InspectorModel WeaponInspector(bool expanded)
    {
        return new InspectorModel
        {
            Key = "fixture:weapon",
            Kind = DecisionDetailKind.Weapon,
            Eyebrow = "LIVE MARKET · WEAPON",
            Title = "Sunforged Glaive",
            Subtitle = "Refined polearm · compatible with any champion",
            PortraitFallback = "⚔",
            Accent = "power",
            CurrencyCost = 9,
            CurrencyBalance = 31,
            Stats = CombatStats("—", "19", "2", "1.0"),
            Sections = new List<InspectorSectionModel>
            {
                Rule("WEAPON PROFILE", "SOLAR EDGE",
                    Expand("Deal 19 damage at Reach 2. Each swing grants 2 Mana.", expanded)),
                Comparison("ON SELECTED UNIT",
                    Compare("POWER", "14", "19", DeltaDirection.Positive),
                    Compare("REACH", "3", "2", DeltaDirection.Contextual),
                    Compare("CADENCE", "0.9", "1.0", DeltaDirection.Contextual)),
                Rule("POLEARM MASTERY", "SUNWARD RIDER",
                    Expand("Mastered or Relic: the third swing in a row burns for 6.", expanded),
                    role: InspectorSectionRole.Deferred),
            },
            Actions = BuyActions("BUY TO ARMORY", 9),
        };
    }

    private static InspectorModel InscriptionInspector(bool expanded)
    {
        return new InspectorModel
        {
            Key = "fixture:inscription",
            Kind = DecisionDetailKind.Inscription,
            Eyebrow = "LIVE MARKET · INSCRIPTION",
            Title = "Edict of Long Reach",
            Subtitle = "Warband law · bound for the remainder of this run",
            PortraitFallback = "◇",
            Accent = "utility",
            CurrencyCost = 11,
            CurrencyBalance = 31,
            Stats = new List<StatChipModel>
            {
                Fact("SCOPE", "WARBAND", PresentationFactId.Scope),
                Fact("DURATION", "THIS RUN", PresentationFactId.Duration),
                Fact("REACH", "+1", PresentationFactId.Reach),
            },
            KeywordNotes = new List<string>
            {
                "REACH · Maximum hex distance at which an attack may select its target.",
            },
            Sections = new List<InspectorSectionModel>
            {
                Rule("WARBAND RULE", "EDICT OF LONG REACH",
                    Expand("All allied ranged attacks gain +1 Reach for the rest of this run.",
                        expanded)),
                Rule("SCOPE", "PERSISTENT LAW",
                    Expand("Applies to current and future recruits. It does not occupy an item slot.",
                        expanded)),
            },
            Actions = BuyActions("BIND INSCRIPTION", 11),
        };
    }

    private static InspectorModel HeroInspector(bool expanded)
    {
        return new InspectorModel
        {
            Key = "fixture:hero",
            Kind = DecisionDetailKind.Champion,
            Rank = "A",
            Eyebrow = "FIELD I · RANK A",
            Title = "Banneret",
            Subtitle = "Captain · composed unit dossier",
            PortraitResource = "UI/Portraits/banneret",
            PortraitFallback = "BN",
            Accent = "precision",
            Stats = CombatStats("188", "17", "2", "1.1"),
            WeaponProperty = WeaponProperty(
                "COMPANY MUSTER", "MUSTER",
                "Start: adjacent allies +10% Speed",
                "At combat start, adjacent allies gain 10% Attack Speed."),
            PathTiers = new List<RankTierSlotModel>
            {
                new RankTierSlotModel
                {
                    Rank = "B",
                    State = RankTierSlotState.Selected,
                    Icon = "✦",
                    Name = "Rallying Standard",
                    Rule = "At battle start, adjacent allies gain 12 Protection for 4 seconds.",
                    Accent = "ward",
                },
                new RankTierSlotModel
                {
                    Rank = "A",
                    State = RankTierSlotState.Selected,
                    Icon = "ϟ",
                    Name = "Last Command",
                    Rule = "When this champion casts, adjacent allies gain Haste for 3 seconds.",
                    Accent = "tempo",
                },
                new RankTierSlotModel
                {
                    Rank = "S",
                    State = RankTierSlotState.Locked,
                    Icon = "◇",
                    Name = "AWAKENS AT RANK S",
                    Rule = "A 1-of-2 specialization is offered at Rank S.",
                },
            },
            KeywordNotes = new List<string>
            {
                "PROTECTION · Absorbs incoming damage before Health.",
                "HASTE · Increases attack cadence.",
            },
            Traits = new List<WarbandSpecBadgeModel>
            {
                new WarbandSpecBadgeModel
                {
                    Rank = "B",
                    Icon = "✦",
                    Name = "Rallying Standard",
                    Rule = "At battle start, adjacent allies gain 12 Protection for 4 seconds.",
                    Accent = "ward",
                },
                new WarbandSpecBadgeModel
                {
                    Rank = "A",
                    Icon = "ϟ",
                    Name = "Last Command",
                    Rule = "When this champion casts, adjacent allies gain Haste for 3 seconds.",
                    Accent = "tempo",
                },
            },
            Sections = new List<InspectorSectionModel>
            {
                Rule("SIGNATURE", "HOLD THE LINE",
                    Expand("Grant 24 Protection and Haste to the two lowest-Health allies.",
                        expanded), UiGlyphId.Mana, "70"),
                Rule("WEAPON", "COMPANY STANDARD",
                    Expand("Deal 17 damage to the nearest enemy at Reach 2.", expanded)),
                Rule("PASSIVE", "COMMAND AURA",
                    Expand("Adjacent allies begin battle with 18 Protection for 4 seconds.",
                        expanded), role: InspectorSectionRole.Deferred),
            },
            Actions = new List<InspectorActionModel>
            {
                new InspectorActionModel
                    { Id = HallActionId.Move, Label = "MOVE TO RESERVE", Enabled = true },
            },
        };
    }

    private static InspectorModel KeywordInspector(bool expanded)
    {
        UnitDef def = Loadout.Compose(Kits.Chassis["phalanx"]).Def;
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
        WeaponDef weapon = Weapons.All["pike"];
        string mastery = MechanicalRulePresenter.WeaponMastery(weapon).Full;
        return new InspectorModel
        {
            Key = "fixture:keyword",
            Kind = DecisionDetailKind.Champion,
            Eyebrow = "FIELD VIII · RANK C",
            Title = "Phalanx",
            Subtitle = "Reaction fighter · semantic-rule fixture",
            PortraitResource = "UI/Portraits/phalanx",
            PortraitFallback = "PH",
            Accent = "reaction",
            Stats = CombatStats("150", "14", "2", "1.0"),
            WeaponProperty = WeaponProperty(
                "SET THE PIKE", weapon.Name.ToUpperInvariant(), mastery, mastery),
            KeywordNotes = new List<string>(PlayerRuleProjection.Keywords(def)),
            Sections = new List<InspectorSectionModel>
            {
                Rule("SIGNATURE", rules.SignatureName,
                    Expand(rules.SignatureText, expanded), UiGlyphId.Mana,
                    def.ManaMax.ToString()),
                // The semantic-keyword capture targets "Gain 1 Riposte" inside a rendered rule
                // sentence, so this fixture keeps its passive PRIMARY on purpose — it stands in
                // for any kind whose passive carries the keyword under test.
                Rule("PASSIVE", rules.PassiveName,
                    Expand(rules.PassiveText, expanded)),
                Rule("WEAPON", weapon.Name.ToUpperInvariant(),
                    Expand(mastery, expanded)),
            },
        };
    }

    /// <summary>Mirrors the live drawer-open rule: rules defer, stat chips yield.</summary>
    private static InspectorModel CompactLoadout(InspectorModel model)
    {
        foreach (InspectorSectionModel section in model.Sections)
            if (section.Kind == InspectorSectionKind.Rule)
                section.Role = InspectorSectionRole.Deferred;
        model.Stats.Clear();
        return model;
    }

    private static InspectorModel ProjectedHeroInspector(bool expanded)
    {
        InspectorModel model = CompactLoadout(HeroInspector(expanded));
        model.Eyebrow = "ARMORY · PROJECTED UNIT";
        model.Subtitle = "Sunforged Glaive pinned · choose a compatible weapon socket";
        model.EquipmentPreview = new EquipmentPreviewModel
        {
            OfferedItemName = "Sunforged Glaive",
            SelectedRecipientHeroKey = "hero:1",
            CurrentItemName = "Ashwood Spear",
            Recipients = new List<RecipientPreviewModel>
            {
                new RecipientPreviewModel
                {
                    HeroKey = "hero:1", DisplayName = "Banneret", RankText = "RANK A",
                    PortraitResource = "UI/Portraits/banneret", PortraitFallback = "BN",
                    CurrentItemName = "Ashwood Spear", IsSelected = true,
                },
                new RecipientPreviewModel
                {
                    HeroKey = "hero:2", DisplayName = "Bulwark", RankText = "RANK B",
                    PortraitResource = "UI/Portraits/bulwark", PortraitFallback = "BW",
                    CurrentItemName = "Iron Mace",
                },
                new RecipientPreviewModel
                {
                    HeroKey = "hero:3", DisplayName = "Cleric", RankText = "RANK B",
                    PortraitResource = "UI/Portraits/cleric", PortraitFallback = "CL",
                    CurrentItemName = "Pilgrim Staff",
                },
            },
            StatDeltas = new List<StatComparisonModel>
            {
                Compare("POWER", "14", "19", DeltaDirection.Positive),
                Compare("REACH", "3", "2", DeltaDirection.Contextual),
                Compare("MANA / HIT", "1", "2", DeltaDirection.Positive),
            },
            LostRule = new RuleDeltaModel
            {
                RuleName = "PATIENT THRUST", ShortSummary = "Every third hit gains +1 Reach.",
                Applies = false,
            },
            GainedRule = new RuleDeltaModel
            {
                RuleName = "SOLAR EDGE", ShortSummary = "Each swing grants 2 Mana.",
                Applies = true,
            },
        };
        model.Actions = new List<InspectorActionModel>
        {
            new InspectorActionModel
                { Id = HallActionId.Equip, Label = "EQUIP TO SELECTED UNIT", Primary = true },
        };
        return model;
    }

    private static PartyShelfModel PartyShelf()
    {
        return new PartyShelfModel
        {
            FieldCapacity = 6,
            FieldCount = 6,
            MaxFieldCapacity = 6,
            ReserveCount = 2,
            ReserveCapacity = 2,
            FocusedHeroKey = "hero:1",
            StoredItems = new List<StoredItemSummaryModel>
            {
                new StoredItemSummaryModel { Key = "item:1", Name = "Sunforged Glaive",
                    Kind = "WEAPON", Icon = "⚔", Accent = "power" },
            },
        };
    }

    private static List<CardModel> Inventory(bool expanded)
    {
        string[] names =
        {
            "Sunforged Glaive", "Oathkeeper Seal", "Frostbite Bow",
            "Saint's Reliquary", "Emberknife", "Mirror Buckle",
            "Gravetide Maul", "Windrunner Knot",
        };
        var items = new List<CardModel>();
        for (int i = 0; i < names.Length; i++)
        {
            bool weapon = i % 2 == 0;
            items.Add(new CardModel
            {
                Key = "fixture:item:" + i,
                ItemInstanceId = 1000 + i,
                EquipmentKind = weapon ? 0 : 1,
                Eyebrow = weapon ? "WEAPON · REFINED" : "TRINKET · BOUND",
                Title = names[i],
                RoleIcon = weapon ? "⚔" : "◇",
                PortraitFallback = weapon ? "⚔" : "◇",
                Accent = weapon ? "power" : "ward",
                InspectorAbilitySummary = Expand(
                    weapon
                        ? "Deal 19 damage; each swing grants 2 Mana."
                        : "Gain 30 Protection the first time Health falls below half.",
                    expanded),
                Stats = weapon
                    ? new List<StatChipModel>
                    {
                        Fact("POWER", (15 + i).ToString(), PresentationFactId.BasicPower),
                        Fact("REACH", "2", PresentationFactId.Reach),
                    }
                    : new List<StatChipModel>
                    {
                        Fact("HP", "+" + (14 + i), PresentationFactId.Hp),
                        Fact("WARD", "30", PresentationFactId.Protection),
                    },
            });
        }
        items[0].Selected = true;
        items[0].Pinned = true;
        return items;
    }

    /// <summary>Muster state (workbench-frame): mirrors the approved 01-muster-state sample —
    /// two candidates picked, Bulwark inspected, the third seat awaiting.</summary>
    private static void ApplyMusterState(RunShellModel shell, bool expanded)
    {
        PlanningModel p = shell.Planning;
        p.Title = "MUSTER YOUR WARBAND";
        p.MusterMode = true;
        p.Act = "BEFORE ACT 1";
        p.Beat = "";
        p.Sand = "";
        p.CurrencyBalance = 0;
        p.Brief = Expand(
            "Choose three champions. Hover a stat or rule for exact mechanics.", expanded);
        p.RerollCost = -1;
        p.RerollLabel = "Reroll the muster offer · free";
        p.CanReroll = true;
        p.CanCommit = false;
        p.CommitLabel = "BEGIN RUN · 2 / 3";
        p.Track = new List<PlanningTrackNodeModel>
        {
            new PlanningTrackNodeModel { Label = "1", Kind = "Fight", State = "current" },
            new PlanningTrackNodeModel { Label = "2", Kind = "Fight", State = "future" },
            new PlanningTrackNodeModel { Label = "3", Kind = "Fight", State = "future" },
            new PlanningTrackNodeModel { Label = "4", Kind = "Fight", State = "future" },
            new PlanningTrackNodeModel { Label = "5", Kind = "Fight", State = "future" },
            new PlanningTrackNodeModel { Label = "BOSS", Kind = "Boss", State = "future" },
        };
        p.PartyShelf = new PartyShelfModel();
        p.MarketOffers = new List<MarketOfferCardModel>
        {
            MusterOffer("shade", "Shade", "DIVER · PRECISION", "precision",
                "BACKSTAB", "The current target takes 25 damage.", 0, false, expanded),
            MusterOffer("berserker", "Berserker", "BRUISER · TEMPO", "power",
                "FRENZY", Expand("For the next 4 basic attacks, attack every 0.1s " +
                    "while a target is in reach.", expanded), 1, false, expanded),
            MusterOffer("bulwark", "Bulwark", "TANK · WARD", "ward",
                "SHIELD SLAM", "The nearest enemy takes 10 damage and is Stunned for 1.0s.",
                -1, true, expanded),
            MusterOffer("cleric", "Cleric", "HEALER · MENDER", "mending",
                "SANCTIFIED PYRE", Expand("Enemies within 1 hex take 12 damage. Allies " +
                    "within 1 hex heal 10.", expanded), -1, false, expanded),
            MusterOffer("sharpshot", "Sharpshot", "SNIPER · POWER", "power",
                "PIERCING BOLT", Expand("Enemies in a line through the current target " +
                    "take 14 damage.", expanded), -1, false, expanded),
        };
        p.Inspector = MusterInspector(expanded);
        shell.WarbandBar = MusterBar();
    }

    /// <summary>
    /// Regression fixture for the reported Phalanx dossier: Brace, Skewer and Riposte all wrap
    /// above the fixed Specs row without any rule being clipped by its section.
    /// </summary>
    private static void ApplyPhalanxMusterReview(RunShellModel shell, bool expanded)
    {
        PlanningModel planning = shell.Planning;
        int bulwark = planning.MarketOffers.FindIndex(
            offer => offer.ContentId == "bulwark");
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(
            Loadout.Compose(Kits.Chassis["phalanx"]).Def);
        MarketOfferCardModel offer = MusterOffer(
            "phalanx", "Phalanx", "FRONTLINE · REACTION", "space",
            rules.SignatureName, rules.SignatureText, -1, true, expanded);
        if (bulwark >= 0) planning.MarketOffers[bulwark] = offer;
        else planning.MarketOffers.Add(offer);
        planning.Inspector = MusterInspector(expanded, "phalanx");
    }

    private static MarketOfferCardModel MusterOffer(
        string id, string title, string classification, string accent,
        string ruleName, string exactRule, int musterSlot, bool inspected, bool expanded)
    {
        string key = "muster:" + id;
        return new MarketOfferCardModel
        {
            Key = key,
            ContentId = id,
            Kind = MarketOfferKind.Recruit,
            Classification = classification,
            TierLabel = "RANK C",
            PathTiers = EmptyPathTiers(),
            MusterSlot = musterSlot,
            Title = title,
            Subtitle = "MUSTER",
            ArtworkResource = "UI/Portraits/" + id,
            ArtworkFallback = title.Substring(0, 2).ToUpperInvariant(),
            Accent = accent,
            RuleLabel = "SIGNATURE",
            RuleName = ruleName,
            ExactRule = exactRule,
            Price = "MUSTER",
            CurrencyCost = -1,
            CurrencyBalance = -1,
            Selected = inspected,
            Affordable = true,
            Metrics = new List<StatChipModel>
            {
                Fact("HP", "200", PresentationFactId.Hp),
                Fact("POWER", "5", PresentationFactId.BasicPower),
                Fact("REACH", "1", PresentationFactId.Reach),
            },
            Detail = new CardModel
            {
                Key = key,
                Title = title,
                AbilityName = ruleName,
                AbilitySummary = exactRule,
                Accent = accent,
            },
        };
    }

    private static List<RankTierSlotModel> EmptyPathTiers()
    {
        return new List<RankTierSlotModel>
        {
            new RankTierSlotModel
            {
                Rank = "B", State = RankTierSlotState.Locked, Icon = "◇",
                Name = "AWAKENS AT RANK B · THE FORK",
                Rule = "At Rank B this champion forks: a 1-of-2 choice that changes what it IS.",
            },
            new RankTierSlotModel
            {
                Rank = "A", State = RankTierSlotState.Locked, Icon = "◇",
                Name = "AWAKENS AT RANK A",
                Rule = "A 1-of-2 specialization is offered at Rank A.",
            },
            new RankTierSlotModel
            {
                Rank = "S", State = RankTierSlotState.Locked, Icon = "◇",
                Name = "AWAKENS AT RANK S",
                Rule = "A 1-of-2 specialization is offered at Rank S.",
            },
        };
    }

    private static InspectorModel MusterInspector(
        bool expanded, string chassisId = "bulwark")
    {
        bool phalanx = chassisId == "phalanx";
        UnitDef def = Loadout.Compose(Kits.Chassis[chassisId]).Def;
        ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
        WeaponDef weapon = Weapons.All[phalanx ? "pike" : "towershield"];
        string mastery = MechanicalRulePresenter.WeaponMastery(weapon).Full;
        string weaponRule = phalanx
            ? mastery
            : "Deals 5 damage every 1.4s at reach 1. 10% critical chance.";
        var inspector = new InspectorModel
        {
            Key = "muster:" + chassisId,
            Kind = DecisionDetailKind.Recruit,
            Eyebrow = phalanx
                ? "CANDIDATE · FRONTLINE · REACTION"
                : "CANDIDATE · TANK · WARD",
            Title = phalanx ? "Phalanx" : "Bulwark",
            Subtitle = "Basic attacks damage enemies",
            PortraitResource = "UI/Portraits/" + chassisId,
            PortraitFallback = phalanx ? "PH" : "BU",
            Accent = phalanx ? "space" : "ward",
            AbilityIcon = "✸",
            AbilityTrigger = "SIGNATURE",
            AbilityName = rules.SignatureName,
            AbilitySummary = rules.SignatureText,
            AbilityManaCost = def.ManaMax,
            PassiveIcon = "⬡",
            PassiveTrigger = "PASSIVE",
            PassiveName = rules.PassiveName,
            PassiveSummary = rules.PassiveText,
            WeaponName = weapon.Name,
            WeaponSummary = Expand(weaponRule, expanded),
            Stats = phalanx
                ? CombatStats("150", "9", "2", "1.1", "11")
                : CombatStats("200", "5", "1", "1.4", "16"),
            WeaponProperty = phalanx
                ? WeaponProperty(
                    "BRACE", "BRACE", mastery, mastery)
                : WeaponProperty(
                    "SHIELDING BLOWS", "HOLD FAST",
                    "Attacks grant 3 Shield",
                    "Basic attacks grant the wielder 3 Shield."),
            PathTiers = EmptyPathTiers(),
            KeywordNotes = new List<string>(PlayerRuleProjection.Keywords(def)),
            // Mirrors RunShell.BuildInspectorSections for a Recruit dossier — the live
            // muster inspector always carries composed sections.
            Sections = new List<InspectorSectionModel>
            {
                Rule("SIGNATURE", rules.SignatureName, Expand(
                    rules.SignatureText, expanded),
                    UiGlyphId.Mana, def.ManaMax.ToString()),
                Rule("WEAPON", phalanx ? "Brace" : "Tower Shield",
                    Expand(weaponRule, expanded)),
                Rule("PASSIVE", rules.PassiveName, rules.PassiveText,
                    role: InspectorSectionRole.Deferred),
            },
            Actions = new List<InspectorActionModel>
            {
                new InspectorActionModel
                {
                    Id = HallActionId.Buy,
                    Label = "MUSTER · SLOT 3",
                    Primary = true,
                    Enabled = true,
                },
            },
        };
        // The reported Phalanx screen uses the live unit-sheet anatomy, where Riposte is fully
        // expanded. Keep that exact renderer in the regression fixture; a deferred one-line row
        // would let the clipping bug pass without ever laying out the passive copy.
        if (phalanx) inspector.UnitSheet = FixtureUnitSheet(inspector);
        return inspector;
    }

    private static WarbandBarModel MusterBar()
    {
        var bar = new WarbandBarModel
        {
            Mode = WarbandBarMode.HallEditable,
            MusterMode = true,
            FieldCount = 2,
            FieldCapacity = 3,
            MaxFieldCapacity = 6,
            ReserveCount = 0,
            ReserveCapacity = 2,
            StoredItems = 0,
            CanManage = false,
            CanEdit = false,
        };
        string[] picked = { "shade", "berserker" };
        string[] names = { "Shade", "Berserker" };
        string[] accents = { "precision", "power" };
        for (int i = 0; i < 6; i++)
        {
            if (i < picked.Length)
            {
                UnitDef def = Loadout.Compose(Kits.Chassis[picked[i]]).Def;
                ChampionRuleProjection rules = PlayerRuleProjection.Champion(def);
                bar.Field.Add(new WarbandHeroModel
                {
                    HeroInstanceId = 900_000_000 + i,
                    FieldIndex = i,
                    SlotIndex = i,
                    ClassName = names[i],
                    Role = "FIELD",
                    Rank = "C",
                    PortraitResource = "UI/Portraits/" + picked[i],
                    PortraitFallback = names[i].Substring(0, 2).ToUpperInvariant(),
                    Accent = accents[i],
                    SignatureIcon = "✦",
                    SignatureName = rules.SignatureName,
                    SignatureRule = rules.SignatureText,
                    SignatureMana = def.ManaMax,
                    Weapon = new WarbandEquipmentModel
                    {
                        Kind = 0, Icon = "⚔",
                        Name = i == 0 ? "Twin Daggers" : "Greataxe",
                        Tier = "W",
                        Rule = "Starter weapon.",
                        Starter = true,
                    },
                    Trinket = new WarbandEquipmentModel
                    {
                        Kind = 1, Icon = "◇",
                        Name = "Empty trinket socket",
                        Empty = true,
                    },
                });
            }
            else if (i == picked.Length)
            {
                bar.Field.Add(new WarbandHeroModel
                {
                    FieldIndex = i,
                    SlotIndex = i,
                    Awaiting = true,
                    AwaitingLabel = "3RD PICK",
                });
            }
            else
            {
                bar.Field.Add(new WarbandHeroModel
                {
                    FieldIndex = i,
                    SlotIndex = i,
                    Empty = i < 3,
                    Locked = i >= 3,
                });
            }
        }
        // No reserve at muster: nothing exists to hold back, so the group stays hidden.
        return bar;
    }

    /// <summary>Rank-up modal (workbench-frame): Phalanx's real B fork — Pikewall vs
    /// Lancer — with the awaiting B slot on the card. Mirrors the approved 02 sample.</summary>
    private static SpecChoiceModel RankUpModalChoice(bool expanded)
    {
        return new SpecChoiceModel
        {
            Pending = true,
            HeroName = "Phalanx",
            RankLabel = "RANK B",
            Fork = true,
            FromRank = "C",
            ToRank = "B",
            BumpText = "+30 HEALTH · +2 POWER — THEN BIND THE PATH",
            PortraitResource = "UI/Portraits/phalanx",
            PortraitFallback = "PH",
            Accent = "ward",
            SignatureIcon = "↶",
            WeaponFilled = true,
            TrinketFilled = false,
            PathTiers = new List<RankTierSlotModel>
            {
                new RankTierSlotModel
                {
                    Rank = "B", State = RankTierSlotState.Pending, Icon = "◈",
                    Name = "THE FORK",
                },
                new RankTierSlotModel
                {
                    Rank = "A", State = RankTierSlotState.Locked, Icon = "◇",
                    Name = "AWAKENS AT RANK A",
                },
                new RankTierSlotModel
                {
                    Rank = "S", State = RankTierSlotState.Locked, Icon = "◇",
                    Name = "AWAKENS AT RANK S",
                },
            },
            Options = new List<SpecOptionModel>
            {
                RealSpecOption("phalanx.pikewall", "↶", expanded),
                RealSpecOption("phalanx.lancer", "⟶", expanded),
            },
        };
    }

    private static SpecOptionModel RealSpecOption(
        string nodeId, string icon, bool expanded)
    {
        string chassisId = nodeId.Substring(0, nodeId.IndexOf('.'));
        ChassisDef chassis = Kits.Chassis[chassisId];
        UnitDef before = Loadout.Compose(chassis).Def;
        UnitDef after = Loadout.Compose(
            chassis, nodes: new[] { Kits.Nodes[nodeId] }).Def;
        SpecializationRuleProjection rule = PlayerRuleProjection.Specialization(
            chassisId, nodeId, before, after);
        var option = new SpecOptionModel
        {
            Name = rule.Name,
            Change = rule.Change.ToString().ToUpperInvariant(),
            Icon = icon,
            Text = Expand(rule.Choice, expanded),
        };

        int? beforeLine = SignatureLine(before);
        int? afterLine = SignatureLine(after);
        if (beforeLine.HasValue && afterLine.HasValue &&
            beforeLine.Value != afterLine.Value)
            option.Comparisons.Add(new StatComparisonModel
            {
                Label = "SIGNATURE LINE",
                Before = beforeLine.Value <= 0 ? "BOARD" : beforeLine.Value.ToString(),
                After = afterLine.Value <= 0 ? "BOARD" : afterLine.Value.ToString(),
            });
        return option;
    }

    private static int? SignatureLine(UnitDef unit)
    {
        foreach (EffectDef effect in unit.Signature)
            if (effect.Select.Kind == SelKind.EnemiesOnLineThroughTarget ||
                effect.Select.Kind == SelKind.EnemiesOnLineThroughFarthest)
                return effect.Select.Range;
        return null;
    }

    private static WarbandBarModel FullWarbandBar()
    {
        var bar = new WarbandBarModel
        {
            Mode = WarbandBarMode.HallEditable,
            FieldCount = 6,
            FieldCapacity = 6,
            MaxFieldCapacity = 6,
            ReserveCount = 2,
            ReserveCapacity = 2,
            StoredItems = 8,
            CanManage = true,
            CanEdit = true,
            FocusedHeroInstanceId = 1,
        };
        string[] heroes =
        {
            "Banneret", "Bulwark", "Cleric", "Sharpshot", "Pyromancer", "Shade",
            "Berserker", "Phalanx",
        };
        string[] portraits =
        {
            "banneret", "bulwark", "cleric", "sharpshot", "pyromancer", "shade",
            "berserker", "phalanx",
        };
        string[] accents =
            { "precision", "ward", "mending", "precision", "power", "affliction",
              "power", "ward" };
        for (int i = 0; i < heroes.Length; i++)
        {
            var hero = new WarbandHeroModel
            {
                HeroInstanceId = i + 1,
                FieldIndex = i < 6 ? i : -1,
                SlotIndex = i < 6 ? i : i - 6,
                Reserve = i >= 6,
                Selected = i == 0,
                ClassName = heroes[i],
                Role = i % 2 == 0 ? "FRONTLINE" : "BACKLINE",
                Rank = i == 0 ? "A" : "B",
                PortraitResource = "UI/Portraits/" + portraits[i],
                PortraitFallback = heroes[i].Substring(0, 2).ToUpperInvariant(),
                Accent = accents[i],
                Weapon = new WarbandEquipmentModel
                {
                    Kind = 0, ItemInstanceId = 100 + i, Icon = "⚔",
                    Name = i == 0 ? "Ashwood Spear" : "Field Weapon",
                    Tier = i == 0 ? "R" : "W",
                    Rule = "Deal damage to the nearest enemy.",
                    Transferable = true,
                    Facts = new List<StatChipModel>
                    {
                        Fact("POWER", (12 + i).ToString(), PresentationFactId.BasicPower),
                        Fact("REACH", "2", PresentationFactId.Reach),
                    },
                },
                Trinket = new WarbandEquipmentModel
                {
                    Kind = 1, ItemInstanceId = 200 + i, Icon = "◇",
                    Name = i == 0 ? "Oathkeeper Seal" : "Field Trinket",
                    Tier = "B",
                    Rule = "Gain Protection when wounded.",
                    Transferable = true,
                    Facts = new List<StatChipModel>
                    {
                        Fact("HP", "+18", PresentationFactId.Hp),
                        Fact("WARD", "24", PresentationFactId.Protection),
                    },
                },
                Specs = new List<WarbandSpecBadgeModel>
                {
                    new WarbandSpecBadgeModel
                    {
                        Rank = "A", Icon = "✦", Name = "Last Light",
                        Rule = "Prevent the first allied defeat.", Accent = accents[i],
                    },
                },
            };
            (i < 6 ? bar.Field : bar.Reserve).Add(hero);
        }
        return bar;
    }

    private static WarbandBarModel OpenWarbandBar()
    {
        WarbandBarModel bar = FullWarbandBar();
        bar.FieldCount = 5;
        bar.ReserveCount = 1;
        bar.Field.RemoveAt(5);
        bar.Field.Add(new WarbandHeroModel
        {
            FieldIndex = 5,
            SlotIndex = 5,
            Empty = true,
        });
        bar.Reserve.RemoveAt(1);
        bar.Reserve.Add(new WarbandHeroModel
        {
            SlotIndex = 1,
            Reserve = true,
            Empty = true,
        });
        return bar;
    }

    private static List<InspectorActionModel> BuyActions(string label, int cost) =>
        new List<InspectorActionModel>
        {
            new InspectorActionModel
            {
                Id = HallActionId.Buy, Label = label, CurrencyCost = cost,
                CurrencyBalance = 31, Primary = true, Enabled = true,
            },
            new InspectorActionModel
            {
                Id = HallActionId.Freeze, Label = "HOLD", Enabled = true,
            },
        };

    private static InspectorSectionModel Rule(
        string label, string name, string summary,
        UiGlyphId labelGlyph = UiGlyphId.Unknown, string labelValue = "",
        InspectorSectionRole role = InspectorSectionRole.Primary) =>
        new InspectorSectionModel
        {
            Kind = InspectorSectionKind.Rule,
            Role = role,
            Label = label,
            Name = name,
            Summary = summary,
            LabelGlyph = labelGlyph,
            LabelValue = labelValue,
        };

    private static InspectorSectionModel Comparison(
        string label, params StatComparisonModel[] rows) =>
        new InspectorSectionModel
        {
            Kind = InspectorSectionKind.Comparison,
            Label = label,
            Comparisons = new List<StatComparisonModel>(rows),
        };

    private static StatComparisonModel Compare(
        string label, string before, string after, DeltaDirection direction) =>
        new StatComparisonModel
        {
            Label = label, Before = before, After = after, Direction = direction,
        };

    private static StatChipModel Fact(
        string label, string value, PresentationFactId id) =>
        new StatChipModel(label, value, id: id);

    private static RuleDeltaModel WeaponProperty(
        string name, string displayName, string summary, string fullRule,
        bool applies = true) =>
        new RuleDeltaModel
        {
            RuleName = name,
            DisplayName = displayName,
            ShortSummary = summary,
            FullDescription = fullRule,
            Icon = "◆",
            Applies = applies,
        };

    private static List<StatChipModel> CombatStats(
        string hp, string power, string reach, string cadence, string manaPerHit = "10") =>
        new List<StatChipModel>
        {
            Fact("HP", hp, PresentationFactId.Hp),
            Fact("POWER", power, PresentationFactId.BasicPower),
            Fact("REACH", reach, PresentationFactId.Reach),
            Fact("CADENCE", cadence, PresentationFactId.Cadence),
            Fact("MANA / HIT", manaPerHit, PresentationFactId.ManaPerSwing),
        };

    private static string Expand(string text, bool expanded)
    {
        if (!expanded || string.IsNullOrEmpty(text)) return text;
        const string stress =
            " Exact values and timing remain fully explicit in every translation.";
        int additionalCharacters = Math.Max(
            12, (int)Math.Ceiling(text.Length * 0.30d));
        return text + stress.Substring(
            0, Math.Min(additionalCharacters, stress.Length));
    }

    private static string Normalize(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "market-recruit";
        string normalized = id.Trim().ToLowerInvariant();
        return Array.IndexOf(Ids, normalized) >= 0 ? normalized : "market-recruit";
    }
}
