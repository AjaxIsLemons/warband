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
        private const int Version = 1;

        public static void Write(Stream stream, IReadOnlyList<PlaybackUnit> initial, IReadOnlyList<BattleEvent> events)
        {
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);

            w.Write(initial.Count);
            foreach (var u in initial)
            {
                w.Write(u.Id); w.Write(u.Team); w.Write(u.Name ?? "");
                w.Write(u.MaxHp); w.Write(u.Hp); w.Write(u.Shield);
                w.Write(u.Mana); w.Write(u.ManaMax);
                w.Write(u.Pos.Q); w.Write(u.Pos.R); w.Write(u.Dead);
                w.Write(u.Statuses.Count);
                foreach (var s in u.Statuses) { w.Write((int)s.Kind); w.Write(s.Mag); }
            }

            w.Write(events.Count);
            foreach (var e in events)
            {
                w.Write(e.Tick); w.Write((int)e.Kind); w.Write(e.Source); w.Write(e.Target);
                w.Write(e.Amount); w.Write((int)e.Cause); w.Write(e.Depth); w.Write(e.Root);
                w.Write(e.Aux); w.Write(e.Aux2); w.Write(e.Crit);
                w.Write(e.PostHp); w.Write(e.PostShield); w.Write(e.PostMana);
            }
        }

        public static (List<PlaybackUnit> Initial, List<BattleEvent> Events) Read(Stream stream)
        {
            using var r = new BinaryReader(stream);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a warband replay (bad magic).");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Unsupported replay version {version}.");

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
                };
                int statusCount = r.ReadInt32();
                for (int s = 0; s < statusCount; s++)
                    u.Statuses.Add(((StatusKind)r.ReadInt32(), r.ReadInt32()));
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
                    Aux = r.ReadInt32(), Aux2 = r.ReadInt32(), Crit = r.ReadBoolean(),
                    PostHp = r.ReadInt32(), PostShield = r.ReadInt32(), PostMana = r.ReadInt32(),
                });
            }
            return (initial, events);
        }
    }
}
