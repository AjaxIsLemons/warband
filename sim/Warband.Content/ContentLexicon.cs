using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Content
{
    /// <summary>
    /// Display copy for CONTENT ids — spec nodes and chassis. The sim half of the vocabulary
    /// (StatusKind, Cause) lives in Warband.Sim.Lexicon; the split follows the assembly
    /// dependency, which only runs Content → Sim.
    ///
    /// Weapons are deliberately absent: WeaponDef.Name is already display text ("Officer's
    /// Sabre"), and their mastery riders are covered by SkirmishProof.MasteryCopy. Adding a
    /// third weapon-copy site would be the drift this class exists to end.
    ///
    /// Node text is descriptive, not numeric. Exact magnitudes are pre-playtest tuning and
    /// would rot on the first balance pass; a tooltip that says "harder, not longer" survives
    /// retuning, one that says "+30%" does not.
    /// </summary>
    public static class ContentLexicon
    {
        public static LexEntry Node(string id) =>
            Nodes.TryGetValue(id, out var e) ? e : Lexicon.Fallback(id);

        public static LexEntry Chassis(string id) =>
            Chassis_.TryGetValue(id, out var e) ? e : Lexicon.Fallback(id);

        /// <summary>
        /// Stable player-facing identity for the Signature a composed unit actually casts.
        /// Base ability names are authored here; specialization abilities reuse their node name.
        /// Mechanical text never lives in either table — <see cref="PlayerRuleProjection"/> binds
        /// the resolved <see cref="UnitDef"/> to <see cref="MechanicalRulePresenter"/>.
        /// </summary>
        public static LexEntry Signature(string abilityId)
        {
            if (Signatures.TryGetValue(abilityId, out var signature)) return signature;
            if (Nodes.TryGetValue(abilityId, out var node))
                return new LexEntry(node.Name, "", node.Kind);
            return Lexicon.Fallback(abilityId);
        }

        /// <summary>
        /// Stable identity for a chassis's innate rule. The effects are deliberately absent:
        /// they come from the chassis triggers/stat rules after composition.
        /// </summary>
        public static LexEntry Innate(string chassisId) =>
            Innates.TryGetValue(chassisId, out var innate)
                ? innate
                : Lexicon.Fallback(chassisId);

        public static readonly IReadOnlyDictionary<string, LexEntry> Chassis_ =
            new Dictionary<string, LexEntry>
            {
                ["cleric"] = new LexEntry("Cleric",
                    "Sister Maren of the Waning Bell. Her censer heals whoever needs it most instead of harming what it hits.", LexKind.Mending),
                ["bulwark"] = new LexEntry("Bulwark",
                    "Brakka, Shieldmaid of the Bronze Hour. The anchor: shields, stuns, and forced attention.", LexKind.Ward),
                ["shade"] = new LexEntry("Shade",
                    "Null, the Redacted. The knife in the dark — crits, executions, and absence when the board turns on her.", LexKind.Precision),
                ["sharpshot"] = new LexEntry("Sharpshot",
                    "Calamity Vance, the Last Deadeye. Reach is the weapon: she wins by never being reached.", LexKind.Power),
                ["pyromancer"] = new LexEntry("Pyromancer",
                    "Ilion-7, Cinder of a Dead Star. Burn as an economy — pools that spend themselves, ground that keeps burning.", LexKind.Affliction),
                ["berserker"] = new LexEntry("Berserker",
                    "Ulfrik, Who Burns His Hours. Frenzy and risk: he trades his own safety for swing rate.", LexKind.Tempo),
                ["phalanx"] = new LexEntry("Phalanx",
                    "Leonnatos of the Unbroken Line. Spacing and punishment — he owns the hex nobody wants to approach.", LexKind.Reaction),
                ["banneret"] = new LexEntry("Banneret",
                    "Capitana Vespera, Banner of the Turning Age. The captain: whoever stands beside the banner at placement is sworn to the Company, and every rally reaches them for the rest of the fight.", LexKind.Utility),
            };

        public static readonly IReadOnlyDictionary<string, LexEntry> Signatures =
            new Dictionary<string, LexEntry>
            {
                ["cleric"] = new LexEntry("Sanctified Pyre", "", LexKind.Mending),
                ["bulwark"] = new LexEntry("Shield Slam", "", LexKind.Control),
                ["shade"] = new LexEntry("Backstab", "", LexKind.Precision),
                ["sharpshot"] = new LexEntry("Piercing Bolt", "", LexKind.Power),
                ["pyromancer"] = new LexEntry("Fire Glyph", "", LexKind.Affliction),
                ["berserker"] = new LexEntry("Frenzy", "", LexKind.Tempo),
                ["phalanx"] = new LexEntry("Skewer", "", LexKind.Reaction),
                ["banneret"] = new LexEntry("Rally", "", LexKind.Utility),
            };

        public static readonly IReadOnlyDictionary<string, LexEntry> Innates =
            new Dictionary<string, LexEntry>
            {
                ["cleric"] = new LexEntry("Mercy Aura", "", LexKind.Mending),
                ["bulwark"] = new LexEntry("Bastion", "", LexKind.Ward),
                ["shade"] = new LexEntry("Ambush", "", LexKind.Evasion),
                ["sharpshot"] = new LexEntry("Full Draw", "", LexKind.Power),
                ["pyromancer"] = new LexEntry("Firebrand", "", LexKind.Affliction),
                ["berserker"] = new LexEntry("Burning Hours", "", LexKind.Tempo),
                ["phalanx"] = new LexEntry("Riposte", "", LexKind.Reaction),
                ["banneret"] = new LexEntry("Standard-Bearer", "", LexKind.Utility),
            };

        public static readonly IReadOnlyDictionary<string, LexEntry> Nodes =
            new Dictionary<string, LexEntry>
            {
                // ---- Cleric ----------------------------------------------------------
                ["cleric.warpriest"] = new LexEntry("War-Priest",
                    "Deepens the bruiser: a heavier Sanctified Pyre that leaves enemies Burning.", LexKind.Affliction),
                ["cleric.lifebinder"] = new LexEntry("Lifebinder",
                    "Swaps to the backline: the pulse abandons the nova and pours into the most wounded ally.", LexKind.Mending),
                ["cleric.warpriest.scorched"] = new LexEntry("Scorched Earth",
                    "Her swings splash onto the nearest Burning enemy.", LexKind.Affliction),
                ["cleric.warpriest.contagion"] = new LexEntry("Contagion",
                    "When a Burning enemy dies its remaining pool moves to the nearest — harder, not longer.", LexKind.Affliction),
                ["cleric.warpriest.conflagration"] = new LexEntry("Conflagration",
                    "Every cast detonates the Burn on all Burning enemies in radius, consuming it.", LexKind.Affliction),
                ["cleric.warpriest.zeal"] = new LexEntry("Zeal",
                    "While any enemy Burns she swings faster, and her swings feed her.", LexKind.Tempo),
                ["cleric.lifebinder.grace"] = new LexEntry("Lasting Grace",
                    "The pulse leaves healing ground behind it.", LexKind.Mending),
                ["cleric.lifebinder.chorus"] = new LexEntry("Chorus",
                    "Every third swing hastes an ally or slows an enemy.", LexKind.Tempo),
                ["cleric.lifebinder.greatchorus"] = new LexEntry("Great Chorus",
                    "The pulse fires twice, the second finding whoever is lowest by then.", LexKind.Mending),
                ["cleric.lifebinder.sanctuary"] = new LexEntry("Sanctuary",
                    "Every cast also shields the most wounded ally.", LexKind.Ward),

                // ---- Bulwark ---------------------------------------------------------
                ["bulwark.juggernaut"] = new LexEntry("Juggernaut",
                    "Shield Slam strikes every adjacent enemy, and stuns for longer.", LexKind.Control),
                ["bulwark.warden"] = new LexEntry("Warden",
                    "The challenge: taunts at reach 3 and shields himself, dealing no damage at all.", LexKind.Control),
                ["bulwark.juggernaut.concussive"] = new LexEntry("Concussive",
                    "Every third swing slows whoever it lands on.", LexKind.Control),
                ["bulwark.juggernaut.shockwave"] = new LexEntry("Shockwave",
                    "Gains Shield for each enemy the Slam catches.", LexKind.Ward),
                ["bulwark.juggernaut.faultline"] = new LexEntry("Faultline",
                    "The Slam reaches two hexes out and stuns everything inside it.", LexKind.Control),
                ["bulwark.juggernaut.grudgekeeper"] = new LexEntry("Grudgekeeper",
                    "The wall swings with its own weight — his attack scales with the Shield he carries.", LexKind.Power),
                ["bulwark.warden.bellow"] = new LexEntry("Bellow",
                    "Enemies holding his Taunt swing weaker.", LexKind.Power),
                ["bulwark.warden.rebuke"] = new LexEntry("Rebuke",
                    "Swings against the enemies he has taunted grant him Shield.", LexKind.Ward),
                ["bulwark.warden.challenge"] = new LexEntry("Open Challenge",
                    "The taunt reaches four hexes, and the self-shield grows with it.", LexKind.Control),
                ["bulwark.warden.retribution"] = new LexEntry("Retribution",
                    "Attackers carrying his Taunt take his thorns back.", LexKind.Reaction),

                // ---- Shade -----------------------------------------------------------
                ["shade.killerstempo"] = new LexEntry("Killer's Tempo",
                    "A kill nearby sends her faster for a moment.", LexKind.Tempo),
                ["shade.opportunist"] = new LexEntry("Opportunist",
                    "Her ability tears half again as deep into anything already wounded.", LexKind.Power),
                ["shade.reaper"] = new LexEntry("Reaper",
                    "The gamble: a large crit bonus, and crits below 30% HP execute outright.", LexKind.Precision),
                ["shade.phantom"] = new LexEntry("Phantom",
                    "The vanish: a burst phases her out, and she returns with a Leap and permanent Empowered.", LexKind.Evasion),
                ["shade.reaper.twist"] = new LexEntry("Twist the Knife",
                    "Crits Mark their victim, and Backstab twists marked wounds.", LexKind.Precision),
                ["shade.reaper.widowmaker"] = new LexEntry("Widowmaker",
                    "Her critical hits strike far harder.", LexKind.Precision),
                ["shade.phantom.hereandgone"] = new LexEntry("Here and Gone",
                    "Every cast phases her out — offense schedules the vanish.", LexKind.Evasion),
                ["shade.phantom.coldreturn"] = new LexEntry("Cold Return",
                    "The first swing after each Phase is an automatic crit.", LexKind.Precision),

                // ---- Sharpshot -------------------------------------------------------
                ["sharpshot.sniper"] = new LexEntry("Sniper",
                    "The nuker: the bolt seeks the farthest enemy down a board-length line.", LexKind.Power),
                ["sharpshot.volleyer"] = new LexEntry("Volleyer",
                    "The scaler: the cast feeds her autos with extra arrows.", LexKind.Tempo),
                ["sharpshot.sniper.twinnock"] = new LexEntry("Twin Nock",
                    "Every third swing fires twice, the second at half weight.", LexKind.Tempo),
                ["sharpshot.sniper.overpen"] = new LexEntry("Overpenetration",
                    "Each body the bolt passes adds weight for everyone farther down the line.", LexKind.Power),
                ["sharpshot.sniper.onebreath"] = new LexEntry("One Breath",
                    "Half as often, twice as hard.", LexKind.Power),
                ["sharpshot.sniper.killwindow"] = new LexEntry("Kill Window",
                    "Kills refund half her mana.", LexKind.Utility),
                ["sharpshot.volleyer.rollingfire"] = new LexEntry("Rolling Fire",
                    "Each cast hastes her next three swings.", LexKind.Tempo),
                ["sharpshot.volleyer.splitheads"] = new LexEntry("Splitheads",
                    "Extra arrows splinter into whoever stands beside their victim.", LexKind.Power),
                ["sharpshot.volleyer.arrowstorm"] = new LexEntry("Arrowstorm",
                    "She opens the fight as though she had already cast twice.", LexKind.Tempo),
                ["sharpshot.volleyer.trueflight"] = new LexEntry("Trueflight",
                    "Width becomes weight: her extra arrows land at full damage.", LexKind.Power),

                // ---- Pyromancer ------------------------------------------------------
                ["pyromancer.inferno"] = new LexEntry("Inferno",
                    "The ground is the weapon: a wider field that spreads on every kill.", LexKind.Affliction),
                ["pyromancer.starfall"] = new LexEntry("Starfall",
                    "The target is the weapon: no field at all, everything poured into the strike.", LexKind.Power),
                ["pyromancer.inferno.chokingsmoke"] = new LexEntry("Choking Smoke",
                    "Enemies she hurts inside her own fields choke.", LexKind.Control),
                ["pyromancer.inferno.stoke"] = new LexEntry("Stoke",
                    "Her swings stoke any enemy standing in the coals.", LexKind.Affliction),
                ["pyromancer.inferno.worldalight"] = new LexEntry("World Alight",
                    "Fire ignites under every Burning enemy on the board.", LexKind.Affliction),
                ["pyromancer.inferno.everburn"] = new LexEntry("Everburn",
                    "The board slowly becomes fire.", LexKind.Affliction),
                ["pyromancer.starfall.detonate"] = new LexEntry("Detonate",
                    "The strike consumes the Burn pool, gaining weight for every stack it eats.", LexKind.Affliction),
                ["pyromancer.starfall.kindling"] = new LexEntry("Kindling",
                    "Her swings leave a small Burn behind them.", LexKind.Affliction),
                ["pyromancer.starfall.dyingstar"] = new LexEntry("Dying Star",
                    "A kill grants a free recast on the nearest Burning enemy.", LexKind.Utility),
                ["pyromancer.starfall.whiteheat"] = new LexEntry("White Heat",
                    "Burn ticks on her enemies land for double.", LexKind.Affliction),

                // ---- Berserker -------------------------------------------------------
                ["berserker.bloodreaver"] = new LexEntry("Bloodreaver",
                    "The drain bruiser: his Frenzy swings drink back what they deal.", LexKind.Mending),
                ["berserker.rampager"] = new LexEntry("Rampager",
                    "The whirlwind: his Frenzy swings splash into everything around him.", LexKind.Power),
                ["berserker.bloodreaver.scent"] = new LexEntry("Scent of Blood",
                    "His swings tear deeper into anything already wounded.", LexKind.Power),
                ["berserker.bloodreaver.redharvest"] = new LexEntry("Red Harvest",
                    "Kills during Frenzy extend the window.", LexKind.Tempo),
                ["berserker.bloodreaver.deathless"] = new LexEntry("Deathless",
                    "The sanctioned cheat: one lethal blow leaves him standing at 1 HP.", LexKind.Evasion),
                ["berserker.bloodreaver.crimsontide"] = new LexEntry("Crimson Tide",
                    "Healing beyond full hardens into Shield instead of spilling.", LexKind.Ward),
                ["berserker.rampager.reckless"] = new LexEntry("Reckless Swing",
                    "The risk dial: he hits harder and takes more, feeding his own rage.", LexKind.Power),
                ["berserker.rampager.aftershock"] = new LexEntry("Aftershock",
                    "The Frenzy window closes with a full ring of damage.", LexKind.Power),
                ["berserker.rampager.avalanche"] = new LexEntry("Avalanche",
                    "The longer storm — the window stays open.", LexKind.Tempo),
                ["berserker.rampager.noquarter"] = new LexEntry("No Quarter",
                    "Width becomes weight: his cleave carries full damage.", LexKind.Power),

                // ---- Phalanx ---------------------------------------------------------
                ["phalanx.pikewall"] = new LexEntry("Pikewall",
                    "The wall: he counters everything, and punishes anyone who Leaps in.", LexKind.Reaction),
                ["phalanx.lancer"] = new LexEntry("Lancer",
                    "The lunge: the thrust runs through every enemy standing on the line.", LexKind.Power),
                ["phalanx.pikewall.spearpoint"] = new LexEntry("Spearpoint",
                    "Rewards holding the enemy at exactly max reach.", LexKind.Power),
                ["phalanx.pikewall.sharprebuke"] = new LexEntry("Sharp Rebuke",
                    "His ripostes disarm whoever they strike.", LexKind.Control),
                ["phalanx.pikewall.unbrokenline"] = new LexEntry("Unbroken Line",
                    "Allies placed beside him share his Riposte.", LexKind.Reaction),
                ["phalanx.pikewall.givenoground"] = new LexEntry("Give No Ground",
                    "Fortified for as long as his Taunt holds someone.", LexKind.Ward),
                ["phalanx.lancer.overreach"] = new LexEntry("Overreach",
                    "Every swing also stabs the hexes behind the target.", LexKind.Power),
                ["phalanx.lancer.deepthrust"] = new LexEntry("Deep Thrust",
                    "The lunge drives harder for every body it passes.", LexKind.Power),
                ["phalanx.lancer.sarissa"] = new LexEntry("Sarissa",
                    "The lunge runs the full length of the board.", LexKind.Power),
                ["phalanx.lancer.perfectform"] = new LexEntry("Perfect Form",
                    "The untouched spearman keeps his tempo.", LexKind.Tempo),

                // ---- Banneret --------------------------------------------------------
                ["banneret.herald"] = new LexEntry("Herald",
                    "The shieldward: his banner protects the Company.", LexKind.Ward),
                ["banneret.warcaller"] = new LexEntry("War-Caller",
                    "The dread captain: one cast moves both tempos at once.", LexKind.Tempo),
                ["banneret.herald.steady"] = new LexEntry("Steady the Line",
                    "The Company holds too — mustered allies take less damage.", LexKind.Ward),
                ["banneret.herald.secondwind"] = new LexEntry("Second Wind",
                    "Allies below half receive double Rally.", LexKind.Mending),
                ["banneret.herald.quickening"] = new LexEntry("Quickening",
                    "Intensity over breadth: the few, faster.", LexKind.Tempo),
                ["banneret.herald.widebanner"] = new LexEntry("Wide Banner",
                    "Breadth over intensity: the muster reaches two hexes out, swearing in a larger Company.", LexKind.Utility),
                ["banneret.warcaller.drumbeat"] = new LexEntry("Drumbeat",
                    "Rallied allies swing faster for their next three.", LexKind.Tempo),
                ["banneret.warcaller.dreadpresence"] = new LexEntry("Dread Presence",
                    "Enemies standing beside him swing slower — a living aura.", LexKind.Tempo),
                ["banneret.warcaller.bearer"] = new LexEntry("Living Inscription",
                    "When a law of the Hourstone activates, Vespera gains Mana — at most once per chain of events.", LexKind.Utility),
                ["banneret.warcaller.lastmarch"] = new LexEntry("Last March",
                    "The whole warband counts as his Company.", LexKind.Utility),
            };

        /// <summary>
        /// Display copy for a RULE id — the render identity Loadout.Compose stamps onto every
        /// Trigger and StatRule (Design/passive-legibility.md). This is the one place that knows how
        /// to read the id shapes the composer produces, so a new source of rules is handled here and
        /// nowhere else:
        ///
        /// <list type="bullet">
        /// <item>spec node — "berserker.bloodreaver.redharvest" → the Nodes table</item>
        /// <item>chassis innate — "berserker" → the Chassis table</item>
        /// <item>weapon — "Greataxe" is already display text, by the same law that keeps weapons
        ///       out of the Nodes table above</item>
        /// <item>mastery rider — "Greataxe/mastery"</item>
        /// <item>team rule — "inscription.chorus" → the Inscription registry (ADR 0026)</item>
        /// <item>authored enemy/boss rule — "crown.bell", named in Enemies.cs/Encounters.cs</item>
        /// <item>the "#2" suffix a source with several rules gets</item>
        /// </list>
        ///
        /// Never throws and never returns empty: an unrecognised id degrades to its raw text, which
        /// is readable-but-obviously-placeholder — the same contract as <see cref="Lexicon.Fallback"/>.
        /// That is what lets a passive be authored today and named properly later.
        /// </summary>
        public static LexEntry Rule(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId)) return Lexicon.Fallback("Passive");

            // "#2" marks the second rule from one source; resolve the source, then say which.
            string id = ruleId;
            string ordinal = "";
            int hash = id.LastIndexOf('#');
            if (hash > 0)
            {
                ordinal = " " + id.Substring(hash + 1);
                id = id.Substring(0, hash);
            }

            var entry = Resolve(id);
            return ordinal.Length == 0
                ? entry
                : new LexEntry(entry.Name + ordinal, entry.Text, entry.Kind);
        }

        private static LexEntry Resolve(string id)
        {
            if (id.EndsWith("/mastery", System.StringComparison.Ordinal))
            {
                string weapon = id.Substring(0, id.Length - "/mastery".Length);
                return new LexEntry(weapon + " mastery",
                    "The weapon's latent rider — live for specialists, and for everyone at Relic temper.",
                    LexKind.Utility);
            }
            if (id.EndsWith("/signature", System.StringComparison.Ordinal))
            {
                string ability = id.Substring(0, id.Length - "/signature".Length);
                return Signature(ability);
            }
            if (id.StartsWith("inscription.", System.StringComparison.Ordinal))
            {
                string key = id.Substring("inscription.".Length);
                return Catalog.Inscriptions.TryGetValue(key, out var inscription)
                    ? new LexEntry(inscription.Name, "A law inscribed in the Hourstone — carried by the whole warband.", LexKind.Utility)
                    : Lexicon.Fallback(key);
            }
            if (Nodes.TryGetValue(id, out var node)) return node;
            if (Innates.TryGetValue(id, out var innate)) return innate;
            if (Authored.TryGetValue(id, out var authored)) return authored;
            // A weapon name, a trinket name, or something not yet authored: the id IS the copy.
            return Lexicon.Fallback(id);
        }

        /// <summary>Rules authored directly onto enemy and boss bodies rather than composed from a
        /// kit. ADR 0024's disclosure contract promises the player these by name, so they get one.</summary>
        public static readonly IReadOnlyDictionary<string, LexEntry> Authored =
            new Dictionary<string, LexEntry>
            {
                ["enemy.ward"] = new LexEntry("Ward",
                    "Its escorts are holding the damage off it. Kill them and the Ward drops.", LexKind.Ward),
                ["enemy.ward.break"] = new LexEntry("Ward Broken",
                    "An escort fell — the Ward is gone.", LexKind.Ward),
                ["enemy.deathfed"] = new LexEntry("Death-fed",
                    "Every death in its court advances the ritual — including the ones you cause.", LexKind.Utility),
                ["enemy.rooted"] = new LexEntry("Channelling",
                    "It stands where it was placed and channels. It will not come to you.", LexKind.Control),
                ["enemy.emplaced"] = new LexEntry("Emplaced",
                    "A fixed battery. It never advances.", LexKind.Control),
                ["enemy.ambush"] = new LexEntry("Ambush",
                    "It opens the fight already in your backline.", LexKind.Utility),
                ["oath.bond"] = new LexEntry("The Bond",
                    "When its sworn partner falls, the survivor is enraged.", LexKind.Tempo),
                ["crown.emplaced"] = new LexEntry("Emplaced",
                    "The Crown does not move. The fight comes to it.", LexKind.Control),
                ["crown.bell"] = new LexEntry("The Bell",
                    "Every death in its court rings the bell closer — yours included.", LexKind.Utility),
            };
    }
}
