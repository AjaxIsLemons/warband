using System;
using System.Collections.Generic;

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
        "market-recruit",
        "market-rankup-b",
        "market-rankup-long",
        "market-rankup-s",
        "market-weapon",
        "market-inscription",
        "armory-empty",
        "armory-full",
        "rail-full",
        "tooltip-keyword",
        "tooltip-equipment",
        "tooltip-rank-tier",
    };

    internal static Fixture Build(
        string requestedId, bool expandedText = false, bool reducedMotion = false)
    {
        string id = Normalize(requestedId);
        var shell = BaseShell(expandedText, reducedMotion);
        switch (id)
        {
            case "market-rankup-b":
                SelectRankUp(shell, expandedText, 1);
                break;
            case "market-rankup-long":
                SelectRankUp(shell, expandedText, 2);
                break;
            case "market-rankup-s":
                SelectRankUp(shell, expandedText, 3);
                break;
            case "tooltip-rank-tier":
                SelectRankUp(shell, expandedText, 3);
                break;
            case "market-weapon":
                Select(shell, "fixture:weapon", WeaponInspector(expandedText));
                break;
            case "market-inscription":
                Select(shell, "fixture:inscription", InscriptionInspector(expandedText));
                break;
            case "armory-empty":
                shell.Planning.PartyShelf.Expanded = true;
                shell.Planning.PartyShelf.LoadoutInventory.Clear();
                shell.Planning.PartyShelf.LoadoutInspector = HeroInspector(expandedText);
                break;
            case "armory-full":
                shell.Planning.PartyShelf.Expanded = true;
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
        MarketOfferPresentationContract.Validate(shell.Planning.MarketOffers);
        return new Fixture(
            id, shell,
            id == "tooltip-keyword",
            id == "tooltip-equipment",
            id == "tooltip-rank-tier");
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

    private static void Select(RunShellModel shell, string key, InspectorModel inspector)
    {
        shell.Planning.Inspector = inspector;
        for (int i = 0; i < shell.Planning.MarketOffers.Count; i++)
            shell.Planning.MarketOffers[i].Selected =
                shell.Planning.MarketOffers[i].Key == key;
    }

    private static void SelectRankUp(RunShellModel shell, bool expanded, int nextTier)
    {
        InspectorModel inspector = RankUpInspector(expanded, nextTier);
        Select(shell, "fixture:rankup", inspector);
        for (int i = 0; i < shell.Planning.MarketOffers.Count; i++)
        {
            MarketOfferCardModel offer = shell.Planning.MarketOffers[i];
            if (offer.Key != "fixture:rankup") continue;
            offer.Title = inspector.Title;
            offer.Metrics = new List<StatChipModel>(inspector.Stats);
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
                "BASIC ATTACK", Expand(
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
        return new InspectorModel
        {
            Key = "fixture:recruit",
            Kind = DecisionDetailKind.Recruit,
            Eyebrow = "LIVE MARKET · RECRUIT",
            Title = "Banneret",
            Subtitle = "Captain · Rank C · joins the field",
            PortraitResource = "UI/Portraits/banneret",
            PortraitFallback = "BN",
            Accent = "precision",
            CurrencyCost = 12,
            CurrencyBalance = 31,
            Stats = CombatStats("164", "14", "2", "1.1"),
            KeywordNotes = new List<string>
            {
                "PROTECTION · Absorbs incoming damage before Health.",
                "COMMAND · A rule that strengthens nearby allies.",
            },
            Sections = new List<InspectorSectionModel>
            {
                Rule("BASIC ATTACK", "DISCIPLINED STRIKE",
                    Expand("Deal 14 damage to the nearest enemy at Reach 2.", expanded)),
                Rule("SIGNATURE", "HOLD THE LINE",
                    Expand("Grant 24 Protection to the two lowest-Health allies for 5 seconds.",
                        expanded), UiGlyphId.Mana, "70"),
                Rule("PASSIVE", "COMMAND AURA",
                    Expand("At battle start, adjacent allies gain 18 Protection for 4 seconds.",
                        expanded)),
            },
            Actions = BuyActions("RECRUIT", 12),
        };
    }

    private static InspectorModel RankUpInspector(bool expanded, int nextTier)
    {
        string current = nextTier == 1 ? "C" : nextTier == 2 ? "B" : "A";
        string next = nextTier == 1 ? "B" : nextTier == 2 ? "A" : "S";
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
        if (nextTier == 3)
            return new List<ChoicePreviewModel>
            {
                Choice("CROWN · PROTECTION", "Great Chorus",
                    "Sanctuary resolves twice; the second pulse finds the new lowest-Health ally.",
                    "ward", expanded),
                Choice("CROWN · RECOVERY", "Sanctuary",
                    "Allies restored by the signature leave consecrated ground beneath them.",
                    "mending", expanded),
            };
        return new List<ChoicePreviewModel>
        {
            Choice("PATH OF MERCY", "Last Light",
                "The first allied defeat each battle is prevented; that ally instead returns " +
                "with 28 Health and cannot trigger this again.", "ward", expanded),
            Choice("PATH OF ZEAL", "Radiant Verdict",
                "Sanctuary also scorches the nearest enemy for 16 damage and restores 5 Mana " +
                "when its healing reaches a wounded ally.", "power", expanded),
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
                Accent = chosen?.Accent ?? (pending ? "choice" : ""),
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
                Rule("BASIC ATTACK", "SOLAR EDGE",
                    Expand("Deal 19 damage at Reach 2. Each swing grants 2 Mana.", expanded)),
                Comparison("ON SELECTED UNIT",
                    Compare("POWER", "14", "19", DeltaDirection.Positive),
                    Compare("REACH", "3", "2", DeltaDirection.Contextual),
                    Compare("CADENCE", "0.9", "1.0", DeltaDirection.Contextual)),
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
            Eyebrow = "FIELD I · RANK A",
            Title = "Banneret",
            Subtitle = "Captain · composed unit dossier",
            PortraitResource = "UI/Portraits/banneret",
            PortraitFallback = "BN",
            Accent = "precision",
            Stats = CombatStats("188", "17", "2", "1.1"),
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
                Rule("BASIC ATTACK", "DISCIPLINED STRIKE",
                    Expand("Deal 17 damage to the nearest enemy at Reach 2.", expanded)),
                Rule("SIGNATURE", "HOLD THE LINE",
                    Expand("Grant 24 Protection and Haste to the two lowest-Health allies.",
                        expanded), UiGlyphId.Mana, "70"),
                Rule("PASSIVE", "COMMAND AURA",
                    Expand("Adjacent allies begin battle with 18 Protection for 4 seconds.",
                        expanded)),
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
            KeywordNotes = new List<string>
            {
                "RIPOSTE · One stored counterattack; spent when it fires.",
                "LINE · Hits every enemy on the traced hex line.",
            },
            Sections = new List<InspectorSectionModel>
            {
                Rule("BASIC ATTACK", "PIKE",
                    Expand("Deal 14 damage to the nearest enemy at Reach 2.", expanded)),
                Rule("SIGNATURE", "SKEWER",
                    Expand(
                        "Enemies on a 3-hex Line through the current target take 12 damage.",
                        expanded), UiGlyphId.Mana, "35"),
                Rule("PASSIVE", "RIPOSTE",
                    Expand(
                        "Gain 1 Riposte. When hit by a basic attack, spend it to " +
                        "counterattack that attacker.", expanded)),
            },
        };
    }

    private static InspectorModel ProjectedHeroInspector(bool expanded)
    {
        InspectorModel model = HeroInspector(expanded);
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
        UiGlyphId labelGlyph = UiGlyphId.Unknown, string labelValue = "") =>
        new InspectorSectionModel
        {
            Kind = InspectorSectionKind.Rule,
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

    private static List<StatChipModel> CombatStats(
        string hp, string power, string reach, string cadence) =>
        new List<StatChipModel>
        {
            Fact("HP", hp, PresentationFactId.Hp),
            Fact("POWER", power, PresentationFactId.BasicPower),
            Fact("REACH", reach, PresentationFactId.Reach),
            Fact("CADENCE", cadence, PresentationFactId.Cadence),
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
