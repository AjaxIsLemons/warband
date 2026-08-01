using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>
    /// A stable fingerprint of content. ADR 0008 has always specified that a run's provenance is
    /// `(seed, choice log, contentVersion)` and CLAUDE.md repeats it — but no contentVersion
    /// existed, so nothing could tell which content build produced a save or a snapshot.
    ///
    /// **The failure this exists to catch is a RETUNE, not a rename.** `RunController.Resume`
    /// already resolves every id eagerly, so a *renamed* node fails loudly. What it cannot see is
    /// the same `cleric.warpriest` with different numbers: the run's encounters are derived from the
    /// seed at fight time, so resuming a save on a retuned build silently fights a different army
    /// than the one it was saved against, and a stored snapshot re-simulates to a different outcome.
    /// Silent divergence, and brutal to debug because everything looks correct.
    ///
    /// Which is why this walks the WHOLE graph — signatures, triggers, their conditions, their
    /// selectors, the fields they create, stat rules, mastery riders, patches — and not just the
    /// scalar stat block. A hash that missed a Burn magnitude buried in a trigger would be worse
    /// than no hash: it would promise a guarantee it does not keep.
    ///
    /// **Stability rules (both load-bearing):**
    /// - `string.GetHashCode()` is FORBIDDEN — .NET randomizes it per process, so the same content
    ///   would fingerprint differently on every launch. This uses explicit FNV-1a-64 over chars.
    /// - Callers must feed content in a deterministic order (sort dictionary keys). Order is part of
    ///   the hash by design: a reordered registry can change tell-matching ties and shop draws.
    /// </summary>
    public sealed class ContentHash
    {
        private const ulong Offset = 14695981039346656037;
        private const ulong Prime = 1099511628211;

        private ulong _h = Offset;

        public ulong Value => _h;

        /// <summary>16 hex chars — short enough to put in a save file and an error message.</summary>
        public string Hex => _h.ToString("x16");

        public ContentHash Add(int v)
        {
            unchecked
            {
                // Four bytes, low to high — endianness-independent because we do the splitting.
                for (int i = 0; i < 4; i++) { _h ^= (byte)(v >> (i * 8)); _h *= Prime; }
            }
            return this;
        }

        public ContentHash Add(bool v) => Add(v ? 1 : 0);

        /// <summary>Null and empty are distinguishable — `WeaponId = null` (chassis starter) is a
        /// different piece of content than `WeaponId = ""`.</summary>
        public ContentHash Add(string? s)
        {
            if (s == null) return Add(-1);
            Add(s.Length);
            unchecked
            {
                foreach (char c in s) { _h ^= (byte)c; _h *= Prime; _h ^= (byte)(c >> 8); _h *= Prime; }
            }
            return this;
        }

        public ContentHash Add<T>(T? nullable) where T : struct
        {
            // Nullable enums/ints: presence is significant (SignaturePatch.LineRange etc.).
            if (!nullable.HasValue) return Add(-1);
            Add(1);
            return Add(System.Convert.ToInt32(nullable.Value));
        }

        // ---- content graph ----------------------------------------------------------

        public ContentHash AddUnit(UnitDef d)
        {
            Add("unit").Add(d.Name).Add(d.MaxHp).Add(d.Attack).Add(d.AttackInterval).Add(d.Range)
                .Add(d.MoveInterval).Add((int)d.TargetPref).Add(d.Standoff).Add(d.ManaMax)
                .Add(d.ManaPerSwing).Add(d.ManaPerHitTaken).Add(d.CritChance).Add(d.CritMultFp)
                .Add(d.HealAutos).Add(d.CleavePct).Add(d.ExtraArrowPct)
                .Add(d.ChassisId).Add(d.WeaponName).Add((int)d.WeaponTier);
            AddEffects(d.Signature);
            AddTriggers(d.Triggers);
            AddStatRules(d.StatRules);
            Add(d.Traits.Count);
            foreach (string t in d.Traits) Add(t);
            return this;
        }

        public ContentHash AddChassis(ChassisDef c)
        {
            Add("chassis").Add(c.Id).Add(c.Name).Add(c.MaxHp).Add(c.MoveInterval)
                .Add((int)c.TargetPref).Add(c.Standoff).Add(c.ManaMax).Add(c.RankHp).Add(c.RankAttack);
            Add(c.Specializations.Count);
            foreach (string s in c.Specializations) Add(s);
            if (c.StarterWeapon != null) AddWeapon(c.StarterWeapon); else Add("no-starter");
            AddEffects(c.Signature);
            // SignatureTriggers is a presentation-identity split inside the chassis trigger
            // sequence. Hash the combined executable sequence exactly as before: moving an
            // unchanged trigger between those channels cannot invalidate saves or replays.
            Add(c.SignatureTriggers.Count + c.Passives.Count);
            foreach (Trigger trigger in c.SignatureTriggers) AddTrigger(trigger);
            foreach (Trigger trigger in c.Passives) AddTrigger(trigger);
            AddStatRules(c.StatRules);
            return this;
        }

        public ContentHash AddWeapon(WeaponDef w)
        {
            Add("weapon").Add(w.Name).Add(w.Category).Add(w.Damage).Add(w.Interval).Add(w.Range)
                .Add(w.ManaPerSwing).Add(w.CritChance).Add(w.CritMultFp).Add(w.HealAutos)
                .Add(w.CleavePct).Add(w.MasteryRangeBonus);
            AddTriggers(w.Triggers);
            AddStatRules(w.StatRules);
            AddTriggers(w.MasteryTriggers);
            AddStatRules(w.MasteryStatRules);
            return this;
        }

        public ContentHash AddTrinket(TrinketDef t)
        {
            Add("trinket").Add(t.Name).Add(t.HpBonus).Add(t.ManaMaxDelta);
            AddTriggers(t.Triggers);
            AddStatRules(t.StatRules);
            AddSpawnStatuses(t.SpawnStatuses);
            return this;
        }

        public ContentHash AddNode(SpecNode n)
        {
            Add("node").Add(n.Name).Add(n.HpBonus).Add(n.CleaveBonusPct);
            Add(n.TargetPref.HasValue).Add(n.TargetPref.HasValue ? (int)n.TargetPref!.Value : 0);
            Add(n.Standoff);
            AddTriggers(n.Triggers);
            AddStatRules(n.StatRules);
            AddSpawnStatuses(n.SpawnStatuses);
            if (n.SignatureOverride == null) Add("no-override"); else AddEffects(n.SignatureOverride);
            if (n.SignaturePatch == null) Add("no-patch"); else AddPatch(n.SignaturePatch);
            return this;
        }

        public ContentHash AddPatch(SignaturePatch p)
        {
            Add("patch").Add(p.RadiusDelta).Add(p.LineRange).Add(p.AmountPct).Add(p.Escalate)
                .Add(p.FieldRadius).Add(p.FieldTicks).Add(p.Repeat);
            AddEffects(p.Add);
            return this;
        }

        public ContentHash AddTriggers(List<Trigger> triggers)
        {
            Add(triggers.Count);
            foreach (var t in triggers) AddTrigger(t);
            return this;
        }

        private void AddTrigger(Trigger trigger)
        {
            Add((int)trigger.On).Add(trigger.OncePerRoot).Add(trigger.EveryN);
            Add(trigger.When.Count);
            foreach (var condition in trigger.When) AddCond(condition);
            AddEffects(trigger.Do);
        }

        public ContentHash AddEffects(List<EffectDef> effects)
        {
            Add(effects.Count);
            foreach (var e in effects)
            {
                Add((int)e.Kind).Add(e.Amount).Add((int)e.Status).Add(e.StatusTicks)
                    .Add(e.StatusSwings).Add(e.PctOfEventAmount).Add(e.ScaleByTargetStatus)
                    .Add((int)e.ScaleStatus).Add(e.ScaleByEventTargetStatus)
                    .Add(e.EscalatePctPerIndex).Add(e.AsCounter);
                AddSelector(e.Select);
                if (e.Field == null) Add("no-field"); else AddField(e.Field);
            }
            return this;
        }

        public ContentHash AddSelector(Selector s) =>
            Add((int)s.Kind).Add(s.Range).Add(s.ExcludeSelf).Add(s.AnchorEvent)
                .Add(s.AnchorEventTarget).Add(s.ExcludeAnchorUnit).Add(s.SkipCtxTarget)
                .Add(s.BelowHpPct).Add(s.MustHave).Add(s.AdjacentToAlly);

        public ContentHash AddCond(Cond c) =>
            Add((int)c.Kind).Add(c.Not).Add(c.Amount).Add((int)c.Cause).Add((int)c.Status);

        public ContentHash AddField(FieldDef f)
        {
            Add("field").Add(f.Radius).Add(f.Ticks).Add(f.IsWall).Add(f.AttachToOwner)
                .Add((int)f.PulseAffects).Add((int)f.PresenceAffects)
                .Add((int)f.ProjectileAffects).Add(f.ProjectileBonus);
            AddEffects(f.Pulse);
            Add(f.Presence.Count);
            foreach (var (kind, mag) in f.Presence) Add((int)kind).Add(mag);
            AddEffects(f.ProjectileRiders);
            return this;
        }

        public ContentHash AddStatRules(List<StatRule> rules)
        {
            Add(rules.Count);
            foreach (var r in rules)
            {
                Add((int)r.Stat).Add(r.Amount).Add((int)r.ScaleBy);
                Add(r.When.Count);
                foreach (var c in r.When) AddCond(c);
            }
            return this;
        }

        private ContentHash AddSpawnStatuses(List<(StatusKind Kind, int Mag)> statuses)
        {
            Add(statuses.Count);
            foreach (var (kind, mag) in statuses) Add((int)kind).Add(mag);
            return this;
        }
    }
}
