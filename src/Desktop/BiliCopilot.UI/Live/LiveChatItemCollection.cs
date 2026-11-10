// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;

namespace BiliCopilot.UI.Live;

/// <summary>A bounded chat projection that keeps surviving rows and their containers.</summary>
public sealed class LiveChatItemCollection<T> : ObservableCollection<T>
{
    /// <summary>Appends one row.</summary>
    public void Append(T item) => Add(item);

    /// <summary>Removes old rows without resetting the virtualized view.</summary>
    public void RemoveFromStart(int count)
    {
        count = Math.Min(count, Count);
        for (var i = 0; i < count; i++)
        {
            RemoveAt(0);
        }
    }

    /// <summary>Clears rows when leaving a room.</summary>
    public void ClearAll()
    {
        if (Count > 0)
        {
            Clear();
        }
    }
}
