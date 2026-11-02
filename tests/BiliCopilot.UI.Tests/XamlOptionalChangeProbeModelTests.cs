// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.Acceptance.XamlOptionalChanges;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// XAML 可选开关探针的纯逻辑：开关清单解析、期望解析与结果判定。
/// 探针本身是 WinUI 应用，这里只测试与 WinUI 无关、决定退出码的那部分。
/// </summary>
public class XamlOptionalChangeProbeModelTests
{
    [Fact]
    public void ParseSwitchSpec_None_ReturnsNoSwitches()
    {
        Assert.Empty(ProbeSwitches.Parse("none"));
        Assert.Empty(ProbeSwitches.Parse(null));
        Assert.Empty(ProbeSwitches.Parse("  "));
    }

    [Fact]
    public void ParseSwitchSpec_All_ReturnsAllFourInCanonicalOrder()
    {
        Assert.Equal(
            new[]
            {
                "DefaultStyleOptimizations",
                "DeferContextFlyoutInit",
                "IconNoGridOptimization",
                "OptimizeApplyStyles",
            },
            ProbeSwitches.Parse("all"));
    }

    [Fact]
    public void ParseSwitchSpec_SpecificNames_ReturnsCanonicalCasingInRequestedOrder()
    {
        Assert.Equal(
            new[] { "IconNoGridOptimization", "DefaultStyleOptimizations" },
            ProbeSwitches.Parse(" iconnogridoptimization , DefaultStyleOptimizations "));
    }

    [Fact]
    public void ParseSwitchSpec_UnknownName_ThrowsAndListsValidNames()
    {
        var error = Assert.Throws<ArgumentException>(() => ProbeSwitches.Parse("DefaultStyleOptimizations,Bogus"));

        Assert.Contains("Bogus", error.Message);
        Assert.Contains("OptimizeApplyStyles", error.Message);
    }

    [Fact]
    public void ParseSwitchSpec_DuplicateName_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProbeSwitches.Parse("OptimizeApplyStyles,OptimizeApplyStyles"));
    }

    [Fact]
    public void ParseExpectations_ParsesNameEqualsValueAndKeepsFirstEqualsSign()
    {
        var expectations = ProbeExpectations.Parse(new[] { " fontIconShape = TextBlock ", "gallery=1200x800" });

        Assert.Equal(new ProbeExpectation("fontIconShape", "TextBlock"), expectations[0]);
        Assert.Equal(new ProbeExpectation("gallery", "1200x800"), expectations[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fontIconShape")]
    [InlineData("=TextBlock")]
    public void ParseExpectations_InvalidFormat_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => ProbeExpectations.Parse(new[] { value }));
    }

    [Fact]
    public void ParseExpectations_DuplicateName_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProbeExpectations.Parse(new[] { "a=1", "a=2" }));
    }

    [Fact]
    public void Evaluate_AllMatchIgnoringValueCase_Succeeds()
    {
        var expectations = ProbeExpectations.Parse(new[] { "icon=textblock", "locked=TRUE" });
        var results = ProbeExpectations.Evaluate(
            expectations,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["icon"] = "TextBlock",
                ["locked"] = "true",
            });

        Assert.All(results, r => Assert.True(r.Passed));
        Assert.DoesNotContain(results, r => r.Actual == "<missing>");
    }

    [Fact]
    public void Evaluate_Mismatch_ReportsExpectedAndActualAndFails()
    {
        var results = ProbeExpectations.Evaluate(
            ProbeExpectations.Parse(new[] { "icon=TextBlock" }),
            new Dictionary<string, string> { ["icon"] = "Grid" });

        var result = Assert.Single(results);
        Assert.False(result.Passed);
        Assert.Equal("TextBlock", result.Expected);
        Assert.Equal("Grid", result.Actual);
    }

    [Fact]
    public void Evaluate_FallsBackToCaseInsensitiveKeyLookup()
    {
        // An ordinal dictionary misses the exact key; the fallback must still
        // find ICON and compare its value case-insensitively.
        var results = ProbeExpectations.Evaluate(
            ProbeExpectations.Parse(new[] { "icon=textblock" }),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ICON"] = "TextBlock",
            });

        var result = Assert.Single(results);
        Assert.True(result.Passed);
        Assert.Equal("TextBlock", result.Actual);
    }

    [Fact]
    public void All_ReturnsReadOnlyList_SoCallersCannotRewriteTheCanonicalArray()
    {
        Assert.False(ProbeSwitches.All is string[]);
        Assert.Equal(4, ProbeSwitches.All.Count);
    }

    [Fact]
    public void Evaluate_MissingObservation_FailsWithMissingMarker()
    {
        var results = ProbeExpectations.Evaluate(
            ProbeExpectations.Parse(new[] { "templateBeforeTree=false" }),
            new Dictionary<string, string>());

        var result = Assert.Single(results);
        Assert.False(result.Passed);
        Assert.Equal("<missing>", result.Actual);
    }
}
