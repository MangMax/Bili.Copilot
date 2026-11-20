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
    [InlineData("v2.2610.3", "2.2610.3.0")]
    [InlineData("2.2610.3.1", "2.2610.3.1")]
    [InlineData("latest", null)]
    [InlineData("v2.beta.1", null)]
    [InlineData("2.2610.3.0.1", null)]
    [InlineData("2.65536.3.0", null)]
    public void ParsePackageVersion_RequiresAnMsixVersion(string tag, string? expected)
        => Assert.Equal(expected, UpdateVersionPolicy.ParsePackageVersion(tag)?.ToString());
}
