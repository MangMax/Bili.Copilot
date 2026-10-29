using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class CoverDecodeSizeTests
{
    [Theory]
    [InlineData(1.5, 384, 216)]
    [InlineData(2, 512, 288)]
    public void LandscapeCoverHasOneDecodedPixelPerDisplayPixel(double dpi, uint width, uint height)
        => Assert.Equal((width, height), CoverDecodeSize.Calculate(1920, 1080, 256, 144, dpi));

    [Fact]
    public void PortraitCoverRetainsEnoughPixelsForHorizontalCrop()
        => Assert.Equal((450u, 675u), CoverDecodeSize.Calculate(600, 900, 300, 169, 1.5));

    [Fact]
    public void SmallSourceIsNotUpsampledDuringDecode()
        => Assert.Equal((100u, 100u), CoverDecodeSize.Calculate(100, 100, 300, 169, 2));

    [Fact]
    public void FractionalLayoutRoundsUpToAvoidUndersizedDecode()
        => Assert.Equal((385u, 217u), CoverDecodeSize.Calculate(1920, 1080, 256.1, 144, 1.5));

    [Fact]
    public void InvalidLayoutIsRejectedBeforeDecoding()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CoverDecodeSize.Calculate(1920, 1080, double.NaN, 144, 1.5));
}
