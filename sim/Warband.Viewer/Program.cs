using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warband.Sim;

namespace Warband.Viewer
{
    /// <summary>
    /// Terminal battle viewer — renders EXCLUSIVELY from the PlaybackState fold
    /// (render-contract.md: the fold IS the view-model; this proves it drives a UI).
    /// Usage: dotnet run [--every N]  (snapshot cadence in ticks, default auto ~12 frames)
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            int every = 0;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--every")
                    every = int.Parse(args[i + 1]);

            var (units, names) = SampleArmies();
            var result = new Battle(units).Run();
            if (every <= 0) every = Math.Max(1, result.EndTick / 12);

            Console.WriteLine($"=== WARBAND — sample skirmish (log: {result.Events.Count} events) ===");
            var fold = PlaybackState.From(result.InitialUnits);
            int nextEvent = 0;

            for (int t = 0; t < result.EndTick; t += every)
            {
                fold.AdvanceToTick(result.Events, t);
                var notable = CollectNotable(result.Events, ref nextEvent, t, names);
                Console.WriteLine();
                Console.WriteLine($"--- tick {t} ({t / 10.0:0.0}s) ---");
                foreach (var line in notable)
                    Console.WriteLine($"  » {line}");
                Console.Write(RenderBoard(fold));
                Console.Write(RenderRoster(fold));
            }

