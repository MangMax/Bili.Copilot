// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐视频增量刷新设置项的持久化与文案契约。
/// </summary>
/// <remarks>
/// 设置值以枚举成员名（<c>SettingNames.ToString()</c>）作为本地设置的键；
/// 卡片标题与说明则通过 <c>{ext:Locale Name=...}</c> 查资源文件。
/// 键名写错或资源键缺失都不会让构建失败，只会让开关状态静默丢失、卡片标题变空，
/// 因此在这里固定住键名，并校验资源文件里确实有这两个键且没有重复。
/// </remarks>
public class IncrementalRecommendRefreshSettingTests
{
    private static readonly string[] ResourceKeys =
    [
        "IncrementalRecommendRefresh",
        "IncrementalRecommendRefreshDescription",
    ];

    [Fact]
    public void SettingName_PersistedKeyIsStable()
    {
        Assert.Equal(
            "IncrementalRecommendRefresh",
            SettingNames.IncrementalRecommendRefresh.ToString());
    }

    [Fact]
    public void Resources_ContainNonEmptySettingText()
    {
        var resources = ResourcesFileHelper.LoadResources();

        foreach (var key in ResourceKeys)
        {
            var entries = resources
                .Where(data => (string?)data.Attribute("name") == key)
                .ToList();

            // 重复键会让 XAML 资源查找行为不确定，追加到长文件时尤其容易发生.
            var entry = Assert.Single(entries);
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Element("value")?.Value),
                $"资源键 {key} 的文案为空.");
        }
    }
}
