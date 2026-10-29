// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class TimestampTextTests
{
    [Theory]
    [InlineData("00:00", 0d)]
    [InlineData("0:00", 0d)]
    [InlineData("01:23", 83d)]
    [InlineData("59:59", 3599d)]
    [InlineData("1:02:03", 3723d)]
    [InlineData("12:34:56", 45296d)]
    public void Split_RecognizesTimestampAndComputesSeconds(string input, double expectedSeconds)
    {
        var segment = Assert.Single(TimestampText.Split(input));

        Assert.True(segment.IsTimestamp);
        Assert.Equal(input, segment.Text);
        Assert.Equal(expectedSeconds, segment.Seconds);
    }

    [Theory]
    [InlineData("1:60")]
    [InlineData("60:00")]
    [InlineData("1:60:03")]
    public void Split_OutOfRangeTimestamp_RemainsPlainText(string input)
    {
        var segment = Assert.Single(TimestampText.Split(input));

        Assert.False(segment.IsTimestamp);
        Assert.Null(segment.Seconds);
        Assert.Equal(input, segment.Text);
    }

    [Theory]
    [InlineData("123:45")]
    [InlineData("1:234")]
    [InlineData("001:23")]
    [InlineData("12:345")]
    public void Split_TimestampGluedToDigits_RemainsPlainText(string input)
    {
        var segment = Assert.Single(TimestampText.Split(input));

        Assert.False(segment.IsTimestamp);
        Assert.Equal(input, segment.Text);
    }

    [Fact]
    public void Split_MultipleTimestamps_AreReturnedInOrderAndPreserveOriginalText()
    {
        const string input = "开头 00:00，中段 01:23，结尾 1:02:03。";

        var segments = TimestampText.Split(input);

        Assert.Equal(
            new[] { "开头 ", "00:00", "，中段 ", "01:23", "，结尾 ", "1:02:03", "。" },
            segments.Select(p => p.Text));
        Assert.Equal(
            new double?[] { null, 0d, null, 83d, null, 3723d, null },
            segments.Select(p => p.Seconds));
        Assert.Equal(input, string.Concat(segments.Select(p => p.Text)));
    }

    [Fact]
    public void Split_TimestampInsidePlainText_KeepsSurroundingTextVerbatim()
    {
        const string input = "从这里（01:23）开始\n看下一行";

        var segments = TimestampText.Split(input);

        Assert.Equal(
            new[] { "从这里（", "01:23", "）开始\n看下一行" },
            segments.Select(p => p.Text));
        Assert.Equal(83d, segments[1].Seconds);
    }

    [Fact]
    public void Split_PlainText_ReturnsSinglePlainSegment()
    {
        const string input = "没有时间戳的普通文本";

        var segment = Assert.Single(TimestampText.Split(input));

        Assert.False(segment.IsTimestamp);
        Assert.Null(segment.Seconds);
        Assert.Equal(input, segment.Text);
    }

    [Fact]
    public void Split_Null_ReturnsEmptyList()
        => Assert.Empty(TimestampText.Split(null));

    [Fact]
    public void Split_EmptyString_ReturnsEmptyList()
        => Assert.Empty(TimestampText.Split(string.Empty));
}
