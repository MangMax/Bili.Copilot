// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天列表的集合：追加只发一条通知，批量裁剪只发一次 Reset。
/// </summary>
/// <remarks>
/// 直接操作 <see cref="ObservableCollection{T}"/> 的底层列表再手动发一次 Reset，
/// 因此一次裁剪不会产生 N 条 Remove 通知，虚拟化列表也不需要逐条重排。
/// </remarks>
/// <typeparam name="T">条目类型.</typeparam>
public sealed class LiveChatItemCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// 追加一条；只发一条 Add 通知。
    /// </summary>
    /// <param name="item">条目.</param>
    public void Append(T item) => Add(item);

    /// <summary>
    /// 用给定条目整体替换当前内容，只发一次 Reset 通知。
    /// </summary>
    /// <param name="items">新的完整条目集合.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = items as IReadOnlyList<T> ?? items.ToList();
        if (snapshot.Count == Count)
        {
            var same = true;
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(this[i], snapshot[i]))
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return;
            }
        }

        Items.Clear();
        foreach (var item in snapshot)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// 从头部移除若干条，只发一次 Reset 通知；保留下来的条目实例不变。
    /// </summary>
    /// <param name="count">移除条数.</param>
    public void RemoveFromStart(int count)
    {
        if (count <= 0 || Items.Count == 0)
        {
            return;
        }

        count = Math.Min(count, Items.Count);
        for (var i = 0; i < count; i++)
        {
            Items.RemoveAt(0);
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// 清空，只发一次 Reset 通知。
    /// </summary>
    public void ClearAll()
    {
        if (Items.Count == 0)
        {
            return;
        }

        Items.Clear();
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
