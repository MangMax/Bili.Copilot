// Copyright (c) Bili Copilot. All rights reserved.

using System.Diagnostics;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Runs a download and opens its folder after successful completion.
/// </summary>
internal static class DownloadProcessRunner
{
    /// <summary>
    /// Takes ownership of an unstarted process. Disposing it never terminates the child.
    /// </summary>
    public static async Task<int> RunAsync(Process process, bool openFolderAfterDownload, Action openFolder)
    {
        int exitCode;
        using (process)
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The download process did not start.");
            }

            // No window-lifetime cancellation: closing the UI must not terminate downloads.
            await process.WaitForExitAsync().ConfigureAwait(false);
            exitCode = process.ExitCode;
        }

        if (exitCode == 0 && openFolderAfterDownload)
        {
            openFolder();
        }

        return exitCode;
    }
}
