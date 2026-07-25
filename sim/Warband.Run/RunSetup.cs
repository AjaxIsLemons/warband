using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// Starting a run. `RunHarness.StarterWarband` takes the first N of the pool because a bot
    /// does not choose — a player does, so the shell needs an OFFER to choose from and a way to
    /// turn that choice into a controller.
    ///
    /// Deterministic from the run seed: the same seed always offers the same recruits, so a run
    /// is reproducible end to end (the same property replays depend on) and a "reroll the seed"
    /// button stays honest.
    /// </summary>
    public static class RunSetup
    {
        // The opening draft is deliberately tight: see five complete, comparable cards and
        // choose three. Six made the first decision read like a catalog page on landscape
        // screens and diluted the "what does this warband need?" comparison.
        public const int DefaultRecruitOffer = 5;

        /// <summary>
        /// The recruit draft for a NEW run: distinct chassis drawn from the act-1 pool. Distinct
        /// because picking the same hero three times is not a choice, and duplicates already have
        /// a meaning in the shop (a dupe card is a rank-up, ADR 0006).
        /// </summary>
        public static List<string> RecruitOffer(IRunContent content, ulong seed,
                                                int count = DefaultRecruitOffer)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var pool = content.HeroPool(1);
            var rng = new Rng(seed ^ 0x5EEDu);
            var remaining = new List<string>(pool);
            var offer = new List<string>();
            while (offer.Count < count && remaining.Count > 0)
            {
                int i = rng.Next(remaining.Count);
                offer.Add(remaining[i]);
                remaining.RemoveAt(i);
            }
            return offer;
        }

        /// <summary>
        /// Begin a run with the player's picked chassis. Validates against the same slot rule the
        /// controller enforces, but fails with a message a UI can show rather than an argument
        /// exception from deep inside the machine.
        /// </summary>
        public static RunController Begin(ulong seed, IRunContent content,
                                          IReadOnlyList<string> pickedChassis, RunConfig? config = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (pickedChassis == null) throw new ArgumentNullException(nameof(pickedChassis));
            var cfg = config ?? new RunConfig();

            if (pickedChassis.Count < 1 || pickedChassis.Count > cfg.StartingFieldSlots)
                throw new ArgumentException(
                    $"pick 1..{cfg.StartingFieldSlots} heroes to start (got {pickedChassis.Count})");
            if (pickedChassis.Distinct().Count() != pickedChassis.Count)
                throw new ArgumentException("cannot start with the same hero twice");

            var band = pickedChassis.Select(id => new HeroInstance { ChassisId = id }).ToList();
            return new RunController(seed, content, band, cfg);
        }

        /// <summary>How many recruits the player still owes before the run can begin.</summary>
        public static int PicksRemaining(int picked, RunConfig? config = null) =>
            Math.Max(0, (config ?? new RunConfig()).StartingFieldSlots - picked);
    }
}
