using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Warband.Sim;

/// <summary>
/// A presentation-only comparison of the witnessed and committed folds at the branch. The receipt
/// never reconstructs combat rules: it only names state that the authoritative event streams prove.
/// </summary>
internal sealed class RevisionReceipt
{
    public string Title = "A NEW HOUR";
    public string Subtitle = "The branch has taken hold.";
    public List<string> Lines = new List<string>();
}

internal static class RevisionReceiptBuilder
{
    internal static RevisionReceipt Build(
        BattleResult witnessed,
        BattleResult revised,
        int branchTick,
        RevisionEffectKind effect)
    {
        var receipt = new RevisionReceipt
        {
            Title = effect == RevisionEffectKind.BorrowedFuture
                ? "FUTURE BORROWED"
                : "FORMATION RECALLED",
            Subtitle = $"NEW REALITY · {branchTick / 10f:0.0}s",
        };
        if (witnessed == null || revised == null)
        {
            receipt.Lines.Add("THE TIMELINE SPLIT");
            return receipt;
        }

        PlaybackState before = Fold(witnessed, branchTick);
        PlaybackState after = Fold(revised, branchTick);
        List<int> revisedIds = revised.Events
            .Where(e => e.Tick == branchTick && e.Kind == EventKind.RevisionApplied)
            .Select(e => e.Target)
            .Distinct()
            .ToList();

        foreach (int id in revisedIds)
        {
            PlaybackUnit oldUnit = before.ById(id);
            PlaybackUnit newUnit = after.ById(id);
            if (oldUnit == null || newUnit == null) continue;
            if (effect == RevisionEffectKind.BorrowedFuture)
                AddBorrowed(receipt.Lines, oldUnit, newUnit);
            else
                AddRecall(receipt.Lines, oldUnit, newUnit);
        }

        if (receipt.Lines.Count == 0)
            receipt.Lines.Add(effect == RevisionEffectKind.BorrowedFuture
                ? "FUTURE MANA CROSSED THE SPLIT"
                : "ENEMY FORMATION WAS REWRITTEN");
        if (receipt.Lines.Count > 4)
            receipt.Lines.RemoveRange(4, receipt.Lines.Count - 4);
        return receipt;
    }

    private static PlaybackState Fold(BattleResult result, int tick)
    {
        PlaybackState fold = PlaybackState.From(result.InitialUnits, result.RuleIds);
        fold.AdvanceToTick(result.Events, tick);
        return fold;
    }

    private static void AddBorrowed(
        ICollection<string> lines,
        PlaybackUnit before,
        PlaybackUnit after)
    {
        var changes = new List<string>();
        int mana = after.Mana - before.Mana;
        int shield = after.Shield - before.Shield;
        if (mana > 0) changes.Add($"+{mana} MANA");
        if (shield > 0) changes.Add($"+{shield} SHIELD");
        if (Has(before, StatusKind.Silence) && !Has(after, StatusKind.Silence))
            changes.Add("SILENCE BROKEN");
        if (Has(before, StatusKind.Disarm) && !Has(after, StatusKind.Disarm))
            changes.Add("DISARM BROKEN");
        if (changes.Count > 0)
            lines.Add($"{before.Name.ToUpperInvariant()}  ·  {string.Join("  ·  ", changes)}");
    }

    private static void AddRecall(
        ICollection<string> lines,
        PlaybackUnit before,
        PlaybackUnit after)
    {
        var changes = new List<string>();
        if (!before.Pos.Equals(after.Pos)) changes.Add("RETURNED TO DEPLOYMENT");
        if (!Has(before, StatusKind.Disarm) && Has(after, StatusKind.Disarm))
            changes.Add("DISARMED");
        if (!Has(before, StatusKind.Root) && Has(after, StatusKind.Root))
            changes.Add("ROOTED");
        if (!Has(before, StatusKind.Omitted) && Has(after, StatusKind.Omitted))
            changes.Add("OMITTED");
        int mana = before.Mana - after.Mana;
        if (mana > 0) changes.Add($"−{mana} MANA");
        if (changes.Count > 0)
            lines.Add($"{before.Name.ToUpperInvariant()}  ·  {string.Join("  ·  ", changes)}");
    }

    private static bool Has(PlaybackUnit unit, StatusKind kind)
    {
        foreach (var status in unit.Statuses)
            if (status.Kind == kind) return true;
        return false;
    }
}
