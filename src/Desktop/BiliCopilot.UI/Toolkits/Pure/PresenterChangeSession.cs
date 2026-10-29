// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Owns delayed presenter settling for one window on its UI context.
/// </summary>
internal sealed class PresenterChangeSession(Func<Task> delay)
{
    private long _generation;
    private bool _closed;

    /// <summary>
    /// Gets whether the latest presenter change is still settling.
    /// </summary>
    public bool IsChanging { get; private set; }

    /// <summary>
    /// Ends this window's presenter session permanently.
    /// </summary>
    public void Close()
    {
        _closed = true;
        _generation++;
        IsChanging = false;
    }

    /// <summary>
    /// Applies a presenter synchronously, then awaits its settling interval.
    /// </summary>
    /// <param name="applyPresenter">The window's presenter change.</param>
    /// <returns>The settling operation.</returns>
    public Task ChangeAsync(Action applyPresenter)
    {
        if (_closed)
        {
            return Task.CompletedTask;
        }

        var generation = ++_generation;
        IsChanging = true;
        try
        {
            applyPresenter();
        }
        catch
        {
            if (generation == _generation)
            {
                IsChanging = false;
            }

            throw;
        }

        return SettleAsync(generation);
    }

    private async Task SettleAsync(long generation)
    {
        // Retain the caller's UI context. No window callback survives the await.
        await delay();
        if (generation == _generation)
        {
            IsChanging = false;
        }
    }
}
