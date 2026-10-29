using System.Diagnostics;
using BiliCopilot.UI.Toolkits;
using Xunit.Abstractions;

namespace BiliCopilot.UI.Tests;

public sealed class DownloadProcessRunnerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SuccessfulDownloadOpensFolderAfterExitAndReleasesHandle()
    {
        using var process = CreateDownload();
        var opened = 0;
        var operation = DownloadProcessRunner.RunAsync(process, true, () => opened++);
        // The started child cannot exit until stdin is supplied or closed.
        var handle = process.SafeHandle;
        Assert.False(operation.IsCompleted);
        Assert.Equal(0, opened);
        await process.StandardInput.WriteLineAsync("finish");
        await operation.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(1, opened);
        Assert.True(handle.IsClosed);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, true)]
    [InlineData(7, false)]
    public async Task DisabledFolderOrFailedDownloadDoesNotOpenFolder(int exitCode, bool openFolder)
    {
        using var process = CreateDownload(exitCode);
        var opened = 0;
        var operation = DownloadProcessRunner.RunAsync(process, openFolder, () => opened++);
        var handle = process.SafeHandle;
        Assert.False(operation.IsCompleted);
        Assert.False(process.HasExited);
        Assert.Equal(0, opened);
        await process.StandardInput.WriteLineAsync("finish");
        Assert.Equal(exitCode, await operation.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(0, opened);
        Assert.True(handle.IsClosed);
    }

    [Fact]
    public async Task StartFailureDoesNotOpenFolderAndDisposesProcess()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        var opened = 0;
        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(
            () => DownloadProcessRunner.RunAsync(process, true, () => opened++));
        Assert.Equal(0, opened);
        Assert.Throws<ObjectDisposedException>(() => process.Start());
    }

    [Fact]
    public async Task FolderOpenFailureStillReleasesDownloadHandle()
    {
        using var process = CreateDownload();
        var operation = DownloadProcessRunner.RunAsync(process, true, () => throw new IOException("Controlled folder failure"));
        var handle = process.SafeHandle;
        await process.StandardInput.WriteLineAsync("finish");
        await Assert.ThrowsAsync<IOException>(() => operation.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.True(handle.IsClosed);
    }

    [Fact]
    public async Task RepeatedDownloadsReleaseHandlesAndRecordWaitingResources()
    {
        // Warm process/async infrastructure before collecting descriptive (not threshold) samples.
        await CompleteBatch(1);
        Sample("warm");
        for (var batch = 1; batch <= 3; batch++)
        {
            await CompleteBatch(10);
            Sample($"completed-{batch * 10}");
        }
    }

    private async Task CompleteBatch(int count)
    {
        var children = Enumerable.Range(0, count).Select(_ => CreateDownload()).ToArray();
        var opened = 0;
        try
        {
            var operations = children.Select(p => DownloadProcessRunner.RunAsync(p, true, () => Interlocked.Increment(ref opened))).ToArray();
            var handles = children.Select(p => p.SafeHandle).ToArray();
            Assert.All(operations, operation => Assert.False(operation.IsCompleted));
            Assert.Equal(0, opened);
            Sample($"waiting-{count}");
            foreach (var child in children)
            {
                await child.StandardInput.WriteLineAsync("finish");
            }

            Assert.All(await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(15)), code => Assert.Equal(0, code));
            Assert.Equal(count, opened);
            Assert.All(handles, handle => Assert.True(handle.IsClosed));
        }
        finally
        {
            // Closing stdin lets a child exit even if an assertion fails. No Kill policy.
            foreach (var child in children) child.Dispose();
        }
    }

    private void Sample(string phase)
    {
        using var host = Process.GetCurrentProcess();
        var waiting = 0;
        foreach (ProcessThread thread in host.Threads)
        {
            using (thread)
            {
                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait) waiting++;
            }
        }

        ThreadPool.GetAvailableThreads(out var available, out _);
        ThreadPool.GetMaxThreads(out var maximum, out _);
        output.WriteLine($"{phase}: handles={host.HandleCount}, OS threads={host.Threads.Count}, OS waiting={waiting}, pool busy={maximum - available}, pool threads={ThreadPool.ThreadCount}");
    }

    private static Process CreateDownload(int exitCode = 0) => new()
    {
        StartInfo = new ProcessStartInfo("cmd.exe", $"/d /q /c \"set /p gate= & exit /b {exitCode}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
        },
    };
}
