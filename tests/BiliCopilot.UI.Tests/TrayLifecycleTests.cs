// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class TrayLifecycleTests
{
    [Fact]
    public void DisposedLifecycleCannotHideRestoreOrRecreateTray()
    {
        var desktop = new Desktop { Saved = true };
        var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        lifecycle.Dispose();
        lifecycle.Dispose();
        lifecycle.Restore();
        lifecycle.SetEnabled(true);
        Assert.False(lifecycle.HandleClose());
        Assert.Empty(desktop.Icons);
        Assert.False(desktop.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrayFailureRetainsAccessibleWindowAndCanRetry(bool saved)
    {
        var desktop = new Desktop { Saved = saved, FailTray = true, Minimized = true };
        using var lifecycle = desktop.Start();
        lifecycle.SetEnabled(true);
        Assert.True(lifecycle.HandleClose());
        Assert.True(desktop.Visible);
        Assert.False(desktop.Minimized);
        Assert.Empty(desktop.Icons);
        desktop.FailTray = false;
        Assert.True(lifecycle.HandleClose());
        Assert.False(desktop.Visible);
        Assert.Single(desktop.Icons);
    }

    [Fact]
    public void ActivationRecoversSameHiddenOrMinimizedWindowRepeatedly()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        var original = Assert.Single(desktop.Icons);
        for (var i = 0; i < 20; i++)
        {
            desktop.Minimized = true;
            lifecycle.HandleClose();
            lifecycle.Restore();
            lifecycle.Restore();
            Assert.True(desktop.Visible);
            Assert.False(desktop.Minimized);
            Assert.Same(original, Assert.Single(desktop.Icons));
        }
    }

    [Fact]
    public void RepeatedEnableDisableReleasesIconsAndRecoversHiddenWindow()
    {
        var desktop = new Desktop();
        using var lifecycle = desktop.Start();
        for (var i = 0; i < 20; i++)
        {
            lifecycle.SetEnabled(true);
            lifecycle.SetEnabled(true);
            Assert.Single(desktop.Icons);
            lifecycle.HandleClose();
            lifecycle.SetEnabled(false);
            lifecycle.SetEnabled(false);
            Assert.Empty(desktop.Icons);
            Assert.True(desktop.Visible);
            Assert.False(lifecycle.HandleClose());
        }
    }

    [Fact]
    public void EnabledSettingSurvivesRestartAndClosesToTray()
    {
        var desktop = new Desktop();
        using (var first = desktop.Start())
        {
            first.SetEnabled(true);
            Assert.Single(desktop.Icons);
            Assert.True(first.HandleClose());
            Assert.False(desktop.Visible);
        }

        Assert.Empty(desktop.Icons);
        desktop.Visible = true;
        using var restarted = desktop.Start();
        Assert.Single(desktop.Icons);
        Assert.True(restarted.HandleClose());
        Assert.False(desktop.Visible);
    }

    [Fact]
    public void FreshInstallationKeepsNormalCloseAndHasNoTray()
    {
        var desktop = new Desktop();
        using var lifecycle = desktop.Start();
        Assert.False(lifecycle.HandleClose());
        Assert.Empty(desktop.Icons);
        Assert.True(desktop.Visible);
    }

    private sealed class Desktop
    {
        public bool Saved { get; set; }
        public bool Visible { get; set; } = true;
        public bool Minimized { get; set; }
        public bool FailTray { get; set; }
        public List<Icon> Icons { get; } = [];

        public TrayLifecycle Start() => new(
            () => Saved,
            value => Saved = value,
            () =>
            {
                if (FailTray)
                {
                    throw new InvalidOperationException("Shell unavailable");
                }

                var icon = new Icon(Icons);
                Icons.Add(icon);
                return icon;
            },
            () => Visible = false,
            () =>
            {
                Visible = true;
                Minimized = false;
            },
            _ => { });
    }

    private sealed class Icon(List<Icon> icons) : IDisposable
    {
        public void Dispose() => icons.Remove(this);
    }
}
