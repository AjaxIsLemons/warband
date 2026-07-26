using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

namespace Warband.Viewer
{
    /// <summary>
    /// Data-driven replay authoring. A <c>scenarios.json</c> describes fights as data — each
    /// unit is a REAL <see cref="Catalog"/> build (chassis + path picks + weapon/tier/mastery +
    /// rank + optional banners), not a hand-tuned capsule. One file → a whole set of diverse
    /// replays for the render layer to tune against (the "contact-sheet" backbone). The builder
    /// reuses the exact compose→spawn→Battle path the sweep harness proved, so scenario replays
    /// are as real as a live run.
    /// </summary>
    public sealed class ScenarioFile
    {
        public List<ScenarioDef> scenarios { get; set; } = new List<ScenarioDef>();
    }

    public sealed class ScenarioDef
    {
        public string name { get; set; } = "scenario";
        public ulong seed { get; set; } = 1;
        /// <summary>Optional per-team banners, keyed by team index as a string ("0"/"1").</summary>
        public Dictionary<string, List<string>>? banners { get; set; }
        /// <summary>Optional static wall hexes placed before the fight (impassable + block projectile
        /// paths). This is the ONLY way to author an IsWall field — no kit creates one — so it exists
        /// for render fixtures like the blocked-shot tell, not as content. Absent → today's behavior.</summary>
        public List<WallSpec>? walls { get; set; }
        /// <summary>
        /// Optional authored encounter id (`Encounters.ById`) staged as team 1 — "waning-crown",
        /// "ashfall-battery", "the-long-range", … Its bodies are added AFTER the listed units, so
        /// `units` carries the player line and the encounter carries the opposition exactly as the
        /// run layer would field it. Absent → today's behavior.
        /// </summary>
        public string? encounter { get; set; }
        /// <summary>Act for an act-parameterized node encounter (bosses ignore it). Default 3.</summary>
        public int? encounterAct { get; set; }
        public List<UnitSpec> units { get; set; } = new List<UnitSpec>();
    }

    public sealed class WallSpec
    {
        public int row { get; set; }
        public int col { get; set; }
    }

    public sealed class UnitSpec
    {
        public int team { get; set; }
        public string chassis { get; set; } = "";
        /// <summary>Path picks at ranks B/A/S (0 or 1 each). Defaults to [0,0,0].</summary>
        public int[]? picks { get; set; }
        public string? weapon { get; set; }   // null → chassis starter
        public string? trinket { get; set; }
        public string? tier { get; set; }     // Worn | Honed | Relic (default Honed)
        public bool? mastered { get; set; }    // default true
        public int? rank { get; set; }         // rankSteps, default 3
        public int row { get; set; }
        public int col { get; set; }
    }

    public static class Scenarios
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static ScenarioFile Load(string path)
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ScenarioFile>(json, JsonOpts)
                       ?? throw new InvalidDataException($"Empty or invalid scenarios file: {path}");
            return file;
        }

        /// <summary>Compose every unit from the catalog, apply banners as team triggers, run the
        /// battle. Throws with a scenario-scoped message on any bad id so authoring errors are legible.</summary>
        public static BattleResult Build(ScenarioDef s, Catalog cat)
        {
            var units = new List<UnitState>();
            int id = 0;
            foreach (var spec in s.units)
            {
                try
                {
                    units.Add(BuildUnit(id++, spec, cat));
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"scenario '{s.name}', unit #{id - 1} ({spec.chassis}): {ex.Message}", ex);
                }
            }

            if (!string.IsNullOrEmpty(s.encounter))
            {
                var encounter = Encounters.ById(s.encounter!, s.encounterAct ?? 3)
                    ?? throw new InvalidDataException(
                        $"scenario '{s.name}': unknown encounter id '{s.encounter}'");
                foreach (var e in encounter.Enemies)
                    units.Add(UnitState.Spawn(id++, 1, e.Def, e.Pos));
            }

            var teamTriggers = new List<(int Team, Trigger T)>();
            if (s.banners != null)
                foreach (var kv in s.banners)
                {
                    if (!int.TryParse(kv.Key, out int team))
                        throw new InvalidDataException($"scenario '{s.name}': banner team key '{kv.Key}' is not an integer");
                    foreach (var bannerId in kv.Value)
                    {
                        if (!Catalog.Banners.TryGetValue(bannerId, out var banner))
                            throw new InvalidDataException($"scenario '{s.name}': unknown banner '{bannerId}'");
                        foreach (var trig in banner.TeamTriggers)
                            teamTriggers.Add((team, trig));
                    }
                }

            // Static walls → the Battle's existing initialFields param. Permanent (Ticks -1), zero
            // radius, neutral owner — the wall FieldDef shape FieldTests proves round-trips through
            // FieldCreated into the fold. null when absent, so a wall-less scenario is byte-identical.
            List<(FieldDef Def, Hex Center, int OwnerTeam)>? initialFields = null;
            if (s.walls != null && s.walls.Count > 0)
            {
                initialFields = new List<(FieldDef, Hex, int)>();
                foreach (var w in s.walls)
                {
                    var pos = Hex.FromRowCol(w.row, w.col);
                    if (!Battle.InBounds(pos))
                        throw new InvalidDataException($"scenario '{s.name}': wall row {w.row}, col {w.col} is off the {Battle.BoardRows}x{Battle.BoardCols} board");
                    initialFields.Add((new FieldDef { Radius = 0, Ticks = -1, IsWall = true }, pos, -1));
                }
            }

            return new Battle(units, teamTriggers, initialFields, seed: s.seed).Run();
        }

        private static UnitState BuildUnit(int id, UnitSpec spec, Catalog cat)
        {
            var chassis = cat.Chassis(spec.chassis); // throws KeyNotFound with the bad id if wrong

            var picks = spec.picks ?? new[] { 0, 0, 0 };
            if (picks.Length != 3)
                throw new InvalidDataException($"'picks' must have exactly 3 entries (B,A,S), got {picks.Length}");

            var fork = cat.ForkRank(spec.chassis);
            string? path = null;
            var nodes = new List<SpecNode>();
            var ranks = new[] { Rank.B, Rank.A, Rank.S };
            for (int i = 0; i < 3; i++)
            {
                var (a, b) = cat.SpecOptions(spec.chassis, ranks[i], path);
                string chosen = picks[i] == 0 ? a : b;
                nodes.Add(cat.Node(chosen));
                if (ranks[i] == fork) path = chosen;
            }

            var weapon = spec.weapon != null ? cat.Weapon(spec.weapon) : null;
            var trinkets = spec.trinket != null ? new[] { cat.Trinket(spec.trinket) } : null;
            var tier = ParseTier(spec.tier);

            var loadout = Loadout.Compose(
                chassis, weapon, trinkets, nodes,
                tier: tier,
                mastered: spec.mastered ?? true,
                rankSteps: spec.rank ?? 3);

            var pos = Hex.FromRowCol(spec.row, spec.col);
            if (!Battle.InBounds(pos))
                throw new InvalidDataException($"row {spec.row}, col {spec.col} is off the {Battle.BoardRows}x{Battle.BoardCols} board");
            return Loadout.Spawn(id, spec.team, loadout, pos);
        }

        private static WeaponTier ParseTier(string? tier)
        {
            if (string.IsNullOrEmpty(tier)) return WeaponTier.Honed;
            if (Enum.TryParse<WeaponTier>(tier, ignoreCase: true, out var t)) return t;
            throw new InvalidDataException($"unknown tier '{tier}' (expected Worn|Honed|Relic)");
        }
    }
}
