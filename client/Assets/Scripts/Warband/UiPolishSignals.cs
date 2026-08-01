using System;

internal enum UiFeedbackTone
{
    Preview,
    Neutral,
    Sand,
    Positive,
    Negative,
    Major,
}

/// <summary>
/// Presentation-only transaction vocabulary. Run rules still live in Warband.Run; this tells the
/// feedback layer which authored recipe, sound family, and haptic weight should present a result.
/// </summary>
internal enum UiTransactionKind
{
    None,
    BuyRecruit,
    BuyRank,
    BuyWeapon,
    BuyTrinket,
    BindInscription,
    BuyCapacity,
    RankChoice,
    Equip,
    Reforge,
    MusterSelect,
    MusterDeselect,
}

internal readonly struct UiFeedbackEvent
{
    public readonly UiPolishSignals.Cue Cue;
    public readonly string SourceId;
    public readonly string TargetId;
    public readonly string ResourceId;
    public readonly string GroupId;
    public readonly int Amount;
    public readonly UiFeedbackTone Tone;
    public readonly UiTransactionKind Transaction;

    public UiFeedbackEvent(UiPolishSignals.Cue cue, string sourceId, string targetId,
                           string resourceId, string groupId, int amount,
                           UiFeedbackTone tone, UiTransactionKind transaction)
    {
        Cue = cue;
        SourceId = sourceId ?? "";
        TargetId = targetId ?? "";
        ResourceId = resourceId ?? "";
        GroupId = groupId ?? "";
        Amount = amount;
        Tone = tone;
        Transaction = transaction;
    }
}

/// <summary>
/// Payload-bearing semantic UI feedback seam. The authoritative controller publishes the result;
/// motion, FX, audio, and haptics consume it without learning run rules.
/// </summary>
internal static class UiPolishSignals
{
    internal enum Cue
    {
        Reveal,
        Preview,
        Select,
        Tab,
        Confirm,
        Purchase,
        Reroll,
        Reward,
        RankUp,
        Route,
        Result,
        Attention,
        TooltipReveal,
        TooltipDismiss,
        Pin,
        Unpin,
        DrawerExpand,
        DrawerCollapse,
        SocketWake,
        ProjectedTarget,
        Error,
    }

    public static event Action<UiFeedbackEvent> Emitted;
    public static event Action<Cue> PreviewRequested;
    public static event Action<UiTransactionKind> TransactionPreviewRequested;

    public static void Emit(Cue cue, string sourceId = "", string targetId = "",
                            string resourceId = "", string groupId = "",
                            int amount = 0, UiFeedbackTone tone = UiFeedbackTone.Neutral,
                            UiTransactionKind transaction = UiTransactionKind.None) =>
        Emitted?.Invoke(new UiFeedbackEvent(cue, sourceId, targetId, resourceId, groupId,
            amount, tone, transaction));

    public static void Preview(Cue cue) => PreviewRequested?.Invoke(cue);
    public static void Preview(UiTransactionKind transaction) =>
        TransactionPreviewRequested?.Invoke(transaction);
}
