using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class MediaDisplayDimensionsTests
{
    [Theory]
    [InlineData(1920, 1080, 90, 1080, 1920)]
    [InlineData(1920, 1080, 270, 1080, 1920)]
    [InlineData(1440, 1080, 0, 1440, 1080)]
    [InlineData(1920, 1080, 180, 1920, 1080)]
    public void BackendDisplayDimensionsApplyRemainingOutputRotation(double width, double height, double rotation, double expectedWidth, double expectedHeight)
    {
        var size = MediaDisplayDimensions.FromOutput(width, height, rotation);
        Assert.Equal(expectedWidth, size.Width, 6);
        Assert.Equal(expectedHeight, size.Height, 6);
    }
}
