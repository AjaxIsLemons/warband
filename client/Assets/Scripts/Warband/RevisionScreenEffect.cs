using UnityEngine;
using Warband.Sim;

/// <summary>
/// Presentation-only state shared by RunShell's unscaled Revision ceremony and the URP fullscreen
/// pass. The sim never sees this type: it carries only how to draw the already-authoritative
/// replay folds.
///
/// One immutable value snapshot is read per rendered frame. Setters stay allocation-free so the
/// flagship moment does not trade a visual hitch for spectacle.
/// </summary>
public static class RevisionScreenEffect
{
    public readonly struct Frame
    {
        public readonly RevisionPresentationPhase Phase;
        public readonly float Progress;
        public readonly RevisionEffectKind Lineage;
        public readonly bool FullRupture;
        public readonly bool FinalChance;
        public readonly bool ReducedMotion;
        public readonly int CaptureRequest;
        public readonly int ResetVersion;
        public readonly Vector4 TargetViewportPositions;
        public readonly int TargetCount;
        public readonly RevisionPresentationTune Tune;

        public bool Active =>
            Phase == RevisionPresentationPhase.Opening ||
            Phase == RevisionPresentationPhase.Held ||
            Phase == RevisionPresentationPhase.Tear ||
            Phase == RevisionPresentationPhase.Rewind ||
            Phase == RevisionPresentationPhase.Vacuum ||
            Phase == RevisionPresentationPhase.Landing;

        internal Frame(
            RevisionPresentationPhase phase,
            float progress,
            RevisionEffectKind lineage,
            bool fullRupture,
            bool finalChance,
            bool reducedMotion,
            int captureRequest,
            int resetVersion,
            Vector4 targetViewportPositions,
            int targetCount,
            RevisionPresentationTune tune)
        {
            Phase = phase;
            Progress = progress;
            Lineage = lineage;
            FullRupture = fullRupture;
            FinalChance = finalChance;
            ReducedMotion = reducedMotion;
            CaptureRequest = captureRequest;
            ResetVersion = resetVersion;
            TargetViewportPositions = targetViewportPositions;
            TargetCount = targetCount;
            Tune = tune;
        }
    }

    private static readonly RevisionPresentationTune DefaultTune =
        new RevisionPresentationTune();

    private static RevisionPresentationPhase s_phase;
    private static float s_progress;
    private static RevisionEffectKind s_lineage;
    private static bool s_fullRupture;
    private static bool s_finalChance;
    private static bool s_reducedMotion;
    private static int s_nextCaptureRequest;
    private static int s_captureRequest;
    private static int s_resetVersion;
    private static Vector4 s_targetViewportPositions =
        new Vector4(0.49f, 0.46f, 0.49f, 0.46f);
    private static int s_targetCount;
    private static RevisionPresentationTune s_tune = DefaultTune;

    public static Frame Current => new Frame(
        s_phase,
        s_progress,
        s_lineage,
        s_fullRupture,
        s_finalChance,
        s_reducedMotion,
        s_captureRequest,
        s_resetVersion,
        s_targetViewportPositions,
        s_targetCount,
        s_tune ?? DefaultTune);

    public static void BeginOpening(
        RevisionEffectKind lineage,
        bool fullRupture,
        bool finalChance,
        bool reducedMotion,
        RevisionPresentationTune tune)
    {
        s_lineage = lineage;
        s_fullRupture = fullRupture;
        s_finalChance = finalChance;
        s_reducedMotion = reducedMotion;
        s_tune = tune ?? DefaultTune;
        s_phase = RevisionPresentationPhase.Opening;
        s_progress = 0f;
        s_captureRequest = 0;
        s_targetCount = 0;
        s_targetViewportPositions = new Vector4(0.49f, 0.46f, 0.49f, 0.46f);
        s_resetVersion++;
    }

    public static void SetPhase(RevisionPresentationPhase phase, float progress)
    {
        bool wasFutureBearing =
            s_phase == RevisionPresentationPhase.Tear ||
            s_phase == RevisionPresentationPhase.Rewind ||
            s_phase == RevisionPresentationPhase.Vacuum ||
            s_phase == RevisionPresentationPhase.Landing;
        s_phase = phase;
        s_progress = Mathf.Clamp01(progress);

        // The receipt is UI-only, and the run-up is ordinary live combat. Release the rejected
        // future as soon as the screen fault has handed attention on, instead of keeping a
        // full-screen texture alive through the tail.
        if ((phase == RevisionPresentationPhase.Receipt ||
             phase == RevisionPresentationPhase.RunUp) && wasFutureBearing)
        {
            s_captureRequest = 0;
            s_resetVersion++;
        }
    }

    /// <summary>
    /// Capture on the next game-camera pass. RunShell requests this only after restoring the exact
    /// witnessed present, so the retained texture is evidence rather than an inferred forecast.
    /// </summary>
    public static void RequestWitnessedFutureCapture()
    {
        s_nextCaptureRequest++;
        if (s_nextCaptureRequest == 0) s_nextCaptureRequest = 1;
        s_captureRequest = s_nextCaptureRequest;
    }

    public static void SetTargetViewportPositions(Vector4 positions, int count)
    {
        s_targetViewportPositions = new Vector4(
            Mathf.Clamp01(positions.x),
            Mathf.Clamp01(positions.y),
            Mathf.Clamp01(positions.z),
            Mathf.Clamp01(positions.w));
        s_targetCount = Mathf.Clamp(count, 0, 2);
    }

    public static void Clear()
    {
        s_phase = RevisionPresentationPhase.None;
        s_progress = 0f;
        s_captureRequest = 0;
        s_targetCount = 0;
        s_resetVersion++;
    }

#if UNITY_EDITOR
    /// <summary>Stable seam used by the Revision Fracture Lab and remote verification.</summary>
    public static void DebugBegin(
        RevisionEffectKind lineage,
        bool fullRupture,
        bool reducedMotion,
        RevisionPresentationTune tune = null)
    {
        BeginOpening(lineage, fullRupture, false, reducedMotion, tune);
    }
#endif
}
