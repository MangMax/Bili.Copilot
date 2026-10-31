// Review-only tests; copied into the isolated test project for the review.
using BiliCopilot.UI.ViewModels.Core;
namespace BiliCopilot.UI.Tests;

public class SeekQueueReviewTests : IDisposable
{
    public SeekQueueReviewTests() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();
    public void Dispose() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = null;

    [Fact]
    public async Task MiddleWriteFinishingLastCannotLeaveMediaAt19()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Client!.Send = async p =>
        {
            if (p == 19) await gate.Task;
            vm.Player.Position = p;
            vm.Player.Notify(nameof(vm.Player.Position));
            return new(true);
        };
        var all = Task.WhenAll(vm.ChangePositionAsync(21), vm.ChangePositionAsync(19), vm.ChangePositionAsync(20));
        gate.SetResult();
        await all.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, vm.Player.Position);
        Assert.Equal(20, vm.Danmaku.Position);
    }

    [Fact]
    public async Task SynchronousNativeCallbackCanEnqueueLatestWithoutDeadlock()
    {
        var vm = new PlayerViewModel();
        Task? reentrant = null;
        vm.Client!.Send = p =>
        {
            if (p == 21) reentrant = vm.ChangePositionAsync(20);
            vm.Player.Position = p;
            vm.Player.Notify(nameof(vm.Player.Position));
            return Task.FromResult(new SeekTestResult(true));
        };
        await vm.ChangePositionAsync(21).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(reentrant);
        await reentrant.WaitAsync(TimeSpan.FromSeconds(5));
        await vm.DrainPositionChangesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([21d, 20d], vm.Client.Requests);
        Assert.Equal(20, vm.Player.Position);
        Assert.Equal(20, vm.Danmaku.Position);
    }

    [Fact]
    public async Task ThrowingSynchronousSeekSubscriberStillReleasesQueue()
    {
        var vm = new PlayerViewModel();
        Task? reentrant = null;
        vm.Danmaku.SeekRequested += (_, p) =>
        {
            if (p != 21) return;
            reentrant = vm.ChangePositionAsync(20);
            throw new InvalidOperationException("review subscriber failure");
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ChangePositionAsync(21));
        Assert.NotNull(reentrant);
        await reentrant.WaitAsync(TimeSpan.FromSeconds(5));
        await vm.DrainPositionChangesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([21d, 20d], vm.Client!.Requests);
        Assert.Equal(20, vm.Danmaku.Position);
    }
}