            Console.WriteLine();
            Console.WriteLine($"=== RESULT: {result.Winner} at tick {result.EndTick} ({result.EndTick / 10.0:0.0}s) ===");
        }

        private static string RenderBoard(PlaybackState fold)
        {
            var fieldHexes = new HashSet<Hex>();
            var wallHexes = new HashSet<Hex>();
            foreach (var f in fold.Fields)
            {
                IEnumerable<Hex> hexes = f.AttachedTo >= 0
                    ? (fold.ById(f.AttachedTo) is { Dead: false } anchor ? Hex.Range(anchor.Pos, f.Radius) : new List<Hex>())
                    : f.Hexes;
                foreach (var h in hexes)
                    (f.IsWall ? wallHexes : fieldHexes).Add(h);
            }

            var sb = new StringBuilder();
            for (int row = 7; row >= 0; row--)   // enemy half on top
            {
                sb.Append(row % 2 == 1 ? " " : "").Append(row).Append("  ");
                for (int col = 0; col < 6; col++)
                {
                    var hex = Hex.FromRowCol(row, col);
                    var here = fold.Units.FirstOrDefault(u => !u.Dead && u.Pos == hex);
                    string cell = here != null ? Glyph(here)
                        : wallHexes.Contains(hex) ? "#"
                        : fieldHexes.Contains(hex) ? "~"
                        : "·";
                    sb.Append(cell).Append(' ');
                }
                sb.AppendLine();
            }
            sb.AppendLine("    0 1 2 3 4 5   (A-F yours, a-f ghost, ~ field, # wall)");
            return sb.ToString();
        }

        private static string Glyph(PlaybackUnit u) =>
            ((char)((u.Team == 0 ? 'A' : 'a') + u.Id % 6)).ToString();

        private static string RenderRoster(PlaybackState fold)
        {
            var sb = new StringBuilder();
            foreach (var u in fold.Units)
            {
                string bar = Bar(u.Hp, u.MaxHp, 10);
                string mana = u.ManaMax > 0 ? $" mp {u.Mana,2}/{u.ManaMax}" : "";
                string shield = u.Shield > 0 ? $" +{u.Shield}sh" : "";
                string statuses = u.Statuses.Count > 0
                    ? " [" + string.Join(" ", u.Statuses.GroupBy(s => s.Kind).Select(g => $"{g.Key}x{g.Count()}")) + "]"
                    : "";
                string state = u.Dead ? "DEAD".PadRight(12) : $"{bar} {Math.Max(0, u.Hp),3}";
                sb.AppendLine($"  {Glyph(u)} {u.Name,-10} {state}{shield}{mana}{statuses}");
            }
            return sb.ToString();
        }

        private static string Bar(int value, int max, int width)
        {
            int filled = max <= 0 ? 0 : Math.Clamp(value * width / max, 0, width);
            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }

        private static List<string> CollectNotable(List<BattleEvent> events, ref int cursor, int upToTick, Dictionary<int, string> names)
        {
            var lines = new List<string>();
            for (; cursor < events.Count && events[cursor].Tick <= upToTick; cursor++)
            {
                var e = events[cursor];
                string N(int id) => id >= 0 && names.TryGetValue(id, out var n) ? n : "?";
                switch (e.Kind)
                {
                    case EventKind.Cast:
                        lines.Add($"t{e.Tick}: {N(e.Source)} CASTS");
                        break;
                    case EventKind.Death:
                        lines.Add($"t{e.Tick}: {N(e.Target)} DIES");
                        break;
                    case EventKind.StatusApplied:
                        lines.Add($"t{e.Tick}: {N(e.Target)} gains {(StatusKind)e.Aux} {e.Amount}");
                        break;
                    case EventKind.StatusExpired:
                        lines.Add($"t{e.Tick}: {(StatusKind)e.Aux} fades from {N(e.Target)}");
                        break;
                }
            }
            if (lines.Count > 6)
            {
                int dropped = lines.Count - 6;
                lines = lines.Skip(dropped).ToList();
                lines.Insert(0, $"(+{dropped} earlier)");
            }
            return lines;
        }

        private static (List<UnitState>, Dictionary<int, string>) SampleArmies()
        {
            UnitDef Def(string name, int hp, int atk, int range = 1, int atkInt = 10, int move = 5) =>
                new UnitDef { Name = name, MaxHp = hp, Attack = atk, Range = range, AttackInterval = atkInt, MoveInterval = move };

            var bulwark = Def("Bulwark", 240, 8);
            bulwark.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 80, Select = new Selector { Kind = SelKind.Self } } },
            });

            var berserker = Def("Berserker", 150, 7);
            berserker.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.AttackUp, Amount = 2, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } } },
            });

            var pyro = Def("Pyromancer", 90, 4, range: 3);
            pyro.ManaMax = 25;
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 28 });
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Dot, Amount = 4, StatusTicks = 100 });

            var cleric = Def("Cleric", 120, 5, range: 3);
            cleric.ManaMax = 20;
            cleric.Signature.Add(new EffectDef { Kind = EffectKind.Heal, Amount = 30, Select = new Selector { Kind = SelKind.LowestHpAlly } });

            var phalanx = Def("Phalanx", 170, 9, range: 2);
            phalanx.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When = { new Cond { Kind = CondKind.TargetIsOwner }, new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack } },
                Do = { new EffectDef { Kind = EffectKind.Damage, Amount = 5, Select = new Selector { Kind = SelKind.EventSource } } },
            });

            var banneret = Def("Banneret", 130, 5);
            banneret.ManaMax = 30;
            banneret.Signature.Add(new EffectDef
            {
                Kind = EffectKind.GrantMana, Amount = 10,
                Select = new Selector { Kind = SelKind.AlliesWithin, Range = 1, ExcludeSelf = true },
            });
            banneret.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.CreateField,
                    Select = new Selector { Kind = SelKind.Self },
                    Field = new FieldDef
                    {
                        AttachToOwner = true, Radius = 1, Ticks = -1,
                        Presence = { (StatusKind.Haste, 200) }, PresenceAffects = Affects.Allies,
                    },
                } },
            });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, bulwark, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, berserker, Hex.FromRowCol(3, 3)),
                UnitState.Spawn(2, 0, pyro, Hex.FromRowCol(1, 3)),
                UnitState.Spawn(3, 1, phalanx, Hex.FromRowCol(4, 2)),
                UnitState.Spawn(4, 1, cleric, Hex.FromRowCol(6, 2)),
                UnitState.Spawn(5, 1, banneret, Hex.FromRowCol(5, 3)),
            };
            var names = units.ToDictionary(u => u.Id, u => u.Def.Name);
            return (units, names);
        }
    }
}
