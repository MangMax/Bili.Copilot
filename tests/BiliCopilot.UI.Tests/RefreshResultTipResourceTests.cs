// Copyright (c) Bili Copilot. All rights reserved.

using System.Xml.Linq;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 刷新结果提示条所用资源键的契约。
/// </summary>
/// <remarks>
/// 提示条通过 <c>{ext:Locale Name=...}</c> 与 <c>StringNames</c> 查资源，
/// 键名写错或缺键只会让按钮文案变空，不会让构建失败，因此在这里固定住。
/// </remarks>
public class RefreshResultTipResourceTests
{
    private static readonly string[] ResourceKeys =
    [
        "UndoRefresh",
        "RefreshResultAdded",
        "RefreshResultReloaded",
    ];

    [Fact]
    public void Resources_ContainTipText()
    {
        var resources = ResourcesFileHelper.LoadResources();

        foreach (var key in ResourceKeys)
        {
            var entries = resources
                .Where(data => (string?)data.Attribute("name") == key)
                .ToList();

            var entry = Assert.Single(entries);
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Element("value")?.Value),
                $"资源键 {key} 的文案为空.");
        }
    }

    [Fact]
    public void RefreshResultAdded_UsesAddedCountPlaceholder()
    {
        var value = GetValue(ResourcesFileHelper.LoadResources(), "RefreshResultAdded");

        Assert.Contains("{0}", value, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshResultReloaded_DoesNotMentionAddedCount()
    {
        var value = GetValue(ResourcesFileHelper.LoadResources(), "RefreshResultReloaded");

        Assert.DoesNotContain("{0}", value, StringComparison.Ordinal);
    }

    private static string GetValue(IReadOnlyList<XElement> resources, string key)
        => resources
            .Single(data => (string?)data.Attribute("name") == key)
            .Element("value")!
            .Value;
}
