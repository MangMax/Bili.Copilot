// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Services.MomentNotifications;

internal enum MomentNoticeKind
{
    Dynamic,
    Video,
    Episode,
}

internal sealed record MomentNotice(
    ulong Id,
    long AuthorId,
    string Author,
    string Text,
    MomentNoticeKind Kind,
    string? MediaId = null,
    string? Cover = null,
    string? Avatar = null);

internal sealed record MomentScanPage(IReadOnlyList<MomentScanItem> Items, string? Offset, bool HasMore);

internal sealed record MomentScanItem(ulong Id, bool IsPinned, MomentNotice? Notice);

internal sealed record MomentScanResult(ulong Baseline, IReadOnlyList<MomentNotice> Notices);

internal static class MomentNotificationScan
{
    internal static async Task<MomentScanResult> ReadAsync(
        ulong? baseline,
        Func<string?, int, CancellationToken, Task<MomentScanPage>> readPage,
        CancellationToken cancellationToken)
    {
        var newest = baseline.GetValueOrDefault();
        var notices = new Dictionary<ulong, MomentNotice>();
        var offsets = new HashSet<string>(StringComparer.Ordinal);
        string? offset = null;

        for (var pageNumber = 1; ; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await readPage(offset, pageNumber, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var hasRegularItem = false;
            var hasNewRegularItem = false;
            foreach (var item in page.Items)
            {
                newest = Math.Max(newest, item.Id);
                if (!item.IsPinned)
                {
                    hasRegularItem = true;
                    hasNewRegularItem |= item.Id > baseline.GetValueOrDefault();
                }

                if (baseline.HasValue && item.Id > baseline.Value && item.Notice is { } notice)
                {
                    notices.TryAdd(item.Id, notice);
                }
            }

            if (!page.HasMore || (hasRegularItem && (!baseline.HasValue || !hasNewRegularItem)))
            {
                return new(newest, notices.Values.ToArray());
            }

            if (string.IsNullOrEmpty(page.Offset) || !offsets.Add(page.Offset))
            {
                throw new InvalidDataException("动态分页游标未推进。");
            }

            offset = page.Offset;
        }
    }
}
