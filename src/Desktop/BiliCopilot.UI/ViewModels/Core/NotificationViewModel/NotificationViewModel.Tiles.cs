// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class NotificationViewModel
{
    private void UpdateMomentTiles(IEnumerable<MomentInformation> moments)
    {
        if (!_isTileSupport)
        {
            return;
        }

        var updater = TileUpdateManager.CreateTileUpdaterForApplication();
        foreach (var moment in moments)
        {
            var media = moment.Data switch
            {
                VideoInformation video => video.Identifier,
                EpisodeInformation episode => episode.Identifier,
                _ => default,
            };

            if (media.Cover?.SourceUri is not { } coverUri || moment.User?.Avatar?.Uri is not { } avatarUri)
            {
                continue;
            }

            var cover = SecurityElement.Escape(coverUri.ToString());
            var avatar = SecurityElement.Escape(avatarUri.ToString());
            var title = SecurityElement.Escape(media.Title);
            var publisher = SecurityElement.Escape(moment.User.Name);
            var xmlContent = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <tile>
                    <visual>
                        <binding template="TileMedium" branding="name">
                            <image src="{cover}" placement="background" hint-overlay="60" />
                            <image src="{avatar}" placement="peek" hint-crop="circle" />
                            <text hint-style="caption" hint-wrap="true">{title}</text>
                        </binding>
                        <binding template="TileWide" branding="nameAndLogo">
                            <image src="{cover}" placement="background" hint-overlay="60" />
                            <text hint-maxLines="2" hint-style="base" hint-wrap="true">{title}</text>
                            <text hint-style="caption">{publisher}</text>
                        </binding>
                        <binding template="TileLarge" branding="nameAndLogo">
                            <image src="{cover}" placement="background" hint-overlay="60" />
                            <text hint-style="subtitle" hint-wrap="true">{title}</text>
                            <text hint-style="base"></text>
                            <text hint-style="base">{publisher}</text>
                        </binding>
                    </visual>
                </tile>
                """;
            var xml = new XmlDocument();
            xml.LoadXml(xmlContent);
            updater.Update(new TileNotification(xml) { Tag = moment.Id });
        }
    }
}
