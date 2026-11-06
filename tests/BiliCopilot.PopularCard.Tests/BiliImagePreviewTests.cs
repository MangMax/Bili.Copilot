// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class BiliImagePreviewTests
{
    [Fact]
    public void PortraitPreviewPreservesAspectAndOriginalSource()
    {
        var source = new Uri("http://i0.hdslb.com/bfs/new_dyn/portrait.png");
        var preview = BiliImagePreview.Create(source, 338, 600, crop: false, modernFormat: true);
        Assert.Equal("https://i0.hdslb.com/bfs/new_dyn/portrait.png@338w_600h.avif", preview.AbsoluteUri);
        Assert.Equal("http://i0.hdslb.com/bfs/new_dyn/portrait.png", source.AbsoluteUri);
    }

    [Fact]
    public void MultiImagePreviewCropsAndSupportsJpegFallback()
    {
        var source = new Uri("https://i0.hdslb.com/bfs/new_dyn/portrait.png");
        Assert.Equal("https://i0.hdslb.com/bfs/new_dyn/portrait.png@208w_208h_1c.jpg",
            BiliImagePreview.Create(source, 208, 208, crop: true, modernFormat: false).AbsoluteUri);
    }

    [Theory]
    [InlineData("https://i0.hdslb.com/bfs/a.png@240w_240h_1c.webp")]
    [InlineData("https://example.org/a.png")]
    [InlineData("https://i0.hdslb.com/bfs/a.png?signature=keep")]
    [InlineData("file:///C:/image.png")]
    public void ExistingTransformsAndExternalSourcesRemainUnchanged(string address)
    {
        var source = new Uri(address);
        Assert.Same(source, BiliImagePreview.Create(source, 208, 208, true, true));
    }
}
