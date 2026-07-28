using System;
using System.Collections.Generic;

internal enum UiNoticeScope
{
    Menu,
    Muster,
    Hall,
    Deployment,
}

internal enum UiNoticeTone
{
    Neutral,
    Positive,
    Error,
}

internal readonly struct UiNotice
{
    public static readonly UiNotice Empty =
        new UiNotice("", UiNoticeTone.Neutral, 0);

    public readonly string Text;
    public readonly UiNoticeTone Tone;
    public readonly long Revision;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    public bool IsError => Tone == UiNoticeTone.Error;

    public UiNotice(string text, UiNoticeTone tone, long revision)
    {
        Text = text ?? "";
        Tone = tone;
        Revision = revision;
    }
}

/// <summary>
/// Route-scoped presentation state. A transaction receipt can survive a rebuild of its owning
/// screen, but it cannot leak into Wager, Deployment, or the next Hall visit.
/// </summary>
internal sealed class UiNoticeStore
{
    private readonly Dictionary<UiNoticeScope, UiNotice> _notices =
        new Dictionary<UiNoticeScope, UiNotice>();
    private long _revision;

    public UiNotice Read(UiNoticeScope scope) =>
        _notices.TryGetValue(scope, out UiNotice notice)
            ? notice
            : UiNotice.Empty;

    public UiNotice Set(
        UiNoticeScope scope, string text,
        UiNoticeTone tone = UiNoticeTone.Neutral)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Clear(scope);
            return UiNotice.Empty;
        }
        var notice = new UiNotice(text.Trim(), tone, ++_revision);
        _notices[scope] = notice;
        return notice;
    }

    public void Clear(UiNoticeScope scope)
    {
        if (_notices.Remove(scope)) _revision++;
    }

    public void ClearAll()
    {
        if (_notices.Count == 0) return;
        _notices.Clear();
        _revision++;
    }
}
