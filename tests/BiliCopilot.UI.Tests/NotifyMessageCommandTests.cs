// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.User;
using Windows.System;

namespace BiliCopilot.UI.Tests;

public sealed class NotifyMessageCommandTests : IDisposable
{
    public void Dispose() => Launcher.Handler.Value = null;

    [Fact]
    public void ActiveCommand_HasStablePerInstanceIdentityAndAsyncContract()
    {
        var first = Create(null);
        var second = Create(null);
        IAsyncRelayCommand command = first.ActiveCommand;
        Assert.IsType<AsyncRelayCommand>(command);
        Assert.Same(command, first.ActiveCommand);
        Assert.NotSame(command, second.ActiveCommand);
        Assert.Null(command.ExecutionTask);
        Assert.False(command.IsRunning);
        Assert.False(command.CanBeCanceled);
        Assert.True(command.CanExecute(new object()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("av123")]
    [InlineData("HTTPS://example.test/item")]
    public async Task ActiveCommand_NonHttpSourceDoesNotLaunch(string? source)
    {
        Launcher.Handler.Value = _ => throw new InvalidOperationException("Must not launch");
        var command = Create(source).ActiveCommand;
        await command.ExecuteAsync(new object());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.True(command.CanExecute(null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActiveCommand_LaunchesSourceAndIgnoresCommandParameterAndLaunchResult(bool result)
    {
        var calls = new List<Uri>();
        Launcher.Handler.Value = uri => { calls.Add(uri); return Task.FromResult(result); };
        var vm = Create("https://example.test/item");
        vm.ActiveCommand.Execute("https://wrong.test/parameter");
        await vm.ActiveCommand.ExecutionTask!;
        Assert.Equal(new Uri("https://example.test/item"), Assert.Single(calls));
        Assert.True(vm.ActiveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ActiveCommand_PendingLaunchDisablesCanExecuteButDirectExecutionStillRuns()
    {
        var completions = new List<TaskCompletionSource<bool>>();
        Launcher.Handler.Value = _ =>
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completions.Add(completion);
            return completion.Task;
        };
        var command = Create("https://example.test/item").ActiveCommand;
        var states = new List<bool>();
        var enabledAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        command.CanExecuteChanged += (_, _) =>
        {
            var enabled = command.CanExecute(null);
            lock (states) { states.Add(enabled); }
            if (enabled) enabledAgain.TrySetResult();
        };
        var first = command.ExecuteAsync(null);
        Assert.Same(first, command.ExecutionTask);
        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(new object()));
        Assert.False(command.CanBeCanceled);
        command.Cancel();
        Assert.False(command.IsCancellationRequested);
        // MVVM Toolkit gates UI through CanExecute; ExecuteAsync itself is not a lock.
        var second = command.ExecuteAsync(new object());
        Assert.Equal(2, completions.Count);
        Assert.Same(second, command.ExecutionTask);
        completions[0].SetResult(true);
        await first;
        Assert.False(command.CanExecute(null));
        completions[1].SetResult(true);
        await second;
        await enabledAgain.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
        lock (states) { Assert.Contains(false, states); Assert.Contains(true, states); }
    }

    [Fact]
    public async Task ActiveCommand_ExecuteAsyncPreservesLaunchException()
    {
        var expected = new InvalidOperationException("launch failed");
        Launcher.Handler.Value = _ => Task.FromException<bool>(expected);
        var command = Create("https://example.test/item").ActiveCommand;
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(null));
        Assert.Same(expected, actual);
        Assert.True(command.ExecutionTask!.IsFaulted);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void ActiveCommand_ExecuteRethrowsOnCallingSynchronizationContext()
    {
        var expected = new InvalidOperationException("launch failed");
        Launcher.Handler.Value = _ => Task.FromException<bool>(expected);
        var context = new CapturingContext();
        var previous = SynchronizationContext.Current;
        var command = Create("https://example.test/item").ActiveCommand;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            command.Execute(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var callback = Assert.Single(context.Callbacks);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => callback.Callback(callback.State)));
        Assert.Same(expected, command.ExecutionTask!.Exception!.InnerException);
    }

    private sealed class CapturingContext : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Callbacks { get; } = [];
        public override void Post(SendOrPostCallback d, object? state) => Callbacks.Add((d, state));
    }

    [Fact]
    public async Task ActiveCommand_InvalidHttpUriFaultsBeforeLaunch()
    {
        var calls = 0;
        Launcher.Handler.Value = _ => { calls++; return Task.FromResult(true); };
        await Assert.ThrowsAsync<UriFormatException>(() => Create("http://[").ActiveCommand.ExecuteAsync(null));
        Assert.Equal(0, calls);
    }

    private static NotifyMessageItemViewModel Create(string? source) => new(new NotifyMessage(
        "notification", NotifyMessageType.Reply,
        [new UserProfile("user", "Name", new BiliImage(new Uri("https://example.test/avatar")))],
        DateTimeOffset.UtcNow, "business", "message", "source", source));
}
