// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// A recycled card can receive Loaded before its template is applied. The control
/// must stay retryable instead of latching a half-initialized state (#177).
/// </summary>
public sealed class ReloadableCoverImageTests
{
    [Fact]
    public void LoadedWithoutTemplateDoesNotThrowOrInitialize()
    {
        var image = new TestCoverImage();

        var exception = Record.Exception(image.Load);

        Assert.Null(exception);
        Assert.Equal(1, image.ApplyTemplateCalls);
        Assert.Equal(0, image.OnApplyTemplateCalls);
        Assert.Equal(0, image.OnControlLoadedCalls);
        Assert.Equal(0, image.OnControlUnloadedCalls);
    }

    [Fact]
    public void LoadWithoutTemplateStaysRetryableForTheNextLoad()
    {
        var image = new TestCoverImage();

        image.Load();

        image.InstallTemplate();
        image.Load();

        Assert.Equal(1, image.OnControlLoadedCalls);
    }

    [Fact]
    public void LoadedWithTemplateInitializesOnceUntilUnloaded()
    {
        var image = new TestCoverImage();
        image.InstallTemplate();

        image.Load();

        Assert.Equal(1, image.ApplyTemplateCalls);
        Assert.Equal(1, image.OnControlLoadedCalls);
    }

    [Fact]
    public void RepeatedLoadedWithoutUnloadDoesNotInitializeAgain()
    {
        var image = new TestCoverImage();
        image.InstallTemplate();

        image.Load();
        image.Load();

        Assert.Equal(1, image.ApplyTemplateCalls);
        Assert.Equal(1, image.OnControlLoadedCalls);
    }

    [Fact]
    public void RemountReinitializesTemplateAndRecreatesSurface()
    {
        var image = new TestCoverImage();
        image.InstallTemplate();
        image.Load();
        image.RecreateSurfaceForTest();
        Assert.Equal(CanvasAlphaMode.Premultiplied, image.Surface!.AlphaMode);

        image.Unload();

        Assert.Equal(1, image.OnControlUnloadedCalls);
        Assert.Null(image.Surface);

        image.Load();

        Assert.Equal(2, image.OnControlLoadedCalls);
        Assert.Equal(1, image.OnControlUnloadedCalls);
        Assert.Equal(2, image.ApplyTemplateCalls);
        Assert.NotNull(image.Surface);
        Assert.Equal(CanvasAlphaMode.Premultiplied, image.Surface!.AlphaMode);
    }

    [Fact]
    public void UnloadWithoutSuccessfulLoadDoesNotCallBaseUnload()
    {
        var image = new TestCoverImage();

        image.Load();
        image.Unload();

        Assert.Equal(0, image.OnControlUnloadedCalls);
    }

    private sealed class TestCoverImage : ReloadableCoverImage
    {
        public CanvasImageSource? Surface => CanvasImageSource;

        public void RecreateSurfaceForTest() => RecreateSurface();
    }
}
