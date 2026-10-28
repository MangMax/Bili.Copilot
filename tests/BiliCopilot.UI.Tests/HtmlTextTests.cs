// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class HtmlTextTests
{
    [Fact]
    public void Decode_DecodesAmpersandEntity()
        => Assert.Equal("Rock & Roll", HtmlText.Decode("Rock &amp; Roll"));

    [Theory]
    [InlineData("1 &lt; 2", "1 < 2")]
    [InlineData("1 &gt; 0", "1 > 0")]
    [InlineData("&quot;引用&quot;", "\"引用\"")]
    [InlineData("It&#39;s fine", "It's fine")]
    [InlineData("&#x4E2D;&#x6587;", "中文")]
    [InlineData("&#20013;&#25991;", "中文")]
    public void Decode_DecodesSingleEntity(string input, string expected)
        => Assert.Equal(expected, HtmlText.Decode(input));

    [Fact]
    public void Decode_DecodesMixedText()
        => Assert.Equal(
            "A & B < C \"D\" 'E' 中",
            HtmlText.Decode("A &amp; B &lt; C &quot;D&quot; &#39;E&#39; &#x4E2D;"));

    [Theory]
    [InlineData("普通标题")]
    [InlineData("【4K】演唱会 2026")]
    [InlineData("100% 原声 & 现场")]
    public void Decode_LeavesPlainTextUnchanged(string input)
        => Assert.Equal(input, HtmlText.Decode(input));

    [Fact]
    public void Decode_ReturnsEmptyForEmptyString()
        => Assert.Equal(string.Empty, HtmlText.Decode(string.Empty));

    [Fact]
    public void Decode_ReturnsEmptyForNull()
        => Assert.Equal(string.Empty, HtmlText.Decode(null));
}
