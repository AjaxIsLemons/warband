using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// Item 19: the run's decision trail as JSON Lines — every fight, every purchase, every tier
    /// selection (Jake, 2026-07-28) — so playtest #1 yields data instead of anecdote. Every
    /// instrument before this measured a default-policy bot; these lines are the first record of
    /// what a HUMAN does with the two levers the bots never pull (placement and purchases).
    ///
    /// This class FORMATS lines and does no IO, same law as <see cref="RunSave"/>: the run layer
    /// is pure, the host owns the bytes (append beside run.save, upload at run end). JSON is
    /// hand-rolled for the same reason RunSave's format is — zero package references by law.
    ///
    /// Every line carries (run, seed, contentVersion via the run id) + act/node/sand, so any
    /// surprising fight in a friend's log is re-simulable: replay = re-simulation from
    /// (seed, snapshots, contentVersion). The schema is append-only — add fields, never rename —
    /// and `v` bumps only on a breaking change.
    /// </summary>
    public sealed class RunTelemetry
    {
        public const int SchemaVersion = 1;

        private readonly string _runId;
        private readonly string _app;

        /// <summary>Run id = seed + content fingerprint prefix: stable across save/resume (state
        /// carries both), unique enough per install, and derivable from any line's own fields.</summary>
        public RunTelemetry(RunState state, string app = "")
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string version = state.ContentVersion ?? "";
            _runId = state.Seed.ToString("x16", CultureInfo.InvariantCulture) + "-" +
                     (version.Length >= 8 ? version.Substring(0, 8) : version);
            _app = app ?? "";
        }

        public string RunId => _runId;

        public string StartLine(RunState s, DateTime utc)
        {
            var line = Header(s, utc, "start")
                .Str("seed", s.Seed.ToString("x16", CultureInfo.InvariantCulture))
                .Str("content", s.ContentVersion ?? "");
            if (_app.Length > 0) line.Str("app", _app);
            line.Raw("party", PartyJson(s.Field));
            return line.Close();
        }

        /// <summary>One line per resolved fight — and because the tier is chosen AT the wager that
        /// resolves it, this line IS the tier-selection record too.</summary>
        public string FightLine(
            RunState s, DateTime utc, NodeKind kind, FightTier tier, string encounter,
            FightOutcome outcome, FightSummary summary)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            var line = Header(s, utc, "fight")
                .Str("kind", kind.ToString())
                .Str("tier", tier.ToString())
                .Str("encounter", encounter ?? "")
                .Bool("won", outcome.Won)
                .Num("killed", outcome.EnemiesKilled)
                .Num("enemies", outcome.EnemyCount)
                .Num("sandEarned", outcome.SandEarned);
            if (summary != null)
            {
                line.Num("ticks", summary.EndTick);
                var units = new StringBuilder(128);
                units.Append('[');
                bool first = true;
                foreach (UnitSummary u in summary.Units)
                {
                    if (u.Team != 0) continue;   // player side; the enemy comp is derivable from seed
                    if (!first) units.Append(',');
                    first = false;
                    units.Append(new JsonLine()
                        .Str("id", u.ChassisId)
                        .Num("dmg", u.DamageDealt)
                        .Num("pct", Math.Round(u.DamagePctOfTeam, 1))
                        .Num("healed", u.HealingDone)
                        .Bool("died", u.Died)
                        .Close());
                }
                units.Append(']');
                line.Raw("units", units.ToString());
            }
            line.Raw("party", PartyJson(s.Field));
            return line.Close();
        }

        public string PurchaseLine(RunState s, DateTime utc, PurchaseResult p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var line = Header(s, utc, "buy")
                .Str("offer", p.OfferKind.ToString())
                .Str("id", p.ContentId ?? "")
                .Str("outcome", p.Outcome.ToString())
                .Num("cost", p.SandSpent);
            if (p.NewRank.HasValue) line.Str("rank", p.NewRank.Value.ToString());
            return line.Close();
        }

        public string RerollLine(RunState s, DateTime utc, int cost) =>
            Header(s, utc, "reroll").Num("cost", cost).Close();

        public string SlotLine(RunState s, DateTime utc, int cost) =>
            Header(s, utc, "slot").Num("cost", cost).Close();

        public string ReforgeLine(RunState s, DateTime utc, ReforgeResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            return Header(s, utc, "reforge")
                .Str("id", r.WeaponId ?? "")
                .Str("from", r.PreviousTier.ToString())
                .Str("to", r.NewTier.ToString())
                .Num("cost", r.SandSpent)
                .Close();
        }

        public string SellLine(RunState s, DateTime utc, string what, string id) =>
            Header(s, utc, "sell").Str("what", what ?? "").Str("id", id ?? "").Close();

        public string InterludeLine(
            RunState s, DateTime utc, InterludePath path, int option, string rewardId) =>
            Header(s, utc, "interlude")
                .Str("path", path.ToString())
                .Num("option", option)
                .Str("reward", rewardId ?? "")
                .Close();

        public string BossRewardLine(RunState s, DateTime utc, int option, string id) =>
            Header(s, utc, "bossReward").Num("option", option).Str("id", id ?? "").Close();

        public string EndLine(RunState s, DateTime utc)
        {
            var line = Header(s, utc, s.Victory ? "victory" : "defeat")
                .Num("bossWins", s.BossWins)
                .Num("bossLosses", s.BossLosses);
            line.Raw("party", PartyJson(s.Field));
            return line.Close();
        }

        private JsonLine Header(RunState s, DateTime utc, string type)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return new JsonLine()
                .Num("v", SchemaVersion)
                .Str("run", _runId)
                .Str("t", type)
                .Str("utc", utc.ToUniversalTime().ToString(
                    "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                .Num("act", s.Act)
                .Num("node", s.NodeIndex)
                .Num("sand", s.Sand);
        }

        private static string PartyJson(IReadOnlyList<HeroInstance> field)
        {
            var sb = new StringBuilder(64);
            sb.Append('[');
            for (int i = 0; i < (field?.Count ?? 0); i++)
            {
                if (i > 0) sb.Append(',');
                HeroInstance h = field[i];
                var entry = new JsonLine().Str("id", h.ChassisId).Str("rank", h.Rank.ToString());
                if (!string.IsNullOrEmpty(h.PathId)) entry.Str("path", h.PathId);
                sb.Append(entry.Close());
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>Minimal JSON object builder — explicit, boring, and total, like RunSave's writer.
    /// Escapes everything JSON requires; non-ASCII passes through (the file is UTF-8).</summary>
    internal sealed class JsonLine
    {
        private readonly StringBuilder _sb = new StringBuilder(192);
        private bool _first = true;
        private bool _closed;

        public JsonLine() { _sb.Append('{'); }

        public JsonLine Str(string key, string value)
        {
            Key(key);
            _sb.Append('"').Append(Escape(value ?? "")).Append('"');
            return this;
        }

        public JsonLine Num(string key, long value)
        {
            Key(key);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLine Num(string key, double value)
        {
            Key(key);
            _sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLine Bool(string key, bool value)
        {
            Key(key);
            _sb.Append(value ? "true" : "false");
            return this;
        }

        /// <summary>Append pre-built JSON (an array of objects). The caller vouches for validity.</summary>
        public JsonLine Raw(string key, string json)
        {
            Key(key);
            _sb.Append(json);
            return this;
        }

        public string Close()
        {
            if (_closed) throw new InvalidOperationException("JsonLine closed twice");
            _closed = true;
            _sb.Append('}');
            return _sb.ToString();
        }

        private void Key(string key)
        {
            if (_closed) throw new InvalidOperationException("JsonLine used after Close");
            if (!_first) _sb.Append(',');
            _first = false;
            _sb.Append('"').Append(Escape(key)).Append("\":");
        }

        private static string Escape(string s)
        {
            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                string replacement = null;
                if (c == '"') replacement = "\\\"";
                else if (c == '\\') replacement = "\\\\";
                else if (c == '\n') replacement = "\\n";
                else if (c == '\r') replacement = "\\r";
                else if (c == '\t') replacement = "\\t";
                else if (c < 0x20)
                    replacement = "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture);
                if (replacement != null)
                {
                    if (sb == null) { sb = new StringBuilder(s.Length + 8); sb.Append(s, 0, i); }
                    sb.Append(replacement);
                }
                else sb?.Append(c);
            }
            return sb == null ? s : sb.ToString();
        }
    }
}
