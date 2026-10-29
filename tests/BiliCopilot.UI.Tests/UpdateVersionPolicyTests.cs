// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class UpdateVersionPolicyTests
{
    [Theory]
    [InlineData("v2.2610.3.0", "2.2610.3.0")]
    [InlineData("V2.2610.3.0", "2.2610.3.0")]
    [InlineData("  v2.2610.3.0  ", "2.2610.3.0")]
    [InlineData("2.2610.3.0", "2.2610.3.0")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_StripsPrefixAndWhitespace(string? input, string expected)
        => Assert.Equal(expected, UpdateVersionPolicy.Normalize(input));

    [Theory]
    [InlineData("2.2610.2.0", "2.2610.3.0", true)]
    [InlineData("2.2610.3.0", "2.2610.3.0", false)]
    [InlineData("2.2610.3.0", "2.2609.9.9", false)]
    [InlineData("2.2610.3.0", "v2.2610.3.1", true)]
    [InlineData("2.2610.3.0", "2.2610.3", false)]
    [InlineData("2.2610.3", "2.2610.3.0", false)]
    [InlineData("2.2610.3.0", "2.2610.3.0.1", true)]
    public void Compare_TreatsMissingSegmentsAsZero(string local, string remote, bool expectedNewer)
        => Assert.Equal(expectedNewer, UpdateVersionPolicy.IsRemoteNewer(local, remote));

    [Fact]
    public void Compare_WithNonNumericRemote_IsNotNewer()
        => Assert.False(UpdateVersionPolicy.IsRemoteNewer("2.2610.3.0", "latest"));

    [Fact]
    public void Compare_WithEmptyRemote_IsNotNewer()
        => Assert.False(UpdateVersionPolicy.IsRemoteNewer("2.2610.3.0", string.Empty));

    [Fact]
    public void ParseParts_WithNonNumericSegment_TreatsItAsZero()
        => Assert.Equal([2, 0, 1], UpdateVersionPolicy.ParseParts("v2.beta.1"));
}
