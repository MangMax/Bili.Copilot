// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Services.MomentNotifications;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class NotificationViewModel
{
    private const string MomentGroupPrefix = "moments-";

    private async Task CheckMomentsAsync(long accountId, bool videoEnabled, bool otherDynamicsEnabled, CancellationToken cancellationToken)
    {
        var path = StatePath(accountId);
        var state = LoadState(path);
        var webAvailable = _source.HasWebSession(accountId);
        var fresh = new List<MomentNotice>();

        if (otherDynamicsEnabled)
        {
            try
            {
                fresh.AddRange(await ScanAuthorsAsync(accountId, state, webAvailable, cancellationToken));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "获取关注列表失败，本轮保留原有扫描位置。");
            }
        }

        if (videoEnabled)
        {
            try
            {
                var (scan, firstPage) = await _source.ScanVideosAsync(state.VideoBaseline, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                state.VideoBaseline = scan.Baseline;
                fresh.AddRange(scan.Notices.Where(notice => notice.AuthorId != accountId));
                UpdateMomentTiles(firstPage.Take(5));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "检查视频动态或更新动态磁贴失败。");
            }
        }

        Dictionary<long, bool> redDots = [];
        if (webAvailable && (fresh.Count > 0 || state.Active.Count > 0 || state.Summary.Count > 0 || state.Queue.Count > 0))
        {
            try
            {
                redDots = await _source.GetAuthorUpdatesAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "作者红点快照不可用，保留现有通知。");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (AccountId != accountId)
        {
            return;
        }

        bool InScope(MomentNoticeKind kind) => kind == MomentNoticeKind.Dynamic ? otherDynamicsEnabled : videoEnabled;
        var manager = AppNotificationManager.Default;
        if (manager.Setting == AppNotificationSetting.Enabled)
        {
            state.PrepareDelivery(fresh, InScope, redDots);
        }
        else
        {
            state.Queue.Clear();
        }

        SaveState(path, state);
        await ReconcileAndDeliverAsync(manager, accountId, path, state, InScope, redDots, cancellationToken);
    }

    private async Task<IReadOnlyList<MomentNotice>> ScanAuthorsAsync(
        long accountId, MomentNotificationState state, bool webAvailable, CancellationToken cancellationToken)
    {
        var authors = await _source.GetAuthorsAsync(accountId, cancellationToken);
        var scanAll = true;
        var now = DateTimeOffset.UtcNow;
        if (webAvailable && state.ProbeBaseline is { } probe && authors.Count > 0
            && state.LastFullAuthorScan is { } last && now - last < TimeSpan.FromHours(1))
        {
            try
            {
                scanAll = await _source.HasUpdatesAsync(probe, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "动态更新检查不可用，改为扫描关注作者。");
            }
        }

        var retained = state.Authors.Where(pair => authors.ContainsKey(pair.Key)).ToDictionary();
        var targets = authors.Where(author => scanAll || !retained.ContainsKey(author.Key)).ToArray();
        var results = new ConcurrentDictionary<long, MomentScanResult>();
        await Parallel.ForEachAsync(targets, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken,
        }, async (author, token) =>
        {
            try
            {
                var baseline = retained.TryGetValue(author.Key, out var current) ? (ulong?)current : null;
                results[author.Key] = await MomentNotificationScan.ReadAsync(baseline,
                    (offset, page, ct) => _source.ReadAuthorAsync(author.Key, author.Value, offset, page, ct), token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "获取作者 {AuthorId} 的动态失败，保留该作者的扫描位置。", author.Key);
            }
        });

        if (results.Count != targets.Length)
        {
            state.ProbeBaseline = null;
        }
        else if (scanAll)
        {
            state.ProbeBaseline = retained.Values.DefaultIfEmpty().Max();
            state.LastFullAuthorScan = now;
        }

        foreach (var (authorId, scan) in results)
        {
            retained[authorId] = scan.Baseline;
        }

        state.Authors = retained;
        return results.Values.SelectMany(result => result.Notices).ToArray();
    }

    private static MomentNotificationState LoadState(string path)
        => File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllText(path), GlobalSerializeContext.Default.MomentNotificationState) ?? new()
            : new();

    private static void SaveState(string path, MomentNotificationState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, GlobalSerializeContext.Default.MomentNotificationState));
        File.Move(temporary, path, overwrite: true);
    }

    private static string StatePath(long accountId)
        => Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path, "MomentNotifications",
            accountId.ToString(CultureInfo.InvariantCulture) + ".json");

    private static string NotificationGroup(long accountId)
        => MomentGroupPrefix + accountId.ToString(CultureInfo.InvariantCulture);

    private async Task ApplyNotificationScopeAsync(long accountId, bool videoEnabled, bool otherDynamicsEnabled, CancellationToken cancellationToken)
    {
        var path = StatePath(accountId);
        var state = LoadState(path);
        bool InScope(MomentNoticeKind kind) => kind == MomentNoticeKind.Dynamic ? otherDynamicsEnabled : videoEnabled;
        await ReconcileAndDeliverAsync(AppNotificationManager.Default, accountId, path, state, InScope,
            new Dictionary<long, bool>(), cancellationToken, deliverQueued: false);
    }

    private static void ResetDisabledState(long accountId, bool videoEnabled, bool otherDynamicsEnabled)
    {
        var path = StatePath(accountId);
        if (!File.Exists(path) || (videoEnabled && otherDynamicsEnabled))
        {
            return;
        }

        if (!videoEnabled && !otherDynamicsEnabled)
        {
            File.Delete(path);
            return;
        }

        var state = LoadState(path);
        if (!videoEnabled)
        {
            state.VideoBaseline = null;
        }

        if (!otherDynamicsEnabled)
        {
            state.Authors.Clear();
            state.ProbeBaseline = null;
            state.LastFullAuthorScan = null;
        }

        state.PrepareDelivery([], kind => kind == MomentNoticeKind.Dynamic ? otherDynamicsEnabled : videoEnabled,
            new Dictionary<long, bool>());
        SaveState(path, state);
    }
}
