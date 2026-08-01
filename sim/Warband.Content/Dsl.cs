using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Content
{
    /// <summary>Terse builders so kit files read like the dive docs. Content is DATA —
    /// tweaking a node means editing one expression here, never touching the engine.</summary>
    internal static class D
    {
        // ---- selectors ----
        public static Selector Self => new Selector { Kind = SelKind.Self };
        public static Selector Cur => new Selector { Kind = SelKind.CurrentTarget };
        public static Selector EvSrc => new Selector { Kind = SelKind.EventSource };
        public static Selector EvTgt => new Selector { Kind = SelKind.EventTarget };
        public static Selector Nearest(StatusKind? must = null, bool atEvent = false, bool atCorpse = false, bool exAnchor = false) =>
            new Selector { Kind = SelKind.NearestEnemy, MustHave = must, AnchorEvent = atEvent,
                           AnchorEventTarget = atCorpse, ExcludeAnchorUnit = exAnchor };
        public static Selector Farthest => new Selector { Kind = SelKind.FarthestEnemy };
        public static Selector Lowest => new Selector { Kind = SelKind.LowestHpAlly };
        public static Selector Allies(int r, bool exSelf = true, int below = 0, bool besideAlly = false) =>
            new Selector { Kind = SelKind.AlliesWithin, Range = r, ExcludeSelf = exSelf, BelowHpPct = below,
                           AdjacentToAlly = besideAlly };
        /// <summary>The captain's Company: whoever his muster swore in at placement, at ANY range.
        /// A live radius cannot express this — the fight drags a warband apart within a few ticks,
        /// so a radius-2 rally lands on ~1 of 3 allies and the placement decision it was supposed
        /// to reward is erased by pathfinding. The roster is stamped once by an AtStart Mustered
        /// and read here, which is the same "locks at placement, follows the drift" law the muster
        /// Haste has always had (ADR 0014).</summary>
        public static Selector Company(int below = 0) =>
            new Selector { Kind = SelKind.AlliesWithin, Range = 99, ExcludeSelf = true,
                           BelowHpPct = below, MustHave = StatusKind.Mustered };
        public static Selector Enemies(int r, StatusKind? must = null, bool atVictim = false, bool exAnchor = false) =>
            new Selector { Kind = SelKind.EnemiesWithin, Range = r, MustHave = must,
                           AnchorEventTarget = atVictim, ExcludeAnchorUnit = exAnchor };
        public static Selector Line(int len = 0, bool behindOnly = false) =>
            new Selector { Kind = SelKind.EnemiesOnLineThroughTarget, Range = len, SkipCtxTarget = behindOnly };
        public static Selector LineFar(int len = 0) =>
            new Selector { Kind = SelKind.EnemiesOnLineThroughFarthest, Range = len };

        // ---- conditions ----
        public static Cond SrcOwner => new Cond { Kind = CondKind.SourceIsOwner };
        public static Cond TgtOwner => new Cond { Kind = CondKind.TargetIsOwner };
        public static Cond SrcEnemy => new Cond { Kind = CondKind.SourceIsEnemyOfOwner };
        public static Cond SrcAlly => new Cond { Kind = CondKind.SourceIsEnemyOfOwner, Not = true };
        public static Cond TgtAlly(bool not = false) => new Cond { Kind = CondKind.TargetIsAllyOfOwner, Not = not };
        public static Cond TgtAllied => new Cond { Kind = CondKind.TargetIsEnemyOfOwner, Not = true }; // ally OR owner
        public static Cond ByAttack => new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack };
        public static Cond ByAbility => new Cond { Kind = CondKind.CauseIs, Cause = Cause.Ability };
        public static Cond ByCounter => new Cond { Kind = CondKind.CauseIs, Cause = Cause.Counter };
        public static Cond ByNot(Cause c) => new Cond { Kind = CondKind.CauseIs, Cause = c, Not = true };
        public static Cond RootEv => new Cond { Kind = CondKind.IsRootEvent };
        public static Cond Crit => new Cond { Kind = CondKind.IsCrit };
        public static Cond TgtBelow(int pct) => new Cond { Kind = CondKind.TargetBelowHpPct, Amount = pct };
        public static Cond TgtAtRange(int r) => new Cond { Kind = CondKind.TargetAtRangeOfOwner, Amount = r };
        public static Cond TgtEngaged => new Cond { Kind = CondKind.TargetAdjacentToAllyOfOwner };
        public static Cond NoEnemyWithin(int r) => new Cond { Kind = CondKind.NoEnemyWithinHexesOfOwner, Amount = r };
        public static Cond OwnerHas(StatusKind s, bool not = false) => new Cond { Kind = CondKind.OwnerHasStatus, Status = s, Not = not };
        public static Cond TgtHas(StatusKind s, bool not = false) => new Cond { Kind = CondKind.TargetHasStatus, Status = s, Not = not };
        public static Cond SrcHas(StatusKind s) => new Cond { Kind = CondKind.SourceHasStatus, Status = s };
        public static Cond Nth(int n) => new Cond { Kind = CondKind.EveryNthSwingOfOwner, Amount = n };
        public static Cond StatusIs(StatusKind s) => new Cond { Kind = CondKind.StatusIs, Status = s };
        public static Cond SrcWithin(int r) => new Cond { Kind = CondKind.SourceWithinHexesOfOwner, Amount = r };
        public static Cond RecentDmg(int pct) => new Cond { Kind = CondKind.OwnerRecentDamageAbovePct, Amount = pct };
        public static Cond FiredTeamRule => new Cond { Kind = CondKind.EventRuleIsTeamRule };

        // ---- effects ----
        public static EffectDef Dmg(Selector sel, int amt, int pctOfEvent = 0, int escalate = 0) =>
            new EffectDef { Kind = EffectKind.Damage, Select = sel, Amount = amt,
                            PctOfEventAmount = pctOfEvent, EscalatePctPerIndex = escalate };
        public static EffectDef DmgPerStack(Selector sel, int perStack, StatusKind stack) =>
            new EffectDef { Kind = EffectKind.Damage, Select = sel, Amount = perStack, ScaleByTargetStatus = true, ScaleStatus = stack };
        public static EffectDef PassStack(StatusKind stack, Selector to) => new EffectDef // the corpse's pool moves on
            { Kind = EffectKind.ApplyStatus, Status = stack, Amount = 1, Select = to,
              ScaleByEventTargetStatus = true, ScaleStatus = stack };
        public static EffectDef Heal(Selector sel, int amt, int pctOfEvent = 0) =>
            new EffectDef { Kind = EffectKind.Heal, Select = sel, Amount = amt, PctOfEventAmount = pctOfEvent };
        public static EffectDef Shield(Selector sel, int amt, int pctOfEvent = 0) =>
            new EffectDef { Kind = EffectKind.GrantShield, Select = sel, Amount = amt, PctOfEventAmount = pctOfEvent };
        public static EffectDef Mana(Selector sel, int amt) =>
            new EffectDef { Kind = EffectKind.GrantMana, Select = sel, Amount = amt };
        public static EffectDef Status(StatusKind s, int mag, Selector sel, int ticks = -1, int swings = 0) =>
            new EffectDef { Kind = EffectKind.ApplyStatus, Status = s, Amount = mag, StatusTicks = ticks, StatusSwings = swings, Select = sel };
        public static EffectDef Strip(StatusKind s, Selector sel) =>
            new EffectDef { Kind = EffectKind.RemoveStatus, Status = s, Select = sel };
        public static EffectDef Spend(StatusKind s, Selector sel, int stacks = 1) =>
            new EffectDef
            {
                Kind = EffectKind.RemoveStatus,
                Status = s,
                Amount = stacks,
                Select = sel,
            };
        public static EffectDef Swing(Selector sel, int pct = 0, bool counter = false) =>
            new EffectDef { Kind = EffectKind.Swing, Select = sel, Amount = pct, AsCounter = counter };
        public static EffectDef Execute(Selector sel) =>
            new EffectDef { Kind = EffectKind.Execute, Select = sel };
        public static EffectDef Recast(Selector sel) =>
            new EffectDef { Kind = EffectKind.Recast, Select = sel };
        public static EffectDef Leap(Selector sel) =>
            new EffectDef { Kind = EffectKind.Leap, Select = sel };
        public static EffectDef Glyph(Selector sel, FieldDef field) =>
            new EffectDef { Kind = EffectKind.CreateField, Select = sel, Field = field };

        // ---- signature patches: degree, not verb (2026-07-25 systems review §4) ----
        /// <summary>Author "the same signature, more of it". Use a SignatureOverride only when the
        /// node genuinely changes the VERB — a patch keeps the inherited effects, so an A-rank
        /// amplifier survives an S-rank crown instead of being silently replaced by it.</summary>
        public static SignaturePatch Patch(int radius = 0, int? line = null, int amountPct = 100,
                                           int? escalate = null, int? fieldRadius = null,
                                           int? fieldTicks = null, int repeat = 1, params EffectDef[] add)
        {
            var p = new SignaturePatch
            {
                RadiusDelta = radius, LineRange = line, AmountPct = amountPct, Escalate = escalate,
                FieldRadius = fieldRadius, FieldTicks = fieldTicks, Repeat = repeat,
            };
            p.Add.AddRange(add);
            return p;
        }

        // ---- triggers / rules ----
        public static Trigger On(EventKind ev, Cond[] when, params EffectDef[] effects)
        {
            var t = new Trigger { On = ev };
            t.When.AddRange(when);
            t.Do.AddRange(effects);
            return t;
        }
        public static Trigger AtStart(params EffectDef[] effects) => On(EventKind.BattleStart, new Cond[0], effects);

        /// <summary>Name a rule that is added DIRECTLY to a composed UnitDef rather than through
        /// Loadout.Compose — authored enemies and encounter bodies (Enemies.cs, Encounters.cs) build
        /// on top of a composed kit, so they miss the composer's automatic stamping. An unnamed rule
        /// is not broken (it falls through to the generic passive tell, by design — nothing is ever
        /// silent), but these are exactly the mechanics ADR 0024's disclosure contract promises the
        /// player, so they get to say their own name. Mutates in place: these instances are built
        /// per call by the DSL and never shared with the catalog.</summary>
        public static Trigger Named(this Trigger t, string ruleId) { t.RuleId = ruleId; return t; }
        public static StatRule Named(this StatRule r, string ruleId) { r.RuleId = ruleId; return r; }
        /// <summary>Counter shape (ADR 0026): fire on every Nth match instead of every one.
        /// Progress rides RuleProgress so the badge rail can show pips.</summary>
        public static Trigger Every(this Trigger t, int n) { t.EveryN = n; return t; }
        /// <summary>Once-per-root guard for a KIT trigger (Living Inscription). Inscription team
        /// triggers get this stamped automatically by Catalog.Identify — never call it there.</summary>
        public static Trigger Guarded(this Trigger t) { t.OncePerRoot = true; return t; }
        public static Cond[] W(params Cond[] c) => c;
        public static StatRule Rule(StatKind stat, int amt, StatScale scale = StatScale.None, params Cond[] when)
        {
            var r = new StatRule { Stat = stat, Amount = amt, ScaleBy = scale };
            r.When.AddRange(when);
            return r;
        }
    }
}
