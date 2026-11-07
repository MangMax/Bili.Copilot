using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class PlayerWindowPlacementSessionTests
{
    [Fact]
    public async Task DraggedPositionSurvivesRoundTripAndWindowRecreation()
    {
        var desktop = new Desktop();
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        desktop.Bounds = new(-900, 120, 400, 250);
        session.GeometryChanged();
        await session.ChangeAsync(WindowPlacementMode.Ordinary, () => desktop.Bounds = new(100, 100, 1100, 700));
        Assert.Equal(new PlacementPoint(-900, 120), desktop.Saved);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => desktop.Bounds = new(1400, 700, 400, 250));
        Assert.Equal(new PlacementBounds(-900, 120, 400, 250), desktop.Bounds);
        session.Close();
        var reopened = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await reopened.ChangeAsync(WindowPlacementMode.CompactOverlay, () => desktop.Bounds = new(1400, 700, 400, 250));
        Assert.Equal(new PlacementBounds(-900, 120, 400, 250), desktop.Bounds);
    }

    [Theory]
    [InlineData(-1800, 200, 400, 250, false, -1800, 200)]
    [InlineData(-1800, 200, 400, 250, true, 0, 200)]
    // Partially overhanging a corner is a deliberate compact position and survives.
    [InlineData(1800, 1000, 600, 375, false, 1800, 1000)]
    [InlineData(0, 0, 400, 250, false, 0, 0)]
    // A fully hidden window is recovered with its current physical size.
    [InlineData(int.MaxValue, int.MaxValue, 400, 250, false, 1520, 790)]
    public async Task RestoreKeepsVisiblePositionAndRecoversOnlyFullyHiddenOne(
        int x, int y, int width, int height, bool removed, int expectedX, int expectedY)
    {
        var desktop = new Desktop { Saved = new(x, y), SecondaryRemoved = removed };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay,
            () => desktop.Bounds = new(1400, 700, width, height));
        Assert.Equal(new PlacementBounds(expectedX, expectedY, width, height), desktop.Bounds);
    }

    [Fact]
    public async Task FirstEntryKeepsSystemDefaultAndDoesNotReadOrdinaryPosition()
    {
        var desktop = new Desktop();
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.Equal(new PlacementBounds(1400, 700, 400, 250), desktop.Bounds);
        Assert.Null(desktop.Saved);
    }

    [Fact]
    public async Task CloseDuringRestorePreventsAnyFurtherWindowAccess()
    {
        var desktop = new Desktop { Saved = new(-900, 120) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        desktop.Moved = () => { session.Close(); desktop.Destroyed = true; };
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.False(session.CanSaveOrdinaryWindow);
        Assert.Equal(new PlacementPoint(-900, 120), desktop.Saved);
    }

    [Fact]
    public async Task OtherWindowSaveOnlyAffectsNextEntry()
    {
        var desktop = new Desktop { Saved = new(40, 50) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        await session.ChangeAsync(WindowPlacementMode.Ordinary, () => desktop.Bounds = new(100, 100, 1100, 700));
        desktop.Saved = new(200, 300);
        Assert.Equal(new PlacementBounds(100, 100, 1100, 700), desktop.Bounds);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => desktop.Bounds = new(1400, 700, 400, 250));
        Assert.Equal(new PlacementBounds(200, 300, 400, 250), desktop.Bounds);
    }

    [Fact]
    public async Task PersistedCoordinatePairSurvivesNewStoreAndSessionWithoutOrdinarySettings()
    {
        var desktop = new Desktop();
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        desktop.Bounds = new(-1250, -600, 400, 250);
        session.Close();
        Assert.Equal("-1250,-600", desktop.StoredValue);
        var restarted = new Desktop { StoredValue = desktop.StoredValue, WorkArea = new(-1920, -1080, 1920, 1080) };
        await new PlayerWindowPlacementSession(restarted, () => Task.CompletedTask)
            .ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.Equal(new PlacementBounds(-1250, -600, 400, 250), restarted.Bounds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("100")]
    [InlineData("1,2,3")]
    [InlineData("2147483648,0")]
    [InlineData("invalid,100")]
    public async Task MissingOrInvalidSettingUsesSystemPlacement(string value)
    {
        var desktop = new Desktop { StoredValue = value };
        await new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask)
            .ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.Equal(new PlacementBounds(1400, 700, 400, 250), desktop.Bounds);
    }

    [Fact]
    public async Task PresenterRelocatedWindowIsMovedBackToTheRememberedPosition()
    {
        var desktop = new Desktop { Saved = new(1800, 1000) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        // The presenter constrains the window to another size at another location.
        desktop.Moved = () => desktop.Bounds = desktop.Bounds with { Width = 600, Height = 375 };
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay,
            () => desktop.Bounds = new(5000, 5000, 600, 375));
        Assert.Equal(new PlacementBounds(1800, 1000, 600, 375), desktop.Bounds);
    }

    [Fact]
    public async Task CompactEntryRepositionsBeforeThePresenterSwitchSoNothingJumps()
    {
        var desktop = new Desktop { Saved = new(1800, 1000) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        PlacementBounds? atPresenter = null;
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay,
            () => atPresenter = desktop.Bounds);
        Assert.Equal(new PlacementPoint(1800, 1000), new PlacementPoint(atPresenter!.Value.X, atPresenter.Value.Y));
        Assert.Equal(new PlacementBounds(1800, 1000, 400, 250), desktop.Bounds);
    }

    [Fact]
    public async Task PresenterBatchRunsThePostSwitchHookBeforeTheSettleDelay()
    {
        var desktop = new Desktop { Saved = new(1800, 1000) };
        var events = new List<string>();
        var session = new PlayerWindowPlacementSession(desktop, () =>
        {
            events.Add("settle");
            return Task.CompletedTask;
        });
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay,
            () => events.Add("presenter"),
            () => events.Add("after"));
        Assert.Equal(new[] { "presenter", "after", "settle" }, events);
    }

    [Fact]
    public async Task FullyHiddenPositionIsRecoveredWithCurrentSize()
    {
        var desktop = new Desktop { Saved = new(2300, 1400) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.Equal(new PlacementBounds(1520, 790, 400, 250), desktop.Bounds);
    }

    [Fact]
    public async Task RestoreEventsCannotOverwriteOrdinaryWindowOrSavedCompactPosition()
    {
        var desktop = new Desktop { Saved = new(-900, 120) };
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        desktop.Moved = () =>
        {
            Assert.False(session.CanSaveOrdinaryWindow);
            session.GeometryChanged();
        };
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        Assert.Equal(new PlacementPoint(-900, 120), desktop.Saved);
        Assert.False(session.CanSaveOrdinaryWindow);
        await session.ChangeAsync(WindowPlacementMode.FullScreen, () => { });
        Assert.False(session.CanSaveOrdinaryWindow);
        await session.ChangeAsync(WindowPlacementMode.Ordinary, () => Assert.False(session.CanSaveOrdinaryWindow));
        Assert.True(session.CanSaveOrdinaryWindow);
    }

    [Fact]
    public async Task SupersededTransitionsAndCloseNeverRestoreOrSaveIntermediateGeometry()
    {
        var desktop = new Desktop { Saved = new(-900, 120) };
        var pending = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource()).ToArray();
        var delays = new Queue<Task>(pending.Select(p => p.Task));
        var session = new PlayerWindowPlacementSession(desktop, () => delays.Dequeue());
        var first = session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        desktop.Bounds = new(500, 500, 800, 600);
        session.GeometryChanged();
        var second = session.ChangeAsync(WindowPlacementMode.FullScreen, () => { });
        pending[1].SetResult();
        await second;
        var third = session.ChangeAsync(WindowPlacementMode.Ordinary, () => { });
        pending[0].SetResult();
        await first;
        Assert.True(session.IsChanging);
        Assert.Equal(new PlacementBounds(500, 500, 800, 600), desktop.Bounds);
        session.Close();
        desktop.Destroyed = true;
        pending[2].SetResult();
        await third;
        session.GeometryChanged();
        session.Close();
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => throw new Exception("Destroyed window"));
        Assert.Equal(new PlacementPoint(-900, 120), desktop.Saved);
    }

    [Fact]
    public async Task LeavingAndClosingCaptureFinalStablePositionWithoutChangeEvent()
    {
        var desktop = new Desktop();
        var session = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { });
        desktop.Bounds = new(30, 40, 400, 250);
        await session.ChangeAsync(WindowPlacementMode.FullScreen, () => desktop.Bounds = new(0, 0, 1920, 1080));
        Assert.Equal(new PlacementPoint(30, 40), desktop.Saved);
        await session.ChangeAsync(WindowPlacementMode.CompactOverlay, () => desktop.Bounds = new(1400, 700, 400, 250));
        desktop.Bounds = new(60, 80, 400, 250);
        session.Close();
        Assert.Equal(new PlacementPoint(60, 80), desktop.Saved);
        Assert.False(session.CanSaveOrdinaryWindow);
    }

    private sealed class Desktop : IPlayerWindowPlacement
    {
        public Action? Moved { get; set; }
        public bool Destroyed { get; set; }
        public PlacementBounds? WorkArea { get; set; }
        public bool SecondaryRemoved { get; set; }
        public string StoredValue { get; set; } = string.Empty;
        public PlacementPoint? Saved
        {
            get => PlacementPoint.ParseSetting(StoredValue);
            set => StoredValue = value?.ToSetting() ?? string.Empty;
        }
        public PlacementBounds Bounds { get; set; } = new(1400, 700, 400, 250);
        public PlacementPoint? ReadPosition() { Assert.False(Destroyed); return Saved; }
        public void WritePosition(PlacementPoint point) { Assert.False(Destroyed); Saved = point; }
        public PlacementBounds GetWorkArea(PlacementPoint point)
            => WorkArea ?? (point.X < 0 && !SecondaryRemoved ? new(-1920, 0, 1920, 1080) : new(0, 0, 1920, 1040));
        public void Move(PlacementPoint point)
        {
            Assert.False(Destroyed);
            Bounds = Bounds with { X = point.X, Y = point.Y };
            Moved?.Invoke();
        }
    }
}
