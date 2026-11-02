// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Shares one asynchronous load, allowing a later retry after failure.</summary>
internal sealed class AsyncValueCache<T>
{
    private readonly object _gate = new();
    private Task<T>? _task;

    public Task<T> GetAsync(Func<Task<T>> load)
    {
        lock (_gate)
        {
            if (_task is null || _task.IsFaulted || _task.IsCanceled)
            {
                _task = load();
            }

            return _task;
        }
    }
}
