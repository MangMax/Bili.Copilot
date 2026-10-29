// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class UpdateSourcePolicyTests
{
    private const string Url = "https://github.com/o/r/releases/download/v1/file.msix";

    [Fact]
    public void BuildCandidates_PutsDirectFirst()
    {
        var candidates = UpdateSourcePolicy.BuildCandidates(Url, ["https://gh-proxy.com"]);

        Assert.Equal(
            [Url, "https://gh-proxy.com/" + Url],
            candidates);
    }

    [Fact]
    public void BuildCandidates_PreservesMirrorOrder()
    {
        var candidates = UpdateSourcePolicy.BuildCandidates(
            Url,
            ["https://a.example", "https://b.example"]);

        Assert.Equal("https://b.example/" + Url, candidates[^1]);
    }

    [Fact]
    public void BuildCandidates_AddsTrailingSlashOnlyOnce()
        => Assert.Equal(
            ["https://a.example/" + Url],
            UpdateSourcePolicy.BuildCandidates(Url, ["https://a.example/"]).Skip(1));

    [Fact]
    public void BuildCandidates_SkipsNonHttpsAndBlankPrefixes()
    {
        var candidates = UpdateSourcePolicy.BuildCandidates(
            Url,
            [null, string.Empty, "  ", "http://insecure.example", "ftp://x"]);

        Assert.Single(candidates);
    }

    [Fact]
    public void BuildCandidates_DeduplicatesMirrors()
    {
        var candidates = UpdateSourcePolicy.BuildCandidates(
            Url,
            ["https://a.example", "https://a.example/"]);

        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void BuildCandidates_WithBlankUrl_ReturnsEmpty()
        => Assert.Empty(UpdateSourcePolicy.BuildCandidates("  ", ["https://a.example"]));

    [Fact]
    public void Select_PrefersRememberedSourceWhenStillAvailable()
    {
        var probes = new[]
        {
            new UpdateSourceProbe("slow", Succeeded: true, ElapsedMs: 900, SupportsRange: true, TotalBytes: 10),
            new UpdateSourceProbe("remembered", Succeeded: true, ElapsedMs: 3000, SupportsRange: true, TotalBytes: 10),
        };

        Assert.Equal("remembered", UpdateSourcePolicy.Select(probes, "remembered"));
    }

    [Fact]
    public void Select_IgnoresRememberedSourceWhenItFailed()
    {
        var probes = new[]
        {
            new UpdateSourceProbe("remembered", Succeeded: false, ElapsedMs: 0, SupportsRange: false, TotalBytes: 0),
            new UpdateSourceProbe("fast", Succeeded: true, ElapsedMs: 500, SupportsRange: true, TotalBytes: 10),
        };

        Assert.Equal("fast", UpdateSourcePolicy.Select(probes, "remembered"));
    }

    [Fact]
    public void Select_PicksFastestWhenNoMemory()
    {
        var probes = new[]
        {
            new UpdateSourceProbe("a", Succeeded: true, ElapsedMs: 800, SupportsRange: true, TotalBytes: 10),
            new UpdateSourceProbe("b", Succeeded: true, ElapsedMs: 200, SupportsRange: true, TotalBytes: 10),
            new UpdateSourceProbe("c", Succeeded: true, ElapsedMs: 500, SupportsRange: true, TotalBytes: 10),
        };

        Assert.Equal("b", UpdateSourcePolicy.Select(probes, null));
    }

    [Fact]
    public void Select_ReturnsNullWhenAllFailed()
    {
        var probes = new[]
        {
            new UpdateSourceProbe("a", Succeeded: false, ElapsedMs: 0, SupportsRange: false, TotalBytes: 0),
            new UpdateSourceProbe("b", Succeeded: false, ElapsedMs: 0, SupportsRange: false, TotalBytes: 0),
        };

        Assert.Null(UpdateSourcePolicy.Select(probes, "a"));
    }

    [Fact]
    public void Select_WithEmptyProbes_ReturnsNull()
    {
        Assert.Null(UpdateSourcePolicy.Select([], "a"));
        Assert.Null(UpdateSourcePolicy.Select(null, "a"));
    }

    [Fact]
    public void Select_WithSingleAvailableSource_ReturnsIt()
        => Assert.Equal(
            "only",
            UpdateSourcePolicy.Select([new UpdateSourceProbe("only", true, 10, true, 5)], null));
}
