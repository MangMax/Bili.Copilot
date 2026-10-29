// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class TrayLifecycleTests
{
    [Fact]
    public void SuccessfulShellRecreationKeepsResidentWindowHidden()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        var icon = Assert.Single(desktop.Icons);
        for (var i = 0; i < 20; i++)
        {
            icon.Recreate(true);
            Assert.False(desktop.Visible);
            Assert.Same(icon, Assert.Single(desktop.Icons));
            Assert.Empty(desktop.Failures);
        }

        lifecycle.Restore();
        lifecycle.HandleClose();
        Assert.False(desktop.Visible);
        Assert.Same(icon, Assert.Single(desktop.Icons));
    }

    [Fact]
    public void RepeatedEnableAndRecoveryDoNotAccumulateHealthSubscriptions()
    {
        var desktop = new Desktop();
        using var lifecycle = desktop.Start();
        for (var i = 0; i < 20; i++)
        {
            lifecycle.SetEnabled(true);
            lifecycle.SetEnabled(true);
            lifecycle.HandleClose();
            var icon = Assert.Single(desktop.Icons);
            Assert.Equal(1, icon.HealthSubscribers);
            icon.Recreate(false);
            Assert.True(desktop.Visible);
            Assert.Single(desktop.Failures);
            desktop.Failures.Clear();

            lifecycle.HandleClose();
            var replacement = Assert.Single(desktop.Icons);
            Assert.NotSame(icon, replacement);
            Assert.Equal(0, icon.HealthSubscribers);
            Assert.Equal(1, replacement.HealthSubscribers);
            lifecycle.SetEnabled(false);
            Assert.Equal(0, replacement.HealthSubscribers);
            Assert.Empty(desktop.Icons);
            Assert.True(desktop.Visible);
        }
    }

    [Fact]
    public void DisabledSessionCannotRestoreWindowOrAffectReplacement()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        var old = Assert.Single(desktop.Icons);
        var lateNotification = old.CaptureHealthNotification();
        lifecycle.SetEnabled(false);
        desktop.Visible = false; // Playback can hide the window after disable.
        lateNotification();
        Assert.False(desktop.Visible);
        Assert.Empty(desktop.Icons);
        Assert.Empty(desktop.Failures);
        Assert.Equal(0, old.HealthSubscribers);

        lifecycle.SetEnabled(true);
        lifecycle.HandleClose();
        var replacement = Assert.Single(desktop.Icons);
        lateNotification();
        Assert.False(desktop.Visible);
        Assert.Same(replacement, Assert.Single(desktop.Icons));
        Assert.Empty(desktop.Failures);
    }

    [Fact]
    public async Task ShellFailureDuringAndAfterExitCannotRestoreOrRecreateTray()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        var icon = Assert.Single(desktop.Icons);
        var lateNotification = icon.CaptureHealthNotification();
        var save = new TaskCompletionSource();
        var exited = false;
        var exit = lifecycle.ExitAsync(() => save.Task, () => Task.CompletedTask, () => exited = true);
        icon.Recreate(false);
        Assert.False(desktop.Visible);
        Assert.Empty(desktop.Failures);
        Assert.Empty(desktop.Icons);
        Assert.False(exited);
        save.SetResult();
        await exit;
        lateNotification();
        Assert.True(exited);
        Assert.False(desktop.Visible);
        Assert.Empty(desktop.Icons);
        Assert.Empty(desktop.Failures);
        Assert.Equal(0, icon.HealthSubscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedShellRecreationRestoresAccessAndSubsequentCloseRetries(bool resident)
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        var icon = Assert.Single(desktop.Icons);
        if (resident)
        {
            lifecycle.HandleClose();
        }

        desktop.Minimized = true;
        icon.Recreate(false);
        Assert.True(desktop.Visible);
        Assert.False(desktop.Minimized);
        Assert.Empty(desktop.Icons);
        Assert.Contains("recreate", Assert.Single(desktop.Failures).Message);

        desktop.FailTray = true;
        Assert.True(lifecycle.HandleClose());
        Assert.True(desktop.Visible);
        Assert.Empty(desktop.Icons);
        desktop.FailTray = false;
        lifecycle.HandleClose();
        Assert.False(desktop.Visible);
        Assert.NotSame(icon, Assert.Single(desktop.Icons));
    }

    [Fact]
    public void PostCreationLossPreventsCloseIntoMissingTrayAndCanRetry()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        var lost = Assert.Single(desktop.Icons);
        lost.Lose();
        desktop.FailTray = true;

        Assert.True(lifecycle.HandleClose());
        Assert.True(desktop.Visible);
        Assert.Empty(desktop.Icons);
        Assert.NotEmpty(desktop.Failures);

        desktop.FailTray = false;
        Assert.True(lifecycle.HandleClose());
        Assert.False(desktop.Visible);
        Assert.NotSame(lost, Assert.Single(desktop.Icons));
    }

    [Theory]
    [InlineData(true, true, 1, false, false, 1)]
    [InlineData(true, false, 2, false, true, 0)]
    [InlineData(false, false, 1, false, true, 0)]
    [InlineData(false, false, 2, false, false, 0)]
    [InlineData(false, false, 1, true, true, 0)]
    public async Task PlaybackOnlyClosePreservesWindowBehavior(
        bool hideOnPlay, bool integration, int count, bool visible, bool expectedVisible, int expectedCloses)
    {
        var desktop = new Desktop { Visible = visible };
        using var lifecycle = desktop.Start();
        var cleanup = new TaskCompletionSource();
        var closes = new List<string>();
        var close = lifecycle.ClosePlayerAsync(hideOnPlay, integration, count, visible,
            () => desktop.Visible = true, () => cleanup.Task, () => closes.Add("close"));
        Assert.Equal(expectedVisible, desktop.Visible);
        Assert.Empty(closes);
        Assert.False(close.IsCompleted);
        cleanup.SetResult();
        await close;
        Assert.Equal(expectedCloses, closes.Count);
    }

    [Fact]
    public async Task ExitJoinsPendingPlayerCloseWithoutRestoringOrClosingMainAgain()
    {
        var desktop = new Desktop { Saved = true, Visible = false };
        using var lifecycle = desktop.Start();
        var cleanup = new TaskCompletionSource();
        var players = new List<string> { "video" };
        var close = lifecycle.ClosePlayerAsync(true, true, 1, false,
            () => desktop.Visible = true,
            async () => { await cleanup.Task; players.Clear(); },
            () => throw new Exception("main close during shutdown"));
        var exited = false;
        var exit = lifecycle.ExitAsync(() => Task.CompletedTask, () => close, () => exited = true);
        lifecycle.Restore();
        Assert.False(desktop.Visible);
        Assert.False(exited);
        Assert.Single(players);
        cleanup.SetResult();
        await exit;
        Assert.True(exited);
        Assert.Empty(players);
        Assert.Empty(desktop.Icons);
    }

    [Fact]
    public async Task CleanupFailureIsReportedAndStillReleasesTrayAndExits()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        var exited = false;
        await lifecycle.ExitAsync(() => Task.CompletedTask,
            () => Task.FromException(new IOException("native cleanup failed")),
            () => exited = true);
        Assert.True(exited);
        Assert.Empty(desktop.Icons);
        Assert.IsType<IOException>(Assert.Single(desktop.Failures));
    }

    [Fact]
    public async Task SaveFailureStillCleansPlayersAndExitsWithoutReactivation()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        var player = new List<string> { "video" };
        var exited = false;
        await lifecycle.ExitAsync(() => throw new IOException("settings unavailable"),
            () => lifecycle.ClosePlayerAsync(true, true, 1, false,
                () => desktop.Visible = true,
                () => { player.Clear(); return Task.CompletedTask; },
                () => throw new Exception("must not close main during exit")),
            () => exited = true);
        Assert.True(exited);
        Assert.Empty(player);
        Assert.Empty(desktop.Icons);
        Assert.False(desktop.Visible);
        Assert.IsType<IOException>(Assert.Single(desktop.Failures));
    }

    [Fact]
    public async Task ExitWaitsForSaveAndPlayerCleanupAndRejectsReentrantActivation()
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        var save = new TaskCompletionSource();
        var cleanup = new TaskCompletionSource();
        var persisted = new List<string>();
        var players = new List<string> { "video", "episode" };
        var exits = new List<string>();
        Task? reentrant = null;
        var exit = lifecycle.ExitAsync(async () =>
        {
            reentrant = lifecycle.ExitAsync(() => throw new Exception("duplicate save"),
                () => throw new Exception("duplicate cleanup"), () => throw new Exception("duplicate exit"));
            await save.Task;
            persisted.Add("settings");
        }, async () =>
        {
            await cleanup.Task;
            persisted.Add("position:120");
            players.Clear();
        }, () =>
        {
            Assert.Equal(new[] { "settings", "position:120" }, persisted);
            Assert.Empty(players);
            Assert.Empty(desktop.Icons);
            exits.Add("exit");
        });

        Assert.Same(exit, reentrant);
        lifecycle.Restore();
        lifecycle.SetEnabled(false);
        Assert.True(lifecycle.HandleClose());
        Assert.False(desktop.Visible);
        Assert.False(exit.IsCompleted);
        Assert.Empty(exits);
        save.SetResult();
        await Task.Yield();
        Assert.False(exit.IsCompleted);
        cleanup.SetResult();
        await exit;
        await lifecycle.ExitAsync(() => throw new Exception("duplicate save"),
            () => throw new Exception("duplicate cleanup"), () => throw new Exception("duplicate exit"));
        lifecycle.Restore();
        Assert.Single(exits);
        Assert.False(desktop.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResidentPlayerCloseKeepsMainHiddenUntilExplicitRestore(bool integration)
    {
        var desktop = new Desktop { Saved = true };
        using var lifecycle = desktop.Start();
        lifecycle.HandleClose();
        var players = new List<string> { "video" };
        var mainCloseRequests = new List<string>();
        await lifecycle.ClosePlayerAsync(true, integration, 1, false,
            () => desktop.Visible = true,
            () => { players.Clear(); return Task.CompletedTask; },
            () => mainCloseRequests.Add("close"));
        Assert.Empty(players);
        Assert.False(desktop.Visible);
        Assert.Empty(mainCloseRequests);
        Assert.Single(desktop.Icons);

        lifecycle.Restore();
        desktop.Visible = false; // A subsequent user-started playback hides the main window.
        await lifecycle.ClosePlayerAsync(true, false, 1, false,
            () => desktop.Visible = true, () => Task.CompletedTask,
            () => mainCloseRequests.Add("close"));
        Assert.True(desktop.Visible);
        Assert.Empty(mainCloseRequests);
    }

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
        public List<Exception> Failures { get; } = [];

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
            Failures.Add);
    }

    private sealed class Icon(List<Icon> icons) : ITrayIconSession
    {
        public event EventHandler? HealthChanged;
        public int HealthSubscribers => HealthChanged?.GetInvocationList().Length ?? 0;
        public bool IsCreated => icons.Contains(this);
        public Action CaptureHealthNotification()
        {
            var handlers = HealthChanged;
            return () => handlers?.Invoke(this, EventArgs.Empty);
        }

        public void Lose() => icons.Remove(this);
        public void Recreate(bool success)
        {
            Lose();
            if (success)
            {
                icons.Add(this);
            }

            HealthChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => icons.Remove(this);
    }
}
