namespace Warband.Sim
{
    /// <summary>
    /// The render layer keys "tells" (the visual/audio feedback for an event) on more than
    /// <see cref="EventKind"/> alone — a Burn tick should read differently from a sword hit, and
    /// each status wants its own pip burst. A tell rule declares an EventKind plus OPTIONAL
    /// <see cref="Cause"/> / <see cref="StatusKind"/> filters; the most specific matching rule
    /// wins (so a generic "Damage" tell is a fallback and "Damage/Burn" overrides it for burns).
    /// This is the client's dispatch brain, kept here so it's headless-testable and shares the
    /// event model with the sim (no drift). The Unity FeedbackDirector is a thin executor over it.
    /// </summary>
    public static class TellMatch
    {
        /// <summary>Does a rule (kind + optional cause/status filters) match this event?</summary>
        public static bool Matches(BattleEvent e, EventKind kind, Cause? cause, StatusKind? status)
        {
            if (e.Kind != kind) return false;
            if (cause.HasValue && e.Cause != cause.Value) return false;
            if (status.HasValue)
            {
                bool isStatusEvent = e.Kind == EventKind.StatusApplied || e.Kind == EventKind.StatusExpired;
                if (!isStatusEvent || (StatusKind)e.Aux != status.Value) return false;
            }
            return true;
        }

        /// <summary>How specific a rule is — more declared filters win ties toward the narrower
        /// rule. Kind is always required so it isn't counted.</summary>
        public static int Specificity(Cause? cause, StatusKind? status)
            => (cause.HasValue ? 1 : 0) + (status.HasValue ? 1 : 0);
    }
}
