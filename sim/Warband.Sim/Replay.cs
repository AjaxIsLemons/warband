using System.Collections.Generic;
using System.IO;

namespace Warband.Sim
{
    /// <summary>
    /// Golden-replay wire format: (initial PlaybackUnits, event log) — exactly the two
    /// things the render contract says the client consumes. Lives in Warband.Sim so the
    /// homeserv producer and the Unity consumer share ONE implementation (no drift). Pure
    /// binary, no dependencies — rides in the DLL the client already loads.
    /// </summary>
    public static class Replay
    {
        private const uint Magic = 0x57425250; // "WBRP"
        private const int Version = 10;  // v10: + PlaybackUnit.ManaPerSwing/CleavePct — immutable
                                         //      Weapon facts rendered by the shared live unit sheet
                                         // v9: + PlaybackUnit.RoleId — the authored enemy role. The
                                         //     board renders a monster as its ROLE (item 29); before
                                         //     this the only identity on the wire was ChassisId, a
                                         //     borrowed hero silhouette
                                         // v8: + per-rule owning team beside each rule id — the id
                                         //     alone cannot say whose law it is (mirror fights
                                         //     register the same Inscription on both sides), and
                                         //     the badge rail must show YOUR Hourstone (ADR 0026)
                                         // v7: + PlaybackUnit targeting rule (TargetPref/Standoff) and
                                         //     its span in the rule table — the in-fight inspector
                                         // v6: + the battle's rule-id table (TriggerFired/RuleChanged
                                         //     carry an index into it — passive legibility, item 20)
                                         // v5: + PlaybackUnit status ExpiryTick (countdown rings)
                                         // v4: + PlaybackUnit committed step (StepTo/StepStart/StepEnd)
                                         // v3: + PlaybackUnit stat/identity block (range, weapon, traits)
                                         // v2: + BattleEvent.Aux3 (FieldCreated carries FieldFlavor)

        public static void Write(Stream stream, IReadOnlyList<PlaybackUnit> initial,
                                 IReadOnlyList<BattleEvent> events, IReadOnlyList<string>? ruleIds = null,
                                 IReadOnlyList<int>? ruleTeams = null)
        {
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);

            // The rule-id table, before the units: TriggerFired/RuleChanged carry an int index into
            // it because BattleEvent is all ints by design. Null is written as an empty table, which
            // is the honest degradation — every index then resolves to "" and every passive falls
            // through to the generic tell instead of naming itself. The owning team rides beside
            // each id; a missing table degrades to team 0, "assume it is yours".
            w.Write(ruleIds?.Count ?? 0);
            if (ruleIds != null)
                for (int i = 0; i < ruleIds.Count; i++)
                {
                    w.Write(ruleIds[i] ?? "");
                    w.Write(ruleTeams != null && i < ruleTeams.Count ? ruleTeams[i] : 0);
                }

            w.Write(initial.Count);
            foreach (var u in initial)
            {
                w.Write(u.Id); w.Write(u.Team); w.Write(u.Name ?? "");
                w.Write(u.MaxHp); w.Write(u.Hp); w.Write(u.Shield);
                w.Write(u.Mana); w.Write(u.ManaMax);
                w.Write(u.Pos.Q); w.Write(u.Pos.R); w.Write(u.Dead);
                w.Write(u.StepTo.Q); w.Write(u.StepTo.R); w.Write(u.StepStart); w.Write(u.StepEnd);
                w.Write(u.Range); w.Write(u.Attack); w.Write(u.AttackInterval);
                w.Write(u.MoveInterval); w.Write(u.ManaPerSwing);
                w.Write(u.CritChance); w.Write(u.CleavePct); w.Write(u.HealAutos);
                w.Write((int)u.TargetPref); w.Write(u.Standoff);
                w.Write(u.TriggerRuleBase); w.Write(u.TriggerRuleCount);
                w.Write(u.StatRuleBase); w.Write(u.StatRuleCount);
                w.Write(u.ChassisId ?? ""); w.Write(u.WeaponName ?? ""); w.Write((int)u.WeaponTier);
                w.Write(u.RoleId ?? "");
                w.Write(u.Traits.Count);
                foreach (var t in u.Traits) w.Write(t ?? "");
                w.Write(u.Statuses.Count);
                foreach (var s in u.Statuses) { w.Write((int)s.Kind); w.Write(s.Mag); w.Write(s.ExpiryTick); }
            }

