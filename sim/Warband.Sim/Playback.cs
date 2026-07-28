using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>The client-facing view of one unit — exactly what a renderer shows.</summary>
    public sealed class PlaybackUnit
    {
        public int Id;
        public int Team;
        public string Name = "";
        public int MaxHp;
        public int Hp;
        public int Shield;
        public int Mana;
        public int ManaMax;
        public Hex Pos;
        public bool Dead;
        /// <summary>ExpiryTick is the playhead tick this entry runs out on, -1 when it is
        /// permanent or swing-scoped. It is DECORATION (a countdown ring) — expiry truth stays
        /// the StatusExpired event, so it is deliberately outside <see cref="PlaybackState.HashView"/>.</summary>
        public List<(StatusKind Kind, int Mag, int ExpiryTick)> Statuses = new List<(StatusKind, int, int)>();

        /// <summary>Indices into <see cref="PlaybackState.RuleIds"/> for the conditional passives
        /// currently ONLINE on this unit, rebuilt by the fold from RuleChanged. Like ExpiryTick it
        /// is deliberately outside <see cref="PlaybackState.HashView"/>: the sim's own projection
        /// (<see cref="From"/>) cannot fill it — evaluating a StatRule's conditions needs the whole
        /// battle, which is precisely why the transitions had to become events — so hashing it would
        /// make the guardrail compare a folded value against a blank one and fail every tick.
        /// The RuleChanged event is the authority, exactly as StatusExpired is for a countdown.</summary>
        public List<int> ActiveRules = new List<int>();

        // ---- committed step (movement law) ----
        // Pos is where the unit IS. While walking it is also still standing there as far as every
        // rule is concerned; StepTo is where it will be at StepEnd. The renderer interpolates
        // Pos → StepTo across [StepStart, StepEnd] and lands exactly when the sim says it lands, so
        // "what you see" and "what resolved" can never disagree by more than a frame.
        private Hex _stepTo;
        public int StepStart;
        public int StepEnd;
        public bool Walking => StepEnd > StepStart;

        /// <summary>Destination while walking, own hex otherwise — derived, so standing still can
        /// never carry a stale destination (mirrors UnitState.StepTo).</summary>
        public Hex StepTo { get => Walking ? _stepTo : Pos; set => _stepTo = value; }

        // Base stat block — what this unit IS, before statuses modulate it. Constant for the whole
        // fight, so the fold never rewrites them. Deployment reads these to answer the questions
        // placement actually poses: how far do I reach, how fast do I swing, how fast do I close.
        public int Range;
        public int Attack;
        public int AttackInterval;
        public int MoveInterval;
        public int CritChance;
        public bool HealAutos;      // censer law: this unit's swings HEAL allies, not damage enemies
        /// <summary>The targeting rule — "acquires NEAREST/FARTHEST/LOWEST", and the standoff it
        /// keeps while its target is inside weapon reach. pve-encounters.md requires targeting to be
        /// inspectable, and a Sanddrift Gunner whose ENTIRE design is "acquires FARTHEST, holds
        /// standoff 5" was previously a name and a health number on the board.</summary>
        public TargetPref TargetPref;
        public int Standoff;

        /// <summary>This unit's span in the battle's rule table (<see cref="PlaybackState.RuleIds"/>)
        /// — what passives it HAS, as opposed to <see cref="ActiveRules"/>, which is the subset
        /// currently live. Together they are the whole "what is this unit and why did it do that"
        /// answer the in-fight inspector owes the player.</summary>
        public int TriggerRuleBase = -1, TriggerRuleCount;
        public int StatRuleBase = -1, StatRuleCount;

        // Identity — the stable content keys behind the display Name (which authored encounters
        // rename freely, e.g. "Oathbound Bulwark").
        public string ChassisId = "";
        public string WeaponName = "";
        public WeaponTier WeaponTier;
        public List<string> Traits = new List<string>();

        /// <summary>
        /// Project live battle state into the renderer contract. Placement previews use this same
        /// projection as Battle's initial snapshot, so Unity never grows a second unit-view model.
        /// </summary>
        public static PlaybackUnit From(UnitState u)
        {
            var view = new PlaybackUnit
            {
                Id = u.Id, Team = u.Team, Name = u.Def.Name, MaxHp = u.Def.MaxHp,
                Hp = u.Hp, Shield = u.Shield, Mana = u.Mana, ManaMax = u.Def.ManaMax,
                Pos = u.Pos, Dead = u.Dead,
                StepTo = u.StepTo, StepStart = u.StepStart, StepEnd = u.StepEnd,
                Range = u.Def.Range, Attack = u.Def.Attack, AttackInterval = u.Def.AttackInterval,
                MoveInterval = u.Def.MoveInterval, CritChance = u.Def.CritChance,
                HealAutos = u.Def.HealAutos,
                TargetPref = u.Def.TargetPref, Standoff = u.Def.Standoff,
                TriggerRuleBase = u.TriggerBase, TriggerRuleCount = u.Def.Triggers.Count,
                StatRuleBase = u.StatRuleBase, StatRuleCount = u.Def.StatRules.Count,
                ChassisId = u.Def.ChassisId, WeaponName = u.Def.WeaponName,
                WeaponTier = u.Def.WeaponTier,
                // Shared, not copied: the guardrail projects every unit EVERY tick, and Traits is
                // immutable Def data that nothing downstream writes. Clone() still copies, so any
                // owned snapshot (PlaybackState.From) gets its own list.
                Traits = u.Def.Traits,
            };
            // TicksLeft IS the expiry tick here: the only consumers of a projected ExpiryTick are
            // the tick-0 initial snapshot and deployment previews. Mid-fight projections exist
            // solely to feed the view hash, which excludes the countdown.
            foreach (var s in u.Statuses)
                view.Statuses.Add((s.Kind, s.Mag, s.TicksLeft > 0 ? s.TicksLeft : -1));
            return view;
        }

        public PlaybackUnit Clone() => new PlaybackUnit
        {
            Id = Id, Team = Team, Name = Name, MaxHp = MaxHp, Hp = Hp, Shield = Shield,
            Mana = Mana, ManaMax = ManaMax, Pos = Pos, Dead = Dead,
            StepTo = StepTo, StepStart = StepStart, StepEnd = StepEnd,
            Statuses = new List<(StatusKind, int, int)>(Statuses),
            ActiveRules = new List<int>(ActiveRules),
            Range = Range, Attack = Attack, AttackInterval = AttackInterval,
            MoveInterval = MoveInterval, CritChance = CritChance, HealAutos = HealAutos,
            TargetPref = TargetPref, Standoff = Standoff,
            TriggerRuleBase = TriggerRuleBase, TriggerRuleCount = TriggerRuleCount,
            StatRuleBase = StatRuleBase, StatRuleCount = StatRuleCount,
            ChassisId = ChassisId, WeaponName = WeaponName, WeaponTier = WeaponTier,
            Traits = new List<string>(Traits),
        };
    }

    /// <summary>
    /// The log-reconstruction fold (render-contract.md): (initial snapshot, events) →
    /// per-tick view state. This IS the view-model — the sim-side guardrail test, the
    /// metrics fold, and the renderer all consume this one implementation. It applies
    /// absolutes carried on events and NEVER runs combat logic.
    /// </summary>
    public sealed class PlaybackState
    {
        public List<PlaybackUnit> Units = new List<PlaybackUnit>();
        public List<PlaybackField> Fields = new List<PlaybackField>();
        public int Tick;
        private int _next; // index into the event list

        /// <summary>The battle's rule-id table (BattleResult.RuleIds), carried so the renderer can
        /// turn a TriggerFired/RuleChanged Aux index into a name. Empty outside a battle — a
        /// deployment preview folds no events.</summary>
        public List<string> RuleIds = new List<string>();

        /// <summary>Index into <see cref="RuleIds"/>, or "" when it names nothing — an unnamed rule
        /// is legal and falls through to the generic passive tell by design.</summary>
        public string RuleId(int index) =>
            index >= 0 && index < RuleIds.Count ? RuleIds[index] : "";

        /// <summary>Counter-rule progress by rule index — the badge rail's pips (ADR 0026), folded
        /// from RuleProgress so scrubbing to tick N reconstructs "2 of 3" like any other state.
        /// SET from the event, never accumulated (ADR 0004). State-level because a team rule
        /// belongs to no unit. Like ExpiryTick it is decoration, deliberately NOT in ViewHash —
        /// Battle's per-tick hashes are projections of unit state and know nothing of it.</summary>
        public Dictionary<int, (int Progress, int N)> RuleCounters = new Dictionary<int, (int, int)>();

        public static PlaybackState From(IEnumerable<PlaybackUnit> initial, IEnumerable<string>? ruleIds = null)
        {
            var s = new PlaybackState();
            foreach (var u in initial)
                s.Units.Add(u.Clone());
            if (ruleIds != null) s.RuleIds.AddRange(ruleIds);
            return s;
        }

        public PlaybackUnit? ById(int id)
        {
            foreach (var u in Units)
                if (u.Id == id)
                    return u;
            return null;
        }

        /// <summary>Apply every event with Tick ≤ tick, in log order.</summary>
        public void AdvanceToTick(List<BattleEvent> events, int tick)
        {
            while (_next < events.Count && events[_next].Tick <= tick)
                Apply(events[_next++]);
            Tick = tick;
        }

        private void Apply(BattleEvent e)
        {
            var src = ById(e.Source);
            var tgt = ById(e.Target);
            switch (e.Kind)
            {
                case EventKind.MoveStart:
                    // Departure: the unit is still standing on Pos, but it is now committed to
                    // StepTo and will be there at StepEnd. Nothing about position changes yet.
                    if (src != null)
                    {
                        src.StepTo = new Hex(e.Amount, e.Aux);
                        src.StepStart = e.Tick;
                        src.StepEnd = e.Tick + e.Aux2;
                    }
                    break;
                case EventKind.Move:
                    // Arrival — or a teleport, when no MoveStart preceded it. Either way the walk is
                    // over: collapsing Start onto End makes Walking false and StepTo fold back to Pos.
                    if (src != null)
                    {
                        src.Pos = new Hex(e.Amount, e.Aux);
                        src.StepStart = src.StepEnd = e.Tick;
                    }
                    break;
                case EventKind.DamageDealt:
                    if (tgt != null)
                    {
                        if (e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                        if (e.PostShield != BattleEvent.Unset) tgt.Shield = e.PostShield;
                    }
                    break;
                case EventKind.Heal:
                    if (tgt != null && e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                    break;
                case EventKind.ShieldChanged:
                    if (tgt != null && e.PostShield != BattleEvent.Unset) tgt.Shield = e.PostShield;
                    break;
                case EventKind.ManaChanged:
                    if (tgt != null && e.PostMana != BattleEvent.Unset) tgt.Mana = e.PostMana;
                    break;
                case EventKind.Cast:
                    if (src != null && e.PostMana != BattleEvent.Unset) src.Mana = e.PostMana;
                    break;
                case EventKind.StatusApplied:
                    if (tgt != null)
                    {
                        // Every kind but Burn stacks as independent instances, so an apply appends
                        // one. Burn is a SINGLE merged pool per unit (the Pyro dive law) whose
                        // magnitude only ever reaches a client as an event — apply REPLACES it, and
                        // the expiry below matches on kind alone since the Mag has moved on.
                        var kind = (StatusKind)e.Aux;
                        int expiry = e.Aux2 > 0 ? e.Tick + e.Aux2 : -1; // Aux2 = TicksLeft at apply
                        int at = -1;
                        if (kind == StatusKind.Burn)
                            for (int i = 0; i < tgt.Statuses.Count; i++)
                                if (tgt.Statuses[i].Kind == StatusKind.Burn) { at = i; break; }
                        if (at >= 0) tgt.Statuses[at] = (kind, e.Amount, expiry);
                        else tgt.Statuses.Add((kind, e.Amount, expiry));
                    }
                    break;
                case EventKind.StatusExpired:
                    if (tgt != null)
                        for (int i = 0; i < tgt.Statuses.Count; i++)
                            if (tgt.Statuses[i].Kind == (StatusKind)e.Aux
                                && (tgt.Statuses[i].Kind == StatusKind.Burn || tgt.Statuses[i].Mag == e.Amount))
                            {
                                tgt.Statuses.RemoveAt(i);
                                break;
                            }
                    break;
                case EventKind.Death:
                    if (tgt != null)
                    {
                        tgt.Dead = true;
                        tgt.StepStart = tgt.StepEnd = e.Tick; // a corpse never arrives (mirrors DeathPhase)
                        if (e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                    }
                    break;
                case EventKind.FieldCreated:
                    Fields.Add(new PlaybackField
                    {
                        Id = e.Target, IsWall = e.Amount == 1, Flavor = e.Flavor,
                        AttachedTo = e.Aux, Radius = e.Aux2,
                    });
                    break;
                case EventKind.FieldHex:
                    for (int i = 0; i < Fields.Count; i++)
                        if (Fields[i].Id == e.Target)
                        {
                            Fields[i].Hexes.Add(new Hex(e.Amount, e.Aux));
                            break;
                        }
                    break;
                case EventKind.FieldExpired:
                    for (int i = 0; i < Fields.Count; i++)
                        if (Fields[i].Id == e.Target)
                        {
                            Fields.RemoveAt(i);
                            break;
                        }
                    break;

                // A conditional passive's on/off state is persistent, so it belongs in the fold and
                // not only in the moment (HSBG's lesson: state that can be read at any time beats a
                // flash you had to be watching for). Scrubbing to tick N therefore reconstructs which
                // passives are live at tick N, exactly like HP or statuses.
                case EventKind.RuleChanged:
                    if (src == null) break;
                    if (e.Amount != 0)
                    {
                        if (!src.ActiveRules.Contains(e.Aux)) src.ActiveRules.Add(e.Aux);
                    }
                    else src.ActiveRules.Remove(e.Aux);
                    break;

                // TriggerFired mutates nothing — it is pure attribution, consumed by the renderer as
                // it is dispatched. Listed so the switch documents that the omission is deliberate.
                case EventKind.TriggerFired:
                    break;

                case EventKind.RuleProgress:
                    RuleCounters[e.Aux] = (e.Amount, e.Aux2);
                    break;
            }
        }

        public ulong ViewHash() => HashView(Units, Fields);

        /// <summary>FNV-1a over the VIEW contract: everything a renderer shows, nothing
        /// internal (field timers stay sim-side; expiries arrive as events). Statuses hashed as a
        /// (kind, mag)-sorted multiset — ExpiryTick rides in the view model for countdown rings
        /// but is NOT hashed: it is decoration, and hashing it would make a scrub past an expiry
        /// diverge from a sim that already dropped the entry. The per-unit
        /// stat/identity block is hashed too — it is shown, not internal, and covering it
        /// makes the replay round-trip check (live hash == reloaded hash) the proof that
        /// the wire format actually carries it.</summary>
        public static ulong HashView(List<PlaybackUnit> units, List<PlaybackField> fields)
        {
            ulong h = 14695981039346656037UL;
            void Mix(int v) { unchecked { h ^= (uint)v; h *= 1099511628211UL; } }
            void MixStr(string s) { foreach (char c in s ?? "") Mix(c); Mix(-1); } // -1 terminates: "ab","c" ≠ "a","bc"
            foreach (var u in units) // callers keep id order
            {
                Mix(u.Id); Mix(u.Team); Mix(u.Hp); Mix(u.Shield); Mix(u.Mana);
                Mix(u.Pos.Q); Mix(u.Pos.R); Mix(u.Dead ? 1 : 0);
                // The committed step is SHOWN (it is the walk on screen), so it is hashed. That makes
                // the guardrail prove MoveStart is emitted and folded correctly, not just Move.
                Mix(u.StepTo.Q); Mix(u.StepTo.R); Mix(u.StepStart); Mix(u.StepEnd);
                Mix(u.Range); Mix(u.Attack); Mix(u.AttackInterval); Mix(u.MoveInterval);
                Mix(u.CritChance); Mix(u.HealAutos ? 1 : 0);
                // The targeting rule and the rule span are SHOWN (the in-fight inspector reads
                // both), so they are hashed for the same reason the stat block is: it makes the
                // replay round-trip check the proof that the wire actually carries them.
                Mix((int)u.TargetPref); Mix(u.Standoff);
                Mix(u.TriggerRuleBase); Mix(u.TriggerRuleCount);
                Mix(u.StatRuleBase); Mix(u.StatRuleCount);
                MixStr(u.Name); MixStr(u.ChassisId); MixStr(u.WeaponName); Mix((int)u.WeaponTier);
                foreach (var t in u.Traits) MixStr(t); // merge order is meaningful (last override wins)
                var sorted = new List<(StatusKind Kind, int Mag, int ExpiryTick)>(u.Statuses);
                sorted.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Mag.CompareTo(b.Mag));
                foreach (var s in sorted) { Mix((int)s.Kind); Mix(s.Mag); }
            }
            foreach (var f in fields) // creation (= id) order on both sides
            {
                Mix(f.Id); Mix(f.IsWall ? 1 : 0); Mix((int)f.Flavor);
                if (f.AttachedTo >= 0) { Mix(f.AttachedTo); Mix(f.Radius); } // aura: anchor derives the footprint
                else foreach (var hex in f.Hexes) { Mix(hex.Q); Mix(hex.R); } // emission order
            }
            return h;
        }
    }
}
