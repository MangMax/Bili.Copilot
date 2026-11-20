// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Services.MomentNotifications;
using BiliCopilot.UI.Toolkits;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Globalization;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class NotificationViewModel
{
    private const string SummaryTag = "summary";

    private async Task ReconcileAndDeliverAsync(
        AppNotificationManager manager, long accountId, string path, MomentNotificationState state,
        Func<MomentNoticeKind, bool> inScope, IReadOnlyDictionary<long, bool> redDots, CancellationToken cancellationToken,
        bool deliverQueued = true)
    {
        var group = NotificationGroup(accountId);
        var notifications = await manager.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var foreignGroup in notifications.Select(notification => notification.Group)
            .Where(other => other != group && other.StartsWith(MomentGroupPrefix, StringComparison.Ordinal)).Distinct())
        {
            await manager.RemoveByGroupAsync(foreignGroup);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var activeTags = notifications.Where(notification => notification.Group == group)
            .Select(notification => notification.Tag).ToHashSet(StringComparer.Ordinal);
        bool ShouldRemove(MomentNotificationTracking tracking)
            => !inScope(tracking.Kind) || (tracking.SawRedDot
                && MomentNotificationState.IsRedDotOff(tracking.AuthorId, redDots));

        foreach (var (id, tracking) in state.Active.ToArray())
        {
            var tag = id.ToString(CultureInfo.InvariantCulture);
            if (!activeTags.Contains(tag))
            {
                state.Active.Remove(id);
            }
            else if (ShouldRemove(tracking))
            {
                await manager.RemoveByTagAndGroupAsync(tag, group);
                cancellationToken.ThrowIfCancellationRequested();
                state.Active.Remove(id);
                activeTags.Remove(tag);
            }
            else if (redDots.GetValueOrDefault(tracking.AuthorId))
            {
                state.Active[id] = tracking with { SawRedDot = true };
            }
        }

        var queuedNotices = deliverQueued ? state.Queue.ToArray() : [];
        foreach (var queued in queuedNotices.Where(queued => !queued.InSummary))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var notice = queued.Notice;
            var tag = notice.Id.ToString(CultureInfo.InvariantCulture);
            if (!activeTags.Contains(tag))
            {
                ShowDetail(manager, accountId, group, tag, notice);
            }

            state.Active[notice.Id] = Track(queued, redDots);
            state.Queue.Remove(queued);
            SaveState(path, state);
        }

        var previousSummaryCount = state.Summary.Count;
        var hasSummary = activeTags.Contains(SummaryTag);
        foreach (var (id, tracking) in state.Summary.ToArray())
        {
            if (!hasSummary || ShouldRemove(tracking))
            {
                state.Summary.Remove(id);
            }
            else if (redDots.GetValueOrDefault(tracking.AuthorId))
            {
                state.Summary[id] = tracking with { SawRedDot = true };
            }
        }

        var summaryNotices = queuedNotices.Where(queued => queued.InSummary).ToArray();
        foreach (var queued in summaryNotices)
        {
            state.Summary[queued.Notice.Id] = Track(queued, redDots);
        }

        var hasNewSummaryItems = summaryNotices.Length > 0;
        if (state.Summary.Count == 0 && hasSummary)
        {
            await manager.RemoveByTagAndGroupAsync(SummaryTag, group);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else if (hasNewSummaryItems || (hasSummary && state.Summary.Count != previousSummaryCount))
        {
            var summary = CreateBuilder(accountId, group, SummaryTag, "moments")
                .AddArgument("section", state.Summary.Values.All(tracking => tracking.Kind != MomentNoticeKind.Dynamic) ? "video" : "all")
                .AddText(ResourceToolkit.GetLocalizedString(StringNames.MomentNotificationSummary))
                .AddText(string.Format(CultureInfo.CurrentCulture,
                    ResourceToolkit.GetLocalizedString(StringNames.MomentNotificationSummaryCount), state.Summary.Count))
                .BuildNotification();
            summary.SuppressDisplay = !hasNewSummaryItems || (hasSummary && previousSummaryCount == 0);
            ShowNotification(manager, summary);
        }

        if (deliverQueued)
        {
            state.Queue.Clear();
        }

        SaveState(path, state);
    }

    private static MomentNotificationTracking Track(MomentQueuedNotice queued, IReadOnlyDictionary<long, bool> redDots)
        => new(queued.Notice.AuthorId, queued.Notice.Kind, queued.SawRedDot || redDots.GetValueOrDefault(queued.Notice.AuthorId));

    private static void ShowDetail(AppNotificationManager manager, long accountId, string group, string tag, MomentNotice notice)
    {
        var route = notice.Kind switch
        {
            MomentNoticeKind.Video when notice.MediaId is not null => "video",
            MomentNoticeKind.Episode when notice.MediaId is not null => "episode",
            _ => "moment",
        };
        var titleName = notice.Kind switch
        {
            MomentNoticeKind.Video => StringNames.NewVideoMomentNotification,
            MomentNoticeKind.Episode => StringNames.NewEpisodeMomentNotification,
            _ => StringNames.NewOtherMomentNotification,
        };
        var author = string.IsNullOrWhiteSpace(notice.Author)
            ? ResourceToolkit.GetLocalizedString(StringNames.MomentNotificationSummary)
            : notice.Author;
        var builder = CreateBuilder(accountId, group, tag, route)
            .AddArgument("id", tag)
            .AddText(string.Format(CultureInfo.CurrentCulture, ResourceToolkit.GetLocalizedString(titleName), author));
        if (notice.MediaId is not null)
        {
            builder.AddArgument("media", notice.MediaId);
        }

        if (!string.IsNullOrWhiteSpace(notice.Text))
        {
            var text = notice.Text;
            if (text.Length > 120)
            {
                text = text[..(char.IsHighSurrogate(text[119]) ? 119 : 120)] + "…";
            }

            builder.AddText(text);
        }

        if (Uri.TryCreate(notice.Cover, UriKind.Absolute, out var cover) && cover.Scheme is "https" or "http")
        {
            builder.SetHeroImage(cover);
        }

        if (Uri.TryCreate(notice.Avatar, UriKind.Absolute, out var avatar) && avatar.Scheme is "https" or "http")
        {
            builder.SetAppLogoOverride(avatar, AppNotificationImageCrop.Circle);
        }

        ShowNotification(manager, builder.BuildNotification());
    }

    private static AppNotificationBuilder CreateBuilder(long accountId, string group, string tag, string route)
        => new AppNotificationBuilder()
            .SetGroup(group)
            .SetTag(tag)
            .AddArgument("account", accountId.ToString(CultureInfo.InvariantCulture))
            .AddArgument("page", route);

    private static void ShowNotification(AppNotificationManager manager, AppNotification notification)
    {
        manager.Show(notification);
        if (notification.Id == 0)
        {
            throw new InvalidOperationException("Windows 未接收动态通知。");
        }
    }

    private static async Task ClearNotificationsAsync(long accountId)
    {
        await AppNotificationManager.Default.RemoveByGroupAsync(NotificationGroup(accountId));
    }
}
