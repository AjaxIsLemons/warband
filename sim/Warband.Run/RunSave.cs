using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>Thrown when a save cannot be trusted. The caller's only correct response is to
    /// discard the save and offer a new run — never to load a partially-parsed state.</summary>
    public sealed class RunSaveException : Exception
    {
        public RunSaveException(string message) : base(message) { }
    }

    /// <summary>
    /// Serialize a run to text and back (roadmap item 7). ADR 0008 made `RunState` a pure
    /// serializable ids-only record *precisely* so this would be cheap; nothing had ever used that
    /// property, so quitting the app destroyed the run — and losses are already terminal, so there
    /// was nothing to soften it.
    ///
    /// **This class does no file IO and never will** — the run layer is "pure: no Unity, no network,
    /// no filesystem, ever" (Warband.Run.csproj). It converts state ⇄ string; the host owns the
    /// bytes. That is also what keeps the save format headless-testable, which is the whole point:
    /// a save system nobody can test is a way to lose runs with extra steps.
    ///
    /// **Why hand-rolled rather than a JSON library.** Warband.Run has zero package references by
    /// law so the DLL drops into Unity unchanged; adding System.Text.Json or Newtonsoft would ship
    /// transitive assemblies into `Plugins/` and invite version conflicts. Reflection-based
    /// serialization is also out — IL2CPP strips it. So: explicit, boring, and total.
    ///
    /// **Format** — one `dotted.key=value` per line after a version header. Properties:
    /// order-independent · unknown keys ignored (a newer save loses fields rather than failing on a
    /// build that predates them) · absent keys take defaults · lists carry an explicit `.count`.
    /// Values are content ids, integers and enum names; <see cref="Safe"/> refuses at WRITE time to
    /// emit any string that could collide with a delimiter, so a future content id containing `=`
    /// or `|` fails loudly here instead of silently corrupting somebody's run.
    /// </summary>
    public static class RunSave
    {
        private const string Header = "warband-run-save v1";
        private const char ListSep = '|';

        // ---- write ------------------------------------------------------------------

        public static string Write(RunState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var b = new StringBuilder();
            b.Append(Header).Append('\n');

            Put(b, "seed", s.Seed.ToString(CultureInfo.InvariantCulture));
            // Provenance, checked by RunController.Resume — see RunState.ContentVersion for why an
            // id check is not enough. Safe() is deliberately not applied: this is a hex digest, and
            // if it ever stops being one the reserved-character guard should be extended, not skipped.
            Put(b, "contentVersion", s.ContentVersion);
            Put(b, "act", s.Act);
            Put(b, "node", s.NodeIndex);
            Put(b, "phase", s.Phase);
            Put(b, "gold", s.Gold);
            Put(b, "fieldSlots", s.FieldSlots);
            Put(b, "unlockedFieldSlots", s.UnlockedFieldSlots);
            Put(b, "slotsBought", s.SlotsBought);
            Put(b, "slotOfferPending", s.SlotOfferPending);
            Put(b, "justClosedBoss", s.JustClosedBoss);
            Put(b, "shopRolls", s.ShopRolls);
            Put(b, "nextItemInstanceId", s.NextItemInstanceId.ToString(CultureInfo.InvariantCulture));
            Put(b, "bossWins", s.BossWins);
            Put(b, "bossLosses", s.BossLosses);
            Put(b, "pendingBossSand", s.PendingBossSand);
            Put(b, "banners", Join(s.Banners));
            Put(b, "pendingBossRewards", Join(s.PendingBossRewards));

            // Act maps: one line per act, comma-joined beat kinds. The boss beat is implicit
            // (NodeIndex == NodesPerAct), so these are exactly the authored node kinds.
            Put(b, "actMaps.count", s.ActMaps.Length);
            for (int a = 0; a < s.ActMaps.Length; a++)
            {
                var kinds = new List<string>();
                foreach (var k in s.ActMaps[a]) kinds.Add(k.ToString());
                Put(b, $"actMaps.{a}", string.Join(",", kinds));
            }

            WriteHeroes(b, "field", s.Field);
            WriteHeroes(b, "bench", s.Bench);

            Put(b, "offers.count", s.ShopOffers.Count);
            for (int i = 0; i < s.ShopOffers.Count; i++)
            {
                var o = s.ShopOffers[i];
                Put(b, $"offers.{i}.present", o != null);
                if (o == null) continue;           // null = bought/empty slot, and that must survive
                Put(b, $"offers.{i}.kind", o.Kind);
                Put(b, $"offers.{i}.id", Safe(o.Id, "offer id"));
                Put(b, $"offers.{i}.price", o.Price);
                Put(b, $"offers.{i}.frozen", o.Frozen);
                Put(b, $"offers.{i}.tier", o.Tier);
            }

            Put(b, "inventory.count", s.Inventory.Count);
            for (int i = 0; i < s.Inventory.Count; i++)
            {
                var it = s.Inventory[i];
                Put(b, $"inventory.{i}.instanceId", it.InstanceId.ToString(CultureInfo.InvariantCulture));
                Put(b, $"inventory.{i}.kind", it.Kind);
                Put(b, $"inventory.{i}.id", Safe(it.Id, "item id"));
                Put(b, $"inventory.{i}.tier", it.Tier);
                Put(b, $"inventory.{i}.sandInvested", it.SandInvested);
            }

            Put(b, "pendingSpec.present", s.PendingSpec != null);
            if (s.PendingSpec != null)
            {
                var p = s.PendingSpec;
                Put(b, "pendingSpec.zone", p.Zone);
                Put(b, "pendingSpec.index", p.Index);
                Put(b, "pendingSpec.forRank", p.ForRank);
                Put(b, "pendingSpec.optionA", Safe(p.OptionA, "spec option"));
                Put(b, "pendingSpec.optionB", Safe(p.OptionB, "spec option"));
            }

            return b.ToString();
        }

        private static void WriteHeroes(StringBuilder b, string prefix, List<HeroInstance> heroes)
        {
            Put(b, $"{prefix}.count", heroes.Count);
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                string k = $"{prefix}.{i}";
                Put(b, $"{k}.chassis", Safe(h.ChassisId, "chassis id"));
                Put(b, $"{k}.rank", h.Rank);
                if (h.PathId != null) Put(b, $"{k}.path", Safe(h.PathId, "path id"));
                Put(b, $"{k}.goldSpent", h.GoldSpent);
                if (h.WeaponId != null) Put(b, $"{k}.weapon", Safe(h.WeaponId, "weapon id"));
                Put(b, $"{k}.weaponTier", h.WeaponTier);
                Put(b, $"{k}.weaponInstanceId", h.WeaponInstanceId.ToString(CultureInfo.InvariantCulture));
                Put(b, $"{k}.weaponSandInvested", h.WeaponSandInvested);
                Put(b, $"{k}.starterWeaponTier", h.StarterWeaponTier);
                Put(b, $"{k}.starterWeaponSandInvested", h.StarterWeaponSandInvested);
                Put(b, $"{k}.trinkets", Join(h.TrinketIds));
                Put(b, $"{k}.trinketInstanceId", h.TrinketInstanceId.ToString(CultureInfo.InvariantCulture));
                Put(b, $"{k}.trinketSandInvested", h.TrinketSandInvested);
                Put(b, $"{k}.spec", Join(h.SpecNodeIds));

                Put(b, $"{k}.bonuses.count", h.RunBonuses.Count);
                for (int j = 0; j < h.RunBonuses.Count; j++)
                {
                    var r = h.RunBonuses[j];
                    Put(b, $"{k}.bonuses.{j}", $"{r.Per},{r.Threshold},{r.Grant},{r.Mag}");
                }

                // Earned statuses are the run's accumulated growth — losing these silently would
                // make a resumed hero quietly weaker than the one the player put down.
                Put(b, $"{k}.earned.count", h.Earned.Count);
                for (int j = 0; j < h.Earned.Count; j++)
                {
                    var e = h.Earned[j];
                    Put(b, $"{k}.earned.{j}", $"{e.Kind},{e.Mag},{e.TicksLeft},{e.SwingsLeft},{e.SourceId}");
                }
            }
        }

        private static void Put(StringBuilder b, string key, string value) =>
            b.Append(key).Append('=').Append(value).Append('\n');
        private static void Put(StringBuilder b, string key, int value) =>
            Put(b, key, value.ToString(CultureInfo.InvariantCulture));
        private static void Put(StringBuilder b, string key, bool value) => Put(b, key, value ? "1" : "0");
        private static void Put(StringBuilder b, string key, Enum value) => Put(b, key, value.ToString());

        private static string Join(List<string> items)
        {
            for (int i = 0; i < items.Count; i++) Safe(items[i], "list entry");
            return string.Join(ListSep.ToString(), items);
        }

        /// <summary>Fail at write time, loudly, rather than corrupt a save. Content ids are
        /// lowercase dotted words today; this exists so that stops being an assumption.</summary>
        private static string Safe(string value, string what)
        {
            if (value == null) throw new RunSaveException($"{what} is null");
            if (value.Length == 0) throw new RunSaveException($"{what} is empty");
            foreach (char c in value)
                if (c == '=' || c == ListSep || c == ',' || c == '\n' || c == '\r')
                    throw new RunSaveException(
                        $"{what} '{value}' contains the reserved character '{c}' — the save format " +
                        "cannot represent it. Change the id or extend RunSave's escaping.");
            return value;
        }

        // ---- read -------------------------------------------------------------------

        public static RunState Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new RunSaveException("save is empty");
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines[0].Trim() != Header)
                throw new RunSaveException(
                    $"unsupported save format '{Truncate(lines[0])}' (this build reads '{Header}')");

            var kv = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) throw new RunSaveException($"malformed save line {i + 1}: '{Truncate(line)}'");
                kv[line.Substring(0, eq)] = line.Substring(eq + 1);
            }

            var s = new RunState
            {
                Seed = ULong(kv, "seed"),
                ContentVersion = Str(kv, "contentVersion"),
                Act = Int(kv, "act", 1),
                NodeIndex = Int(kv, "node"),
                Phase = Enum<RunPhase>(kv, "phase", RunPhase.Planning),
                Gold = Int(kv, "gold"),
                FieldSlots = Int(kv, "fieldSlots", 3),
                UnlockedFieldSlots = Int(kv, "unlockedFieldSlots", 3),
                SlotsBought = Int(kv, "slotsBought"),
                SlotOfferPending = Bool(kv, "slotOfferPending"),
                JustClosedBoss = Bool(kv, "justClosedBoss"),
                ShopRolls = Int(kv, "shopRolls"),
                NextItemInstanceId = Long(kv, "nextItemInstanceId", 1),
                BossWins = Int(kv, "bossWins"),
                BossLosses = Int(kv, "bossLosses"),
                PendingBossSand = Int(kv, "pendingBossSand"),
            };
            s.Banners.AddRange(Split(Str(kv, "banners")));
            s.PendingBossRewards.AddRange(Split(Str(kv, "pendingBossRewards")));

            int acts = Int(kv, "actMaps.count");
            var maps = new NodeKind[acts][];
            for (int a = 0; a < acts; a++)
            {
                string row = Str(kv, $"actMaps.{a}");
                var parts = row.Length == 0 ? new string[0] : row.Split(',');
                maps[a] = new NodeKind[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    maps[a][i] = ParseEnum<NodeKind>(parts[i], $"actMaps.{a}");
            }
            s.ActMaps = maps;

            ReadHeroes(kv, "field", s.Field);
            ReadHeroes(kv, "bench", s.Bench);

            int offers = Int(kv, "offers.count");
            for (int i = 0; i < offers; i++)
            {
                if (!Bool(kv, $"offers.{i}.present")) { s.ShopOffers.Add(null); continue; }
                s.ShopOffers.Add(new ShopOffer
                {
                    Kind = Enum<OfferKind>(kv, $"offers.{i}.kind", OfferKind.Weapon),
                    Id = Str(kv, $"offers.{i}.id"),
                    Price = Int(kv, $"offers.{i}.price"),
                    Frozen = Bool(kv, $"offers.{i}.frozen"),
                    Tier = Enum<WeaponTier>(kv, $"offers.{i}.tier", WeaponTier.Worn),
                });
            }

            int inv = Int(kv, "inventory.count");
            for (int i = 0; i < inv; i++)
                s.Inventory.Add(new ItemRef
                {
                    InstanceId = Long(kv, $"inventory.{i}.instanceId"),
                    Kind = Enum<ItemKind>(kv, $"inventory.{i}.kind", ItemKind.Weapon),
                    Id = Str(kv, $"inventory.{i}.id"),
                    Tier = Enum<WeaponTier>(kv, $"inventory.{i}.tier", WeaponTier.Worn),
                    SandInvested = Int(kv, $"inventory.{i}.sandInvested"),
                });

            if (Bool(kv, "pendingSpec.present"))
                s.PendingSpec = new PendingSpec
                {
                    Zone = Enum<RosterZone>(kv, "pendingSpec.zone", RosterZone.Field),
                    Index = Int(kv, "pendingSpec.index"),
                    ForRank = Enum<Rank>(kv, "pendingSpec.forRank", Rank.B),
                    OptionA = Str(kv, "pendingSpec.optionA"),
                    OptionB = Str(kv, "pendingSpec.optionB"),
                };

            Validate(s);
            return s;
        }

        private static void ReadHeroes(Dictionary<string, string> kv, string prefix, List<HeroInstance> into)
        {
            int n = Int(kv, $"{prefix}.count");
            for (int i = 0; i < n; i++)
            {
                string k = $"{prefix}.{i}";
                var h = new HeroInstance
                {
                    ChassisId = Str(kv, $"{k}.chassis"),
                    Rank = Enum<Rank>(kv, $"{k}.rank", Rank.C),
                    PathId = kv.TryGetValue($"{k}.path", out string path) ? path : null,
                    GoldSpent = Int(kv, $"{k}.goldSpent"),
                    WeaponId = kv.TryGetValue($"{k}.weapon", out string weapon) ? weapon : null,
                    WeaponTier = Enum<WeaponTier>(kv, $"{k}.weaponTier", WeaponTier.Worn),
                    WeaponInstanceId = Long(kv, $"{k}.weaponInstanceId"),
                    WeaponSandInvested = Int(kv, $"{k}.weaponSandInvested"),
                    StarterWeaponTier = Enum<WeaponTier>(kv, $"{k}.starterWeaponTier", WeaponTier.Worn),
                    StarterWeaponSandInvested = Int(kv, $"{k}.starterWeaponSandInvested"),
                    TrinketInstanceId = Long(kv, $"{k}.trinketInstanceId"),
                    TrinketSandInvested = Int(kv, $"{k}.trinketSandInvested"),
                };
                h.TrinketIds.AddRange(Split(Str(kv, $"{k}.trinkets")));
                h.SpecNodeIds.AddRange(Split(Str(kv, $"{k}.spec")));

                int bonuses = Int(kv, $"{k}.bonuses.count");
                for (int j = 0; j < bonuses; j++)
                {
                    var f = Fields(kv, $"{k}.bonuses.{j}", 4);
                    h.RunBonuses.Add(new RunBonus
                    {
                        Per = ParseEnum<GrowthMetric>(f[0], "bonus metric"),
                        Threshold = ParseInt(f[1], "bonus threshold"),
                        Grant = ParseEnum<StatusKind>(f[2], "bonus grant"),
                        Mag = ParseInt(f[3], "bonus mag"),
                    });
                }

                int earned = Int(kv, $"{k}.earned.count");
                for (int j = 0; j < earned; j++)
                {
                    var f = Fields(kv, $"{k}.earned.{j}", 5);
                    h.Earned.Add(new Status
                    {
                        Kind = ParseEnum<StatusKind>(f[0], "earned kind"),
                        Mag = ParseInt(f[1], "earned mag"),
                        TicksLeft = ParseInt(f[2], "earned ticks"),
                        SwingsLeft = ParseInt(f[3], "earned swings"),
                        SourceId = ParseInt(f[4], "earned source"),
                    });
                }
                into.Add(h);
            }
        }

        /// <summary>
        /// Structural sanity only — enough that a truncated or garbage file is rejected instead of
        /// loading as a bizarre run. Content-id validity is NOT checked here (the run layer has no
        /// catalog); that is <see cref="RunController.Resume"/>'s job.
        /// </summary>
        private static void Validate(RunState s)
        {
            if (s.Act < 1) throw new RunSaveException($"act {s.Act} is not a real act");
            if (s.NodeIndex < 0) throw new RunSaveException($"node index {s.NodeIndex} is negative");
            if (s.ActMaps.Length == 0) throw new RunSaveException("save has no act maps");
            if (s.Act > s.ActMaps.Length)
                throw new RunSaveException($"act {s.Act} is past the saved act maps ({s.ActMaps.Length})");
            if (s.Field.Count == 0) throw new RunSaveException("save has an empty warband");
            foreach (var h in s.Field)
                if (h.ChassisId.Length == 0) throw new RunSaveException("a fielded hero has no chassis");
        }

        // ---- typed getters ----------------------------------------------------------

        private static string Str(Dictionary<string, string> kv, string key) =>
            kv.TryGetValue(key, out string v) ? v : "";

        private static int Int(Dictionary<string, string> kv, string key, int fallback = 0) =>
            kv.TryGetValue(key, out string v) ? ParseInt(v, key) : fallback;

        private static long Long(Dictionary<string, string> kv, string key, long fallback = 0)
        {
            if (!kv.TryGetValue(key, out string v)) return fallback;
            if (!long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long r))
                throw new RunSaveException($"'{key}' is not a number: '{Truncate(v)}'");
            return r;
        }

        private static ulong ULong(Dictionary<string, string> kv, string key)
        {
            if (!kv.TryGetValue(key, out string v)) return 0;
            if (!ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong r))
                throw new RunSaveException($"'{key}' is not a number: '{Truncate(v)}'");
            return r;
        }

        private static bool Bool(Dictionary<string, string> kv, string key) =>
            kv.TryGetValue(key, out string v) && v == "1";

        private static T Enum<T>(Dictionary<string, string> kv, string key, T fallback) where T : struct
            => kv.TryGetValue(key, out string v) ? ParseEnum<T>(v, key) : fallback;

        private static T ParseEnum<T>(string value, string what) where T : struct
        {
            if (!System.Enum.TryParse<T>(value, out T r))
                throw new RunSaveException($"'{what}' is not a known {typeof(T).Name}: '{Truncate(value)}'");
            return r;
        }

        private static int ParseInt(string value, string what)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r))
                throw new RunSaveException($"'{what}' is not a number: '{Truncate(value)}'");
            return r;
        }

        private static string[] Fields(Dictionary<string, string> kv, string key, int expected)
        {
            var parts = Str(kv, key).Split(',');
            if (parts.Length != expected)
                throw new RunSaveException($"'{key}' should have {expected} fields, has {parts.Length}");
            return parts;
        }

        /// <summary>Empty string is an empty list — no content id is ever empty, so this is
        /// unambiguous and keeps a hero with no trinkets from resuming with one blank trinket.</summary>
        private static string[] Split(string joined) =>
            joined.Length == 0 ? new string[0] : joined.Split(ListSep);

        private static string Truncate(string v) => v.Length <= 40 ? v : v.Substring(0, 40) + "…";
    }
}
