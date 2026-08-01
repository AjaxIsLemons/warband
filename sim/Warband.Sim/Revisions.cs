using System;
using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>
    /// The two authored ways a run may intervene in an otherwise autonomous battle.
    /// This is simulation vocabulary, not content identity: the run/content layer owns
    /// names, copy and upgrade offers; Battle owns the deterministic state change.
    /// </summary>
    public enum RevisionEffectKind
    {
        BorrowedFuture,
        RecallToFormation,
    }

    /// <summary>Authored upgrade consequences already resolved by the run layer.</summary>
    [Flags]
    public enum RevisionModifier
    {
        None = 0,
        SharedPremonition = 1 << 0,
        DeepReserve = 1 << 1,
        ClearIntention = 1 << 2,
        LongMemory = 1 << 3,
        Convergence = 1 << 4,
        Afterthought = 1 << 5,
        FixedPoint = 1 << 6,
        LongPeace = 1 << 7,
        RollCall = 1 << 8,
        EmptyHands = 1 << 9,
        GeneralRecall = 1 << 10,
        MissingHour = 1 << 11,
    }

    /// <summary>
    /// One target's value in the watched future. The Battle computes the branch value from
    /// its own live state; a caller cannot supply the carried delta or a destination.
    /// </summary>
    public sealed class RevisionTarget
    {
        public int UnitId;
        public int PresentMana;
    }

    /// <summary>
    /// A validated intervention injected before the selected tick decides. It is deliberately
    /// integer/id-only so it can cross the run/sim boundary without Unity or content objects.
    /// </summary>
    public sealed class TimelineIntervention
    {
        public int BranchTick;
        public RevisionEffectKind Kind;
        public RevisionModifier Modifiers;
        public List<RevisionTarget> Targets = new List<RevisionTarget>();

        public TimelineIntervention Clone()
        {
            var clone = new TimelineIntervention
            {
                BranchTick = BranchTick,
                Kind = Kind,
                Modifiers = Modifiers,
            };
            foreach (var target in Targets)
                clone.Targets.Add(new RevisionTarget
                {
                    UnitId = target.UnitId,
                    PresentMana = target.PresentMana,
                });
            return clone;
        }
    }
}
