// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class UpdateAssetParserTests
{
    private const string Sha = "2f651a6831da783f8885126e357f806cddab3bb1655bf9b6ecddb88314e3b166";

    private static string Row(string file, string tag = "v2.2610.3.0", string? digest = Sha) => $"""
        <li data-view-component="true" class="Box-row d-flex flex-column flex-md-row">
          <a href="/MangMax/Bili.Copilot/releases/download/{tag}/{file}" rel="nofollow" data-turbo="false" class="wb-break-all">
            <span class="text-bold">{file}</span>
          </a>
          <span class="Truncate-text">sha256:{digest}</span>
        </li>
        """;

    [Fact]
    public void Parse_ReadsNamePathAndDigest()
    {
        var assets = UpdateAssetParser.Parse(Row("BiliCopilot_2.2610.3.0_x64.msix"));

        var asset = Assert.Single(assets);
        Assert.Equal("BiliCopilot_2.2610.3.0_x64.msix", asset.Name);
        Assert.Equal("/MangMax/Bili.Copilot/releases/download/v2.2610.3.0/BiliCopilot_2.2610.3.0_x64.msix", asset.DownloadPath);
        Assert.Equal(Sha, asset.Sha256);
        Assert.True(asset.IsMsix);
    }

    [Fact]
    public void Parse_WithMultipleRows_ReturnsAll()
    {
        var html = Row("BiliCopilot_2.2610.3.0_x64.msix")
            + Row("BiliCopilot_2.2610.3.0_x64.zip")
            + Row("BiliCopilot_2.2610.3.0_arm64.msix");

        Assert.Equal(3, UpdateAssetParser.Parse(html).Count);
    }

    [Fact]
    public void Parse_WithMissingDigest_ReturnsNullSha()
        => Assert.Null(Assert.Single(UpdateAssetParser.Parse(Row("BiliCopilot_2.2610.3.0_x64.msix", digest: null))).Sha256);

    [Fact]
    public void Parse_IgnoresSourceArchives()
    {
        const string html = """
            <li><a href="/MangMax/Bili.Copilot/archive/refs/tags/v2.2610.3.0.zip">Source code</a></li>
            """;

        Assert.Empty(UpdateAssetParser.Parse(html));
    }

    [Fact]
    public void Parse_WithEmptyHtml_ReturnsEmpty()
    {
        Assert.Empty(UpdateAssetParser.Parse(null));
        Assert.Empty(UpdateAssetParser.Parse(string.Empty));
    }

    [Fact]
    public void Select_PrefersMsixForMatchingArchitecture()
    {
        var assets = UpdateAssetParser.Parse(
            Row("BiliCopilot_2.2610.3.0_x64.zip") + Row("BiliCopilot_2.2610.3.0_x64.msix"));

        var selection = UpdateAssetParser.Select(assets, "x64");

        Assert.Equal(UpdateAssetKind.Msix, selection.Kind);
        Assert.Equal("BiliCopilot_2.2610.3.0_x64.msix", selection.Asset!.Name);
    }

    [Fact]
    public void Select_FallsBackToZipWhenNoMsix()
    {
        var assets = UpdateAssetParser.Parse(Row("BiliCopilot_2.2610.3.0_x64.zip"));

        var selection = UpdateAssetParser.Select(assets, "x64");

        Assert.Equal(UpdateAssetKind.Zip, selection.Kind);
        Assert.Equal("BiliCopilot_2.2610.3.0_x64.zip", selection.Asset!.Name);
    }

    [Fact]
    public void Select_WithArm64_PicksArm64Asset()
    {
        var assets = UpdateAssetParser.Parse(
            Row("BiliCopilot_2.2610.3.0_x64.msix") + Row("BiliCopilot_2.2610.3.0_arm64.msix"));

        var selection = UpdateAssetParser.Select(assets, "arm64");

        Assert.Equal("BiliCopilot_2.2610.3.0_arm64.msix", selection.Asset!.Name);
    }

    [Fact]
    public void Select_WithNoMatchingArchitecture_ReturnsNone()
    {
        var assets = UpdateAssetParser.Parse(Row("BiliCopilot_2.2610.3.0_x64.msix"));

        var selection = UpdateAssetParser.Select(assets, "arm64");

        Assert.Equal(UpdateAssetKind.None, selection.Kind);
        Assert.Null(selection.Asset);
    }

    [Fact]
    public void Select_WithEmptyAssets_ReturnsNone()
    {
        Assert.Equal(UpdateAssetKind.None, UpdateAssetParser.Select([], "x64").Kind);
        Assert.Equal(UpdateAssetKind.None, UpdateAssetParser.Select(null, "x64").Kind);
    }

    [Fact]
    public void Select_WithBlankArchitecture_ReturnsNone()
        => Assert.Equal(UpdateAssetKind.None, UpdateAssetParser.Select(UpdateAssetParser.Parse(Row("a_x64.msix")), " ").Kind);
}
