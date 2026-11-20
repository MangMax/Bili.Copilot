// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Services.MomentNotifications;

internal sealed class MomentNotificationState
{
    private const int MaxDetailNotifications = 3;

    public Dictionary<long, ulong> Authors { get; set; } = [];

    public ulong? ProbeBaseline { get; set; }

    public ulong? VideoBaseline { get; set; }

    public DateTimeOffset? LastFullAuthorScan { get; set; }

    public Dictionary<ulong, MomentNotificationTracking> Active { get; set; } = [];

    public Dictionary<ulong, MomentNotificationTracking> Summary { get; set; } = [];

    public List<MomentQueuedNotice> Queue { get; set; } = [];

    internal void PrepareDelivery(
        IEnumerable<MomentNotice> fresh, Func<MomentNoticeKind, bool> inScope, IReadOnlyDictionary<long, bool> redDots)
    {
        Queue.RemoveAll(queued => !inScope(queued.Notice.Kind)
            || (queued.SawRedDot && IsRedDotOff(queued.Notice.AuthorId, redDots)));
        var queuedIds = Queue.Select(queued => queued.Notice.Id).ToHashSet();
        var detailCount = Queue.Count(queued => !queued.InSummary);
        foreach (var notice in fresh.OrderByDescending(notice => notice.Id))
        {
            if (inScope(notice.Kind) && !Active.ContainsKey(notice.Id) && !Summary.ContainsKey(notice.Id) && queuedIds.Add(notice.Id))
            {
                Queue.Add(new(notice, detailCount >= MaxDetailNotifications, redDots.GetValueOrDefault(notice.AuthorId)));
                detailCount++;
            }
        }
    }

    internal static bool IsRedDotOff(long authorId, IReadOnlyDictionary<long, bool> redDots)
        => redDots.TryGetValue(authorId, out var updated) && !updated;
}

internal sealed record MomentNotificationTracking(long AuthorId, MomentNoticeKind Kind, bool SawRedDot);

internal sealed record MomentQueuedNotice(MomentNotice Notice, bool InSummary, bool SawRedDot);
