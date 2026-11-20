// Copyright (c) Bili Copilot. All rights reserved.

using Bilibili.App.Dynamic.V2;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using System.Globalization;
using System.Text.Json;

namespace BiliCopilot.UI.Services.MomentNotifications;

internal sealed class MomentNotificationSource(
    BiliHttpClient http,
    BiliAuthenticator authenticator,
    IBiliCookiesResolver cookies,
    IRelationshipService relationships,
    IMomentDiscoveryService moments)
{
    private const string WebApi = "https://api.bilibili.com/x/polymer/web-dynamic/v1/";
    private const string AllFollowedAuthorsGroup = "-20";

    internal bool HasWebSession(long accountId)
    {
        var session = cookies.GetCookies();
        return session is not null && session.TryGetValue("SESSDATA", out var value) && !string.IsNullOrWhiteSpace(value)
            && session.TryGetValue("DedeUserID", out var mid) && long.TryParse(mid, out var user) && user == accountId;
    }

    internal async Task<Dictionary<long, string>> GetAuthorsAsync(long accountId, CancellationToken cancellationToken)
    {
        var authors = new Dictionary<long, string>();
        var page = 0;
        while (true)
        {
            using var timeout = CreateRequestTimeout(cancellationToken);
            var (users, nextPage) = await relationships.GetMyFollowUserGroupDetailAsync(AllFollowedAuthorsGroup, page, timeout.Token).ConfigureAwait(false);
            if (users.Count == 0)
            {
                authors.Remove(accountId);
                return authors;
            }

            var previousCount = authors.Count;
            foreach (var user in users)
            {
                if (user.Profile?.User is { } profile && long.TryParse(profile.Id, out var id) && id > 0)
                {
                    authors.TryAdd(id, profile.Name);
                }
            }

            if (nextPage <= page || authors.Count == previousCount)
            {
                throw new InvalidDataException("关注列表分页未推进。");
            }

            page = nextPage;
        }
    }

    internal async Task<MomentScanPage> ReadAuthorAsync(long authorId, string author, string? offset, int page, CancellationToken cancellationToken)
    {
        using var timeout = CreateRequestTimeout(cancellationToken);
        using var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Community.DynamicAllPersonal), new DynAllPersonalReq
        {
            HostUid = authorId,
            Offset = offset ?? string.Empty,
            Page = page,
            IsPreload = 1,
            LocalTime = BasicExtensions.GetLocalTimeZoneNumber(),
        });
        authenticator.AuthorizeGrpcRequest(request, requireToken: true);
        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        var result = await BiliHttpClient.ParseAsync(response, DynAllPersonalReply.Parser).ConfigureAwait(false);
        var items = new List<MomentScanItem>();
        foreach (var item in result.List)
        {
            if (item.CardType is DynamicType.DynNone or DynamicType.Fold or DynamicType.Ad or DynamicType.Banner
                || !ulong.TryParse(item.Extend?.DynIdStr, out var id) || id == 0)
            {
                continue;
            }

            var pinned = item.Modules.Any(module => module.ModuleTopTag?.TagName == "置顶");
            items.Add(new(id, pinned, GetNotice(item, id, authorId, author)));
        }

        return new(items, result.Offset, result.HasMore);
    }

    internal async Task<(MomentScanResult Result, IReadOnlyList<MomentInformation> FirstPage)> ScanVideosAsync(
        ulong? baseline, CancellationToken cancellationToken)
    {
        string? updateBaseline = null;
        IReadOnlyList<MomentInformation> firstPage = [];
        var result = await MomentNotificationScan.ReadAsync(baseline, async (offset, page, token) =>
        {
            using var timeout = CreateRequestTimeout(token);
            var view = await moments.GetVideoMomentsAsync(offset, updateBaseline, timeout.Token).ConfigureAwait(false);
            if (page == 1)
            {
                updateBaseline = view.UpdateBaseline;
                firstPage = view.Moments;
            }

            return MapVideoPage(view);
        }, cancellationToken).ConfigureAwait(false);
        return (result, firstPage);
    }

    private static MomentScanPage MapVideoPage(MomentView view)
    {
        var items = new List<MomentScanItem>();
        foreach (var moment in view.Moments)
        {
            if (!ulong.TryParse(moment.Id, out var id) || id == 0)
            {
                continue;
            }

            var author = moment.User?.Name ?? string.Empty;
            var avatar = moment.User?.Avatar?.Uri?.ToString();
            MomentNotice? notice = null;
            if (moment.Data is VideoInformation video)
            {
                var media = video.Identifier;
                var authorId = long.TryParse(moment.User?.Id, out var mid) ? mid : 0;
                notice = new(id, authorId, author, media.Title ?? string.Empty, MomentNoticeKind.Video,
                    media.Id, media.Cover?.Uri?.ToString(), avatar);
            }
            else if (moment.Data is EpisodeInformation episode)
            {
                var media = episode.Identifier;
                var episodeId = long.TryParse(media.Id, out var value) && value > 0 ? media.Id : null;
                notice = new(id, 0, author, media.Title ?? string.Empty, MomentNoticeKind.Episode,
                    episodeId, media.Cover?.Uri?.ToString(), avatar);
            }

            items.Add(new(id, false, notice));
        }

        return new(items, view.Offset, view.HasMoreMoments == true);
    }

    internal async Task<bool> HasUpdatesAsync(ulong baseline, CancellationToken cancellationToken)
    {
        using var document = await ReadWebAsync("feed/all/update", new()
        {
            ["type"] = "all",
            ["update_baseline"] = baseline.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("data").GetProperty("update_num").GetInt64() > 0;
    }

    internal async Task<Dictionary<long, bool>> GetAuthorUpdatesAsync(CancellationToken cancellationToken)
    {
        var updates = new Dictionary<long, bool>();
        var offsets = new HashSet<string>(StringComparer.Ordinal);
        string? offset = null;

        while (true)
        {
            using var document = offset is null
                ? await ReadWebAsync("portal", new() { ["up_list_more"] = "1" }, cancellationToken).ConfigureAwait(false)
                : await ReadWebAsync("uplist", new() { ["offset"] = offset, ["platform"] = "web" }, cancellationToken).ConfigureAwait(false);
            var data = document.RootElement.GetProperty("data");
            var page = offset is null ? data.GetProperty("up_list") : data;
            foreach (var item in page.GetProperty("items").EnumerateArray())
            {
                if (!item.TryGetProperty("mid", out var mid) || !long.TryParse(mid.ToString(), out var id) || id <= 0)
                {
                    continue;
                }

                if (!item.TryGetProperty("has_update", out var flag) || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    continue;
                }

                var hasUpdate = flag.GetBoolean();
                if (!updates.TryAdd(id, hasUpdate) && updates[id] != hasUpdate)
                {
                    throw new InvalidDataException("关注更新红点状态不一致。");
                }
            }

            if (!page.GetProperty("has_more").GetBoolean())
            {
                return updates;
            }

            offset = page.GetProperty("offset").GetString();
            if (string.IsNullOrWhiteSpace(offset) || !offsets.Add(offset))
            {
                throw new InvalidDataException("关注更新红点分页未推进。");
            }
        }
    }

    private async Task<JsonDocument> ReadWebAsync(string path, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        using var timeout = CreateRequestTimeout(cancellationToken);
        parameters.Add("web_location", "333.1365");
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(WebApi + path));
        request.Headers.Referrer = new("https://t.bilibili.com/");
        authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings
        {
            ApiType = BiliApiType.Web,
            RequireCookie = true,
            ForceNoToken = true,
        });
        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
    }

    private static MomentNotice? GetNotice(DynamicItem item, ulong id, long authorId, string author)
    {
        var authorInfo = item.Modules.FirstOrDefault(module => module.ModuleAuthor is not null)?.ModuleAuthor.Author;
        var forward = item.Modules.FirstOrDefault(module => module.ModuleAuthorForward is not null)?.ModuleAuthorForward;
        if ((authorInfo?.Mid ?? forward?.Uid) != authorId)
        {
            return null;
        }

        var content = item.Modules.FirstOrDefault(module => module.ModuleDynamic is not null)?.ModuleDynamic;
        var name = authorInfo?.Name ?? forward?.Title.FirstOrDefault()?.Text ?? author;
        if (item.CardType is not (DynamicType.Word or DynamicType.Draw or DynamicType.Forward or DynamicType.Article))
        {
            return null;
        }

        var text = item.Modules.FirstOrDefault(module => module.ModuleDesc is not null)?.ModuleDesc.Text;
        return new(id, authorId, name, content?.DynArticle?.Title ?? text ?? string.Empty, MomentNoticeKind.Dynamic,
            Avatar: authorInfo?.Face ?? forward?.FaceUrl);
    }

    private static CancellationTokenSource CreateRequestTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        return timeout;
    }
}