            w.Write(events.Count);
            foreach (var e in events)
            {
                w.Write(e.Tick); w.Write((int)e.Kind); w.Write(e.Source); w.Write(e.Target);
                w.Write(e.Amount); w.Write((int)e.Cause); w.Write(e.Depth); w.Write(e.Root);
                w.Write(e.Aux); w.Write(e.Aux2); w.Write(e.Aux3); w.Write(e.Crit);
                w.Write(e.PostHp); w.Write(e.PostShield); w.Write(e.PostMana);
            }
        }

        /// <summary>Read a replay, discarding the rule-id table. Kept so the dozen existing call
        /// sites that only want (units, events) stay untouched; anything that renders passives uses
        /// the overload below.</summary>
        public static (List<PlaybackUnit> Initial, List<BattleEvent> Events) Read(Stream stream) =>
            Read(stream, out _);

        public static (List<PlaybackUnit> Initial, List<BattleEvent> Events) Read(
            Stream stream, out List<string> ruleIds) => Read(stream, out ruleIds, out _);

        public static (List<PlaybackUnit> Initial, List<BattleEvent> Events) Read(
            Stream stream, out List<string> ruleIds, out List<int> ruleTeams)
        {
            using var r = new BinaryReader(stream);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a warband replay (bad magic).");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Unsupported replay version {version}.");

            int ruleCount = r.ReadInt32();
            ruleIds = new List<string>(ruleCount);
            ruleTeams = new List<int>(ruleCount);
            for (int i = 0; i < ruleCount; i++) { ruleIds.Add(r.ReadString()); ruleTeams.Add(r.ReadInt32()); }

            int unitCount = r.ReadInt32();
            var initial = new List<PlaybackUnit>(unitCount);
            for (int i = 0; i < unitCount; i++)
            {
                var u = new PlaybackUnit
                {
                    Id = r.ReadInt32(), Team = r.ReadInt32(), Name = r.ReadString(),
                    MaxHp = r.ReadInt32(), Hp = r.ReadInt32(), Shield = r.ReadInt32(),
                    Mana = r.ReadInt32(), ManaMax = r.ReadInt32(),
                    Pos = new Hex(r.ReadInt32(), r.ReadInt32()), Dead = r.ReadBoolean(),
                    // Initializer order IS the read order — StepTo's setter only stores; the
                    // standing-still fold-back is derived from StepStart/StepEnd assigned next.
                    StepTo = new Hex(r.ReadInt32(), r.ReadInt32()),
                    StepStart = r.ReadInt32(), StepEnd = r.ReadInt32(),
                    Range = r.ReadInt32(), Attack = r.ReadInt32(), AttackInterval = r.ReadInt32(),
                    MoveInterval = r.ReadInt32(), ManaPerSwing = r.ReadInt32(),
                    CritChance = r.ReadInt32(), CleavePct = r.ReadInt32(),
                    HealAutos = r.ReadBoolean(),
                    TargetPref = (TargetPref)r.ReadInt32(), Standoff = r.ReadInt32(),
                    TriggerRuleBase = r.ReadInt32(), TriggerRuleCount = r.ReadInt32(),
                    StatRuleBase = r.ReadInt32(), StatRuleCount = r.ReadInt32(),
                    ChassisId = r.ReadString(), WeaponName = r.ReadString(),
                    WeaponTier = (WeaponTier)r.ReadInt32(), RoleId = r.ReadString(),
                };
                int traitCount = r.ReadInt32();
                for (int t = 0; t < traitCount; t++)
                    u.Traits.Add(r.ReadString());
                int statusCount = r.ReadInt32();
                for (int s = 0; s < statusCount; s++)
                    u.Statuses.Add(((StatusKind)r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
                initial.Add(u);
            }

            int eventCount = r.ReadInt32();
            var events = new List<BattleEvent>(eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new BattleEvent
                {
                    Tick = r.ReadInt32(), Kind = (EventKind)r.ReadInt32(),
                    Source = r.ReadInt32(), Target = r.ReadInt32(), Amount = r.ReadInt32(),
                    Cause = (Cause)r.ReadInt32(), Depth = r.ReadInt32(), Root = r.ReadInt32(),
                    Aux = r.ReadInt32(), Aux2 = r.ReadInt32(), Aux3 = r.ReadInt32(), Crit = r.ReadBoolean(),
                    PostHp = r.ReadInt32(), PostShield = r.ReadInt32(), PostMana = r.ReadInt32(),
                });
            }
            return (initial, events);
        }
    }
}
