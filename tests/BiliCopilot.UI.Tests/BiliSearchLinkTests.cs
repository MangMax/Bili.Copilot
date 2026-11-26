// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class BiliSearchLinkTests
{
    [Theory]
    [InlineData("  https://www.bilibili.com/video/BV1xx411c7mD/?p=2#share  ", "Video", "BV1xx411c7mD")]
    [InlineData("m.bilibili.com/video/av123", "Video", "123")]
    [InlineData("https://www.bilibili.com/bangumi/play/ep123", "Episode", "123")]
    [InlineData("https://www.bilibili.com/bangumi/play/ss456", "Season", "456")]
    [InlineData("https://www.bilibili.com/read/cv123?from=share", "Article", "123")]
    [InlineData("//live.bilibili.com/123/?from=share", "Live", "123")]
    [InlineData("http://live.bilibili.com/blanc/456", "Live", "456")]
    [InlineData("b23.tv/Ab123Cd", "Short", "Ab123Cd")]
    public void RecognizesContentLinks(string input, string kind, string id)
    {
        var result = Assert.IsType<BiliSearchLink>(BiliSearchLink.Parse(input));
        Assert.Equal(kind, result.Kind.ToString());
        Assert.Equal(id, result.Id);
    }

    [Theory]
    [InlineData("视频 搜索")]
    [InlineData("BV1xx411c7mD")]
    [InlineData("https://www.bilibili.com.example.com/video/av123")]
    [InlineData("https://www.bilibili.com@other.example/video/av123")]
    [InlineData("https://other.example@www.bilibili.com/video/av123")]
    [InlineData("ftp://www.bilibili.com/video/av123")]
    [InlineData("https://www.bilibili.com/video/av123/other")]
    [InlineData("https://www.bilibili.com/read/cv0")]
    [InlineData("https://live.bilibili.com/")]
    [InlineData("https://live.bilibili.com/123abc")]
    public void LeavesOtherInputForKeywordSearch(string input)
        => Assert.Null(BiliSearchLink.Parse(input));
}
