using BiliCopilot.UI.ViewModels.Core;
using Xunit;

public class SpeedCloseTests
{
    [Fact]
    public async Task ClosingAndClosedPlayersRejectSpeed()
    {
        var vm = new PlayerViewModel();
        var init = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.HoldInitialization(init.Task);
        var close = vm.CloseAsync();
        await vm.SpeedAsync();
        Assert.Empty(vm.Client.Speeds);
        init.SetResult();
        await close;
        await vm.SpeedAsync();
        Assert.Empty(vm.Client.Speeds);
    }

    [Fact]
    public async Task RepeatedCloseJoinsAndDrainsEverySpeed()
    {
        var vm = new PlayerViewModel();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Client.Send = () => first.Task;
        var a = vm.SpeedAsync();
        vm.Client.Send = () => second.Task;
        var b = vm.SpeedAsync();
        var close = vm.CloseAsync();
        Assert.Same(close, vm.CloseAsync());
        first.SetResult(); await a;
        Assert.False(close.IsCompleted);
        Assert.Equal(0, vm.Client.Disposals);
        second.SetResult(); await b; await close;
        Assert.Equal(1, vm.Client.Disposals);
    }

    [Fact]
    public async Task SpeedFaultPropagatesAndStillReleasesClose()
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Client.Send = () => gate.Task;
        var speed = vm.SpeedAsync();
        var close = vm.CloseAsync();
        var failure = new InvalidOperationException("controlled speed failure");
        gate.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => speed));
        await close;
        Assert.Equal(1, vm.Client.Disposals);
    }

    [Fact]
    public async Task DirectDisposalAlsoRejectsAndDrainsSpeed()
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Client.Send = () => gate.Task;
        var speed = vm.SpeedAsync();
        var dispose = vm.DisposeAsync().AsTask();
        var again = vm.DisposeAsync().AsTask();
        await vm.SpeedAsync();
        Assert.Single(vm.Client.Speeds);
        Assert.False(dispose.IsCompleted);
        gate.SetResult(); await speed; await dispose; await again;
        Assert.Equal(1, vm.Client.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldCompletionCannotReportSuccessForNewMediaOrClient(bool replaceClient)
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = vm.Client;
        old.Send = () => gate.Task;
        var speed = vm.ChangeSpeedAsync(2);
        if (replaceClient) { vm.Client = new(); vm.Player = new(vm.Client); }
        else { vm.ChangeMedia(); }
        gate.SetResult();
        Assert.False(await speed);
        Assert.Equal(new[] { 2d }, old.Speeds);
        if (replaceClient) Assert.Empty(vm.Client.Speeds);
        Assert.True(await vm.ChangeSpeedAsync(1.5));
        Assert.Equal(replaceClient ? new[] { 1.5d } : new[] { 2d, 1.5d }, vm.Client.Speeds);
    }

    [Fact]
    public async Task ReopenedPlayerIsIndependentOfOldClose()
    {
        var old = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        old.Client.Send = () => gate.Task;
        var speed = old.SpeedAsync(); var close = old.CloseAsync();
        var reopened = new PlayerViewModel();
        Assert.True(await reopened.ChangeSpeedAsync(1.5));
        gate.SetResult(); await speed; await close;
        Assert.Equal(0, reopened.Client.Disposals);
        Assert.True(await reopened.ChangeSpeedAsync(2));
        Assert.Equal(new[] { 1.5d, 2d }, reopened.Client.Speeds);
    }

    [Fact]
    public async Task CloseWaitsForAcceptedSpeedBeforeDisposal()
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Client.Send = () => gate.Task;
        var speed = vm.SpeedAsync();
        var close = vm.CloseAsync();
        try
        {
            Assert.Single(vm.Client.Speeds);
            Assert.False(close.IsCompleted);
            Assert.Equal(0, vm.Client.Disposals);
        }
        finally { gate.SetResult(); await speed; await close; }
        Assert.Equal(1, vm.Client.Disposals);
    }
}
