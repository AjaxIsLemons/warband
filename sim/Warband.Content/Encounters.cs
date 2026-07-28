using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// One authored PvE setup plus the exact information the preview owes the player.
    /// Calling an encounter factory returns fresh mutable defs; callers may safely tune or
    /// compose a fight without mutating the catalog for later attempts.
    /// </summary>
    public sealed class EncounterDef
    {
        public string Id = "";
        public string Name = "";
        public string Pressure = "";
        public string RuleName = "";
        public string RuleText = "";
        public List<EncounterUnit> Enemies = new List<EncounterUnit>();
    }

    public sealed class EncounterUnit
    {
        public UnitDef Def = null!;
        public Hex Pos;
        /// <summary>Stable role id from <see cref="Enemies"/> — drives the preview's eyebrow and
        /// accent. Free-text role blurbs are display, this is the key.</summary>
        public string RoleId = "";
        /// <summary>What this body is doing in THIS encounter, when the bare role label undersells
        /// it ("Warded wall" vs plain ANCHOR). Empty = use the role's own label.</summary>
        public string Role = "";
    }

    /// <summary>
    /// What the Bond actually did in one resolved fight. The point of the encounter is the
    /// question "which threat are you willing to leave enraged" — so the Bond is only real if
    /// it FIRES before the fight ends and the survivor then lives long enough to spend the
    /// speed. A readout where Enrage never fires, or fires with two ticks left, means the
    /// relationship is decoration and the encounter poses no decision.
    /// </summary>
    public readonly struct BondReadout
    {
        public readonly bool EnrageFired;
        public readonly int EnrageTick;          // -1 when it never fired
        public readonly int SurvivorId;          // -1 when it never fired
        public readonly int TicksAfterEnrage;    // fight length remaining once the survivor turned
        public readonly int SurvivorSwingsAfterEnrage; // what the speed was actually converted into
        public readonly int EnemyDeaths;

        public BondReadout(bool fired, int tick, int survivorId, int ticksAfter, int swingsAfter, int enemyDeaths)
        {
            EnrageFired = fired; EnrageTick = tick; SurvivorId = survivorId;
            TicksAfterEnrage = ticksAfter; SurvivorSwingsAfterEnrage = swingsAfter;
            EnemyDeaths = enemyDeaths;
        }
    }

    /// <summary>Small authored proofs. Grow this only after each relationship earns its keep in play.</summary>
    public static class Encounters
    {
        public const int BondHaste = 1000;

        /// <summary>
        /// Every authored node encounter (roadmap item 2). Each is built from <see cref="Enemies"/>
        /// roles and each poses a DIFFERENT problem — that is the whole point, since every normal
        /// fight used to be random hero kits with a stat multiplier and deployment therefore never
        /// mattered. This list is the CATALOGUE, not a pool: what an act may draw is
        /// <see cref="PoolFor"/>.
        ///
        /// Ordered, never shuffled in place: selection is by salted rng in the catalog, so the
        /// preview and the spawn resolve to the same encounter (RunController.PreviewEnemies).
        /// </summary>
        public static readonly System.Func<int, EncounterDef>[] NodePool =
        {
            GnawingHour, TheLongRange, NinthBell, TheDrop, Slagworks, LongProcession,
        };

        /// <summary>
        /// The pool an act may draw from — act-scoped since 2026-07-26 (roadmap item 14).
        ///
        /// Acts 2 and 3 used to draw an IDENTICAL pool, so a three-act run was one act played three
        /// times at rising numbers, and theme.md's "acts are eras" plus pve-encounters.md's "an
        /// act's pool is its identity" were both simply untrue in the build. Worse, ADR 0024 gave
        /// each act a different boss while the law above it says **the boss rules the act** —
        /// normal encounters exist to introduce, combine and stress the pieces its boss recombines.
        /// A shared pool cannot do that for three different exams.
        ///
        /// So each pool is now built backwards from its boss:
        ///
        /// | act | boss | its exam | what the act introduces |
        /// |---|---|---|---|
        /// | 1 | The Last Oath | which threat you leave enraged | Gnawing Hour (target economy) · Ninth Bell (a clock) · The Drop (placement) |
        /// | 2 | The Ashfall Battery | reach the gun behind the wall | The Long Range — kill order past a ward |
        /// | 3 | The Waning Crown | your own kills ring the bell | Long Procession (your kills wind a clock) · Slagworks (the lane) |
        ///
        /// **Acts 2 and 3 are now disjoint** — the defect this item names, gone at the root rather
        /// than softened. Act 1 deliberately owns no encounter of its own: its job is to teach
        /// pieces cleanly that the later acts recombine, so all three of its fights recurring later
        /// is the through-line working. The Ninth Bell in particular teaches a clock in act 1 and
        /// act 3's boss IS a clock.
        ///
        /// **Act 1 is deliberately untouched.** It is already where the naive bot line dies most
        /// (4/12 runs at act 1 node 0), so this item's job was to differentiate 2 from 3, not to
        /// make the opening harder.
        ///
        /// **What this does NOT do is make the pool harder**, and that was tried. Every composition
        /// that made a new encounter measure non-flat against the four answer axes (a third wall in
        /// the Slagworks; a nine-body Procession court; act 2 drawing only wall fights) also drove
        /// the naive line from 3/12 completed runs to 0/12 and tripped `FullRunsCompleteOnRealContent`.
        /// The gap between a competent party and the weakest legal comp is currently wider than the
        /// band an encounter can sit in. That is a balance finding, not a composition one, and the
        /// content doctrine parks it until playtest #1 — so these compositions are sized to sit
        /// beside the existing pool rather than to beat a metric.
        ///
        /// The Long Range stays out of act 1 ON PURPOSE: measured against a rank-C opening warband
        /// only 2 of 4 answer axes clear it at all. That is an act-placement fact, not a number to
        /// flatten — softening it until three recruits can beat it would delete the problem it
        /// exists to pose.
        /// </summary>
        public static System.Func<int, EncounterDef>[] PoolFor(int act) => act switch
        {
            <= 1 => new System.Func<int, EncounterDef>[] { GnawingHour, NinthBell, TheDrop },
            // Four, not three. Act 2 is the wall act, and a three-deep pool of which two draws were
            // 340-HP Colossi meant many runs met two wall fights back to back: the naive line went
            // 3/12 completed runs to 0/12 and `ContentTests` correctly called the content
            // unwinnable. The Gnawing Hour returns as the clean-teach breather the through-line law
            // asks for ("early encounters teach one piece cleanly"), and act 2's identity still
            // comes from the two problems only it poses.
            2 => new System.Func<int, EncounterDef>[] { TheLongRange, TheDrop, GnawingHour },
            // Act 3 and every endless act past it (ADR 0016) keep facing the Crown's pool.
            _ => new System.Func<int, EncounterDef>[] { LongProcession, NinthBell, Slagworks },
        };

        public static EncounterDef Select(int act, Rng rng)
        {
            var pool = PoolFor(act);
            return pool[rng.Next(pool.Length)](act);
        }

        /// <summary>
        /// Look an authored encounter up by its published id — node pool first, then bosses. Exists
        /// so render fixtures (`scenarios.json`) can stage a real authored encounter instead of
        /// approximating one out of hero chassis. Returns null for an unknown id so the caller can
        /// report which scenario named it.
        /// </summary>
        public static EncounterDef? ById(string id, int act)
        {
            foreach (var factory in NodePool)
            {
                var def = factory(act);
                if (def.Id == id) return def;
            }
            foreach (var factory in BossPool)
            {
                var def = factory();
                if (def.Id == id) return def;
            }
            return null;
        }

        /// <summary>
        /// Act/tier difficulty, in ONE place so the catalog and the authoring probe can never drift
        /// into measuring different games. Anchored to act + tier and nothing else (ADR 0002: never
        /// the player's record).
        ///
        /// Stats are the SECOND difficulty lever, deliberately. ADR 0016: "difficulty adds new
        /// pressure before it adds raw stats" — so an act mostly changes an encounter's COMPOSITION
        /// (how many bodies, which roles, see each factory's `act` parameter) and this curve only
        /// tilts the same fight. The act-1 floor sits well under 100% because the authored blocks
        /// are written for the mid-run warband, not for three rank-C recruits with no gear.
        /// </summary>
        public static void Scale(UnitDef def, int act, FightTier tier)
        {
            int hpPct = 70 + 15 * (act - 1) + 10 * (int)tier;
            int atkPct = 70 + 12 * (act - 1) + 10 * (int)tier;
            def.MaxHp = def.MaxHp * hpPct / 100;
            def.Attack = def.Attack * atkPct / 100;
        }

        /// <summary>
        /// SWARM — more bodies than you have answers. No special rule at all: the pressure is the
        /// count, and the exam is whether anything you brought hits more than one thing at a time.
        /// (Deliberately ruleless. `pve-encounters.md` parks the linked-swarm "Dying Procession"
        /// until the bonded pair has actually been played — this does not spend that scope.)
        /// </summary>
        public static EncounterDef GnawingHour(int act)
        {
            // The act lever is BODIES, not bigger Hourlings: five is a problem a starting three can
            // out-trade, ten is a problem that needs an actual answer.
            int bodies = act <= 1 ? 5 : act == 2 ? 8 : 10;
            var slots = new[]
            {
                (5, 0), (5, 2), (5, 4), (6, 1), (6, 3),
                (4, 1), (4, 4), (6, 5), (7, 2), (7, 3),
            };
            var def = new EncounterDef
            {
                Id = "gnawing-hour",
                Name = "The Gnawing Hour",
                Pressure = $"{bodies} bodies, none dangerous alone. Can you kill faster than they arrive?",
                RuleName = "SWARM",
                RuleText = $"{bodies} Hourlings, no encounter rule. They are fast, they are fragile, "
                         + "and single-target damage will not keep up.",
            };
            for (int i = 0; i < bodies; i++)
                def.Enemies.Add(Place(Enemies.Hourling(), slots[i].Item1, slots[i].Item2, Enemies.Swarm));
            return def;
        }

        /// <summary>
        /// WARD — the wall is only a wall while its gunners live, and the gunners are shooting past
        /// the wall at your backline. Focusing the biggest threat is the WRONG answer, which is the
        /// decision the encounter exists to pose.
        /// </summary>
        public static EncounterDef TheLongRange(int act) => new EncounterDef
        {
            Id = "the-long-range",
            Name = "The Long Range",
            Pressure = "The wall is not the threat. Can you reach what is behind it?",
            RuleName = "WARD",
            RuleText = "The Ashen Colossus takes 50% less damage while any Sanddrift Gunner lives. "
                     + "Gunners fire at your FARTHEST unit and give ground to keep their distance.",
            Enemies =
            {
                Place(Enemies.Colossus(), 4, 2, Enemies.Anchor, "WARDED WALL"),
                Place(Enemies.Gunner(), 7, 1, Enemies.Artillery),
                Place(Enemies.Gunner(), 7, 4, Enemies.Artillery),
            },
        };

        /// <summary>
        /// RITUAL — a clock with a body, behind a wall. The Scribe's mana is fed by time alone, so
        /// the countdown is readable off its bar and answerable four ways: out-damage it, reach it,
        /// Silence it, or Stun it. The Colossus here is unwarded — one wall, one clock, one lesson.
        /// </summary>
        public static EncounterDef NinthBell(int act)
        {
            var def = new EncounterDef
            {
                Id = "ninth-bell",
                Name = "The Ninth Bell",
                Pressure = act <= 1
                    ? "A ritual you cannot out-wait. Can you reach it in time?"
                    : "A ritual you cannot out-wait, behind a body you cannot rush.",
                RuleName = "RITUAL",
                RuleText = "The Hour-Scribe never attacks. Its ritual advances on time alone — striking "
                         + "it does not hasten it — and at full mana it damages and Slows your ENTIRE "
                         + "warband, then begins again. Silence and Stun both stop the clock.",
            };
            // Act 1 teaches the clock on its own. From act 2 the clock gets a body in front of it,
            // which is the same lesson with the answer made expensive.
            if (act >= 2) def.Enemies.Add(Place(Enemies.Colossus(warded: false), 4, 2, Enemies.Anchor, "WALL"));
            def.Enemies.Add(Place(Enemies.Hourling(), 5, 0, Enemies.Swarm));
            def.Enemies.Add(Place(Enemies.Hourling(), 5, 4, Enemies.Swarm));
            if (act >= 3) def.Enemies.Add(Place(Enemies.Hourling(), 5, 2, Enemies.Swarm));
            def.Enemies.Add(Place(Enemies.Scribe(), 7, 2, Enemies.Ritualist));
            return def;
        }

        /// <summary>
        /// AMBUSH — the fight opens with two knives already behind you. This is the encounter that
        /// makes a back rank a decision instead of a default.
        /// </summary>
        public static EncounterDef TheDrop(int act)
        {
            int knives = act <= 1 ? 2 : 3;
            var def = new EncounterDef
            {
                Id = "the-drop",
                Name = "The Drop",
                Pressure = "Your backline is the front line. Where does that leave your artillery?",
                RuleName = "AMBUSH",
                RuleText = $"All {knives} Gloamstalkers leap adjacent to your FARTHEST unit the moment "
                         + "the fight begins, and keep hunting backward from there. A Sanddrift Gunner "
                         + "shoots past your front line while they work.",
            };
            def.Enemies.Add(Place(Enemies.Stalker(), 6, 1, Enemies.Diver));
            def.Enemies.Add(Place(Enemies.Stalker(), 6, 4, Enemies.Diver));
            if (knives >= 3) def.Enemies.Add(Place(Enemies.Stalker(), 5, 2, Enemies.Diver));
            def.Enemies.Add(Place(Enemies.Gunner(), 7, 2, Enemies.Artillery));
            return def;
        }

        /// <summary>
        /// TWO WALLS — the Ashfall Battery's geometry with its clock removed. Two Colossi hold the
        /// lane and the guns behind them are already shooting your back rank, so a front line does
        /// not protect anything and the answer is reach or go around. Deliberately RULELESS (the
        /// Gnawing Hour's precedent): the shape is the problem, and act 2's boss supplies the clock.
        ///
        /// Distinct from The Long Range on purpose — that one is a kill-ORDER problem (the ward
        /// makes the big body the wrong target); this is a GEOMETRY problem with no correct order.
        /// </summary>
        public static EncounterDef Slagworks(int act)
        {
            // The act lever is the LANE, not the numbers: the third wall closes the gap the player
            // learned to walk through, and it lands in act 3 where this encounter is the carried-
            // forward STRESS slot (3 of 4 axes, control cannot solve it, reach swings 83 points).
            //
            // It was briefly moved to act 2 to chase a non-flat number there, and that is exactly
            // the tune-to-the-metric trap: it made act 2's whole pool maximal — three wall/reach
            // problems with no breather — and the naive line went from 3/12 completed runs to 0/12.
            // Two walls at act 2 measures free for the four answer axes, which is the pool-wide
            // condition (44 of 48 cells sit at win=100), not something this encounter should be
            // distorted to fix on its own.
            var def = new EncounterDef
            {
                Id = "slagworks",
                Name = "The Slagworks",
                Pressure = "Two walls, and the guns are already past your front line.",
                RuleName = "LANE",
                RuleText = "No encounter rule. Two Ashen Colossi hold the lane; Sanddrift Gunners "
                         + "behind them acquire your FARTHEST unit and give ground to keep their "
                         + "distance. Standing behind your own wall does not help.",
            };
            def.Enemies.Add(Place(Enemies.Colossus(warded: false), 5, 1, Enemies.Anchor, "WALL"));
            def.Enemies.Add(Place(Enemies.Colossus(warded: false), 5, 4, Enemies.Anchor, "WALL"));
            def.Enemies.Add(Place(Enemies.Gunner(), 7, 0, Enemies.Artillery));
            def.Enemies.Add(Place(Enemies.Gunner(), 7, 5, Enemies.Artillery));
            return def;
        }

        /// <summary>
        /// PROCESSION — the Waning Crown's lesson, taught at a survivable scale. The Scribe's ritual
        /// advances on every death in its court, so clearing the escorts is both the obvious answer
        /// and the thing that fires the ritual sooner. This is the encounter act 3 was missing: on
        /// the old pool, the run's final boss was the FIRST time a player ever met the idea that
        /// their own kills can be the losing line, which is a knowledge check, not an exam.
        /// </summary>
        public static EncounterDef LongProcession(int act)
        {
            // The court is the clock, so the COURT is the act lever: every body is both a threat
            // and 4 more mana the moment you answer it. Six measured FREE and flat at act 3
            // (`--enc`, 2026-07-26) — the ritual fired but never mattered.
            int court = act <= 2 ? 4 : 6;
            var slots = new[] { (5, 0), (5, 4), (6, 1), (6, 3), (4, 2), (6, 5) };
            var def = new EncounterDef
            {
                Id = "long-procession",
                Name = "The Long Procession",
                Pressure = "Its court is the clock. Every one you kill winds it further.",
                RuleName = "PROCESSION",
                RuleText = $"The Procession-Scribe never attacks. Its ritual advances on time AND on "
                         + $"every death among its {court} Hourlings — including the ones you cause. "
                         + "At full mana it damages and Slows your ENTIRE warband. Silence stops the "
                         + "ritual completely; Stun holds it.",
            };
            for (int i = 0; i < court; i++)
                def.Enemies.Add(Place(Enemies.Hourling(), slots[i].Item1, slots[i].Item2, Enemies.Swarm));
            def.Enemies.Add(Place(Enemies.Scribe(deathFed: true), 7, 2, Enemies.Ritualist, "DEATH-FED RITUAL"));
            return def;
        }

        private static EncounterUnit Place(UnitDef def, int row, int col, string roleId,
                                           string role = "") =>
            new EncounterUnit
            {
                Def = def, Pos = Hex.FromRowCol(row, col), RoleId = roleId,
                Role = string.IsNullOrEmpty(role) ? Enemies.RoleLabel(roleId) : role,
            };

        /// <summary>
        /// Read the Bond out of a resolved fight. Keyed on the published BondHaste magnitude so
        /// it can never confuse the Enrage with an incidental Haste from a player's own kit.
        /// </summary>
        public static BondReadout ReadBond(BattleResult result, ICollection<int> enemyIds)
        {
            int enrageTick = -1, survivor = -1;
            foreach (var e in result.Events)
            {
                if (e.Kind != EventKind.StatusApplied) continue;
                if (e.Aux != (int)StatusKind.Haste || e.Amount != BondHaste) continue;
                if (!enemyIds.Contains(e.Target)) continue;
                enrageTick = e.Tick; survivor = e.Target;
                break;
            }

            int enemyDeaths = 0;
            foreach (var e in result.Events)
                if (e.Kind == EventKind.Death && enemyIds.Contains(e.Target))
                    enemyDeaths++;

            if (enrageTick < 0)
                return new BondReadout(false, -1, -1, 0, 0, enemyDeaths);

            int swings = 0;
            foreach (var e in result.Events)
                if (e.Kind == EventKind.Attack && e.Source == survivor && e.Tick >= enrageTick)
                    swings++;

            return new BondReadout(true, enrageTick, survivor,
                result.EndTick - enrageTick, swings, enemyDeaths);
        }

        /// <summary>
        /// First PvE proof from Design/pve-encounters.md: the player sees both enemies,
        /// their formation, and the complete Bond rule before deployment.
        /// </summary>
        public static EncounterDef BondedPair()
        {
            var bulwark = Loadout.Compose(Kits.Chassis["bulwark"]).Def;
            bulwark.Name = "Oathbound Bulwark";
            bulwark.MaxHp = 230;
            bulwark.Attack = 8;
            bulwark.Triggers.Add(BondEnrage());

            var sharpshot = Loadout.Compose(Kits.Chassis["sharpshot"]).Def;
            sharpshot.Name = "Oathbound Sharpshot";
            sharpshot.MaxHp = 135;
            sharpshot.Attack = 12;
            sharpshot.Triggers.Add(BondEnrage());

            return new EncounterDef
            {
                Id = "bonded-pair",
                Name = "The Last Oath",
                Pressure = "Choose which threat you are willing to leave enraged.",
                RuleName = "BOND",
                RuleText = "When either Oathbound dies, the survivor Enrages (+100% Attack Speed).",
                Enemies =
                {
                    new EncounterUnit
                    {
                        Def = bulwark,
                        Pos = Hex.FromRowCol(5, 0),
                        RoleId = Enemies.Anchor,
                        Role = "OATHBOUND · FRONTLINE",
                    },
                    new EncounterUnit
                    {
                        Def = sharpshot,
                        Pos = Hex.FromRowCol(5, 5),
                        RoleId = Enemies.Artillery,
                        Role = "OATHBOUND · BACKLINE",
                    },
                },
            };
        }

        private static Trigger BondEnrage() =>
            On(EventKind.Death, W(TgtAlly()),
                Status(StatusKind.Haste, BondHaste, Self)).Named("oath.bond");

        // ---- act bosses (2026-07-26, ADR 0024) --------------------------------------
        // Until tonight `Catalog.Boss` returned the act-scaled Last Oath for EVERY act, so a
        // three-act run ended three times on the same two bodies with bigger numbers. Each act now
        // closes on a DIFFERENT strength exam (pve-encounters.md "a boss is a strength exam"), and
        // each exam admits several qualitatively different strong answers:
        //
        // | act | boss                | the one-sentence pressure          | answers it admits |
        // |-----|---------------------|------------------------------------|-------------------|
        // | 1   | The Last Oath       | which threat you leave enraged     | focus order · burst · control · placement |
        // | 2   | The Ashfall Battery | reach the gun behind the wall      | reach · dive · spread · sustain · Silence/Stun |
        // | 3   | The Waning Crown    | your own kills ring the bell       | burst the Crown · reach · Silence/Stun · sustain |
        //
        // Act 1 deliberately keeps the Last Oath: it is the only boss whose decision has been
        // MEASURED (oath-probe-2026-07-25 — placement chooses the survivor in 4/4 lineups, Δ84
        // win%), and pve-encounters.md says to earn new bonded scope by playing the pair first.
        // Re-authoring it would have thrown that evidence away.
        public static readonly System.Func<EncounterDef>[] BossPool =
        {
            BondedPair, AshfallBattery, WaningCrown,
        };

        /// <summary>The act's boss. Acts past the authored three (the endless horizon, ADR 0016)
        /// keep facing the last one — scaled, never re-rolled.</summary>
        public static EncounterDef BossFor(int act) =>
            BossPool[System.Math.Min(System.Math.Max(act, 1), BossPool.Length) - 1]();

        /// <summary>
        /// Bosses are authored FOR their own act, so unlike node encounters they take no act
        /// curve — 100% is the number that was measured. The multiplier exists only for acts
        /// beyond the authored three, where the act-3 boss is all there is to escalate.
        /// </summary>
        public static int BossScalePct(int act) =>
            act <= BossPool.Length ? 100 : 100 + 25 * (act - BossPool.Length);

        /// <summary>
        /// BATTERY (act 2) — the exam for everything act 2 teaches about reach. The Bombard is
        /// Rooted behind two Colossi and cannot be starved: its clock runs on time alone, so
        /// hitting the wall does not buy time, and out-DPSing 300 HP through 2 × 340 HP of body is
        /// not a plan. The shell lands on your FARTHEST unit and burns the ground around it, so
        /// the reflex answer to artillery — bunch up behind the tank — is the losing one.
        ///
        /// Authoring test (pve-encounters.md): ① reach the gun before its casts resolve.
        /// ② An ordinary warband cannot: the wall out-bodies its damage and the shell out-paces
        /// its healing. ③ Strong answers: a diver/Leap, long reach past the wall, spreading so the
        /// crater takes one body, Silence or Stun on the Bombard, or sustain through the shells.
        /// ④ Sharpshot/Shade/Berserker get reach work; Cleric/Bulwark get spread-sustain work.
        /// ⑤ It pressures slow, clumped, short-reach warbands — it does not switch off any build.
        /// ⑥ The crater is a field, which the client already renders as ground.
        /// </summary>
        public static EncounterDef AshfallBattery() => new EncounterDef
        {
            Id = "ashfall-battery",
            Name = "The Ashfall Battery",
            Pressure = "The gun is behind the wall, and the shell falls on whoever stands farthest back.",
            RuleName = "BATTERY",
            RuleText = "The Ashfall Bombard is Rooted and never attacks. Its clock advances on time "
                     + "ALONE — striking it does not hasten it — and at full mana it shells your "
                     + "FARTHEST unit and leaves burning ground around the crater. Two Ashen "
                     + "Colossi hold the lane in front of it. Silence stops the clock; Stun holds it.",
            Enemies =
            {
                Place(Enemies.Colossus(warded: false), 5, 1, Enemies.Anchor, "WALL"),
                Place(Enemies.Colossus(warded: false), 5, 4, Enemies.Anchor, "WALL"),
                Place(Enemies.Hourling(), 6, 2, Enemies.Swarm),
                Place(Enemies.Hourling(), 6, 3, Enemies.Swarm),
                Place(Enemies.Bombard(), 7, 2, Enemies.Siege),
            },
        };

        /// <summary>
        /// WANING (act 3) — the run's last exam, and the only encounter that turns the player's own
        /// habit against them. Three acts of node fights train "clear what is in front of you";
        /// here every death in the Crown's court advances its bell, so clearing the court is what
        /// rings it. The court is deliberately the whole role grammar — wall, artillery, swarm,
        /// diver — so that ignoring it is not free either.
        ///
        /// Authoring test: ① can you end the Hour before it ends you, when killing its servants
        /// hurries it? ② An ordinary warband cannot burst 460 HP behind six bodies, and cannot eat
        /// repeated warband-wide damage plus a Slow. ③ Strong answers: burst the Crown past its
        /// court · reach it · Silence it (mana gain is gated on Silence, so the bell stops dead) ·
        /// Stun it · or out-sustain the bells while grinding. ④ Every path gets work: reach,
        /// control, sustain, and raw damage all answer. ⑤ It pressures slow single-target warbands
        /// with no reach and no control — three separate ways out. ⑥ The bell is a mana bar, a
        /// board-wide flash and a Slow on every ally: legible in automatic combat.
        /// </summary>
        public static EncounterDef WaningCrown() => new EncounterDef
        {
            Id = "waning-crown",
            Name = "The Waning Crown",
            Pressure = "The Hour itself. Every death in its court rings the bell sooner — including the ones you cause.",
            RuleName = "WANING",
            RuleText = "The Waning Crown is Rooted and never attacks. Its bell advances on time — "
                     + "and EVERY death among its court advances it further, including the deaths "
                     + "you cause. When the bell rings, your whole warband takes damage and is "
                     + "Slowed. Silence stops the bell completely; Stun holds it while it lasts.",
            Enemies =
            {
                Place(Enemies.Colossus(warded: false), 5, 2, Enemies.Anchor, "WALL"),
                Place(Enemies.Hourling(), 4, 1, Enemies.Swarm),
                Place(Enemies.Hourling(), 4, 4, Enemies.Swarm),
                Place(Enemies.Stalker(), 6, 3, Enemies.Diver),
                Place(Enemies.Gunner(), 7, 0, Enemies.Artillery),
                Place(Enemies.Gunner(), 7, 5, Enemies.Artillery),
                Place(Enemies.Crown(), 7, 2, Enemies.Hour),
            },
        };
    }
}
