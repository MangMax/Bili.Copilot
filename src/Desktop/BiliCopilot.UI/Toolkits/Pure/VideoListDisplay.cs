// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 把视图集合对齐到「真实卡片 + 骨架占位」的期望序列.
/// </summary>
/// <remarks>
/// 骨架只是列表里的占位项：每张卡片到达就顶掉对应位置的骨架，用户看到的是逐格替换。
/// 占位不进入业务列表，也不参与撤销快照与差异计算。
/// </remarks>
public static class VideoListDisplay
{
    /// <summary>
    /// 布局尚未完成时先按这个数量铺骨架，等真实尺寸算出来后立刻校正.
    /// </summary>
    public const int InitialSkeletonEstimate = 16;

    /// <summary>
    /// 按期望序列对齐集合；已有占位项复用，避免每次同步都重建骨架.
    /// </summary>
    /// <param name="display">视图集合.</param>
    /// <param name="desiredCount">期望长度.</param>
    /// <param name="itemAt">按索引取真实条目，返回 <see langword="null"/> 表示该位置放骨架.</param>
    public static void Apply(ObservableCollection<object> display, int desiredCount, Func<int, object?> itemAt)
    {
        for (var i = 0; i < desiredCount; i++)
        {
            var desired = itemAt(i);
            var current = i < display.Count ? display[i] : null;
            if (desired is null)
            {
                if (current is VideoCardSkeleton)
                {
                    continue;
                }

                desired = new VideoCardSkeleton();
            }
            else if (ReferenceEquals(current, desired))
            {
                continue;
            }

            if (i < display.Count)
            {
                display[i] = desired;
            }
            else
            {
                display.Add(desired);
            }
        }

        while (display.Count > desiredCount)
        {
            display.RemoveAt(display.Count - 1);
        }
    }
}