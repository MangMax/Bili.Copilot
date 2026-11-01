using BiliCopilot.UI.Controls.Danmaku;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.Tests;

public class TerminalFailureTests
{

    [Fact]
    public void TerminalProgressRetiresOnceAndKeepsSubsequentInputsUsable()
    {

        TestLogs.Errors.Clear();
        var panel = new VideoDanmakuPanel();
        panel.Load();
        var master = panel.TestMaster!;
        var calls = master.Calls;
        panel.ViewModel.Redraw(); // Queue work before the worker fails.
        var fault = new InvalidOperationException("worker");
        master.WorkerFailure = fault;
        panel.ViewModel.Progress(25);
        panel.ViewModel.Seek(40);
        panel.ViewModel.OtherInputs();
        panel.ViewModel.Renderer = Models.Constants.DanmakuRendererType.DirectX;
        panel.ViewModel.Redraw();
        panel.ViewModel.IsShowDanmaku = false;
        panel.ViewModel.IsShowDanmaku = true;
        panel.DispatcherQueue.Drain();
        panel.Unload();
        Assert.Equal(calls, master.Calls);
        Assert.Equal(1, master.Closes);
        Assert.Null(panel.TestMaster);
        Assert.Null(panel.TestCore);
        Assert.Equal(40, panel.ViewModel.Position);
        Assert.Same(fault, Assert.Single(TestLogs.Errors));
    }

    [Fact]
    public void UnrelatedExceptionStillPropagates()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var error = new ArgumentException("bad request");
        p.TestMaster!.NextFailure = error;
        Assert.Same(error, Assert.Throws<ArgumentException>(() => p.ViewModel.Progress(25)));
        Assert.Equal(0, p.TestMaster.Closes);
    }

    [Fact]
    public void FailurePublishedDuringCallIsRetired()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.PublishFailure = true;
        m.NextFailure = new InvalidOperationException("render");
        p.ViewModel.Seek(25);
        Assert.Null(p.TestMaster);
        Assert.Equal(1, m.Closes);
    }

    [Fact]
    public void NewViewModelCanRecoverButReloadAndRedrawCannot()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.WorkerFailure = new InvalidOperationException("worker");
        p.ViewModel.Progress(25);
        p.Unload();
        p.Load();
        Assert.Null(p.TestMaster);
        p.SetViewModel(new());
        p.DispatcherQueue.Drain();
        Assert.NotNull(p.TestMaster);
        Assert.NotSame(m, p.TestMaster);
    }

    [Fact]
    public void FaultedCleanupFailureIsLoggedAndDoesNotEscape()
    {
        TestLogs.Errors.Clear();
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.WorkerFailure = new InvalidOperationException("worker");
        m.CloseFailure = new InvalidOperationException("cleanup");
        p.ViewModel.Progress(25);
        p.Unload();
        Assert.Equal(1, m.Closes);
        Assert.Equal(2, TestLogs.Errors.Count);
        Assert.Null(p.TestMaster);
    }

    [Fact]
    public void UnloadDetectsPreviouslyUnobservedFailure()
    {
        TestLogs.Errors.Clear();
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.WorkerFailure = new InvalidOperationException("worker");
        p.Unload();
        Assert.Equal(1, m.Closes);
        Assert.Single(TestLogs.Errors);
        p.Load();
        Assert.Null(p.TestMaster);
    }

    [Fact]
    public void CleanupFailureRequiresNewPanel()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.WorkerFailure = new InvalidOperationException("worker");
        m.CloseFailure = new InvalidOperationException("cleanup");
        p.ViewModel.Progress(25);
        p.SetViewModel(new());
        p.DispatcherQueue.Drain();
        Assert.Null(p.TestMaster);
        var fresh = new VideoDanmakuPanel();
        fresh.Load();
        Assert.NotNull(fresh.TestMaster);
    }

    [Fact]
    public void ConcurrentUnrelatedErrorIsNotMistakenForTerminalRejection()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        var unrelated = new ArgumentException("input");
        m.BeforeCall = () => { m.WorkerFailure = new InvalidOperationException("worker"); throw unrelated; };
        Assert.Same(unrelated, Assert.Throws<ArgumentException>(() => p.ViewModel.Progress(25)));
        Assert.Equal(0, m.Closes);
    }

    [Fact]
    public void BackgroundProgressRetiresOnlyOnOwnerDispatcher()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var m = p.TestMaster!;
        m.WorkerFailure = new InvalidOperationException("worker");
        var worker = new Thread(() => p.ViewModel.Progress(25));
        worker.Start();
        worker.Join();
        Assert.Equal(0, m.Closes);
        p.DispatcherQueue.Drain();
        Assert.Equal(1, m.Closes);
        Assert.Null(p.TestMaster);
    }

    [Fact]
    public void QueuedOldViewModelInputCannotTouchReplacement()
    {
        var p = new VideoDanmakuPanel();
        p.Load();
        var worker = new Thread(() => p.ViewModel.Seek(25));
        worker.Start();
        worker.Join();
        p.SetViewModel(new());
        var m = p.TestMaster!;
        m.SeekRequests.Clear();
        p.DispatcherQueue.Drain();
        Assert.Equal(new uint[] { 0 }, m.SeekRequests);
        p.Unload();
        Assert.Equal(1, m.Closes);
    }
    [Fact]
    public void FailureDuringReplacementCleanupDoesNotCreateAnotherRenderer()
    {
        TestLogs.Errors.Clear();
        var panel = new VideoDanmakuPanel();
        panel.Load();
        var master = panel.TestMaster!;
        master.BeforeClose = () => { master.WorkerFailure = new InvalidOperationException("worker"); throw new InvalidOperationException("cleanup"); };
        panel.ViewModel.Renderer = Models.Constants.DanmakuRendererType.DirectX;
        panel.ViewModel.Redraw();
        panel.DispatcherQueue.Drain();
        Assert.Equal(1, master.Closes);
        Assert.Null(panel.TestMaster);
        Assert.Null(panel.TestCore);
        Assert.Equal(2, TestLogs.Errors.Count);
    }
    [Fact]
    public void QueuedInputCannotCrossUnloadAndReloadOfSameViewModel()
    {
        var panel = new VideoDanmakuPanel();
        panel.Load();
        var old = panel.TestMaster!;
        var worker = new Thread(() => panel.ViewModel.Seek(25));
        worker.Start(); worker.Join();
        panel.Unload();
        panel.ViewModel.Position = 42;
        panel.Load();
        Assert.Equal(1, old.Closes);
        Assert.NotSame(old, panel.TestMaster);
        Assert.Equal(new uint[] { 42000 }, panel.TestMaster!.SeekRequests);
    }
}
