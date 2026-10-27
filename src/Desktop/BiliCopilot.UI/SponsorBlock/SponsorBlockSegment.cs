// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手的单个可跳过片段.
/// </summary>
public sealed class SponsorBlockSegment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockSegment"/> class.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="start">开始时间（秒）.</param>
    /// <param name="end">结束时间（秒）.</param>
    /// <param name="uuid">片段唯一标识.</param>
    public SponsorBlockSegment(
        SponsorBlockCategory category,
        double start,
        double end,
        string? uuid = null)
    {
        Category = category;
        Start = start;
        End = end;
        Uuid = uuid;
    }

    /// <summary>
    /// 片段类别.
    /// </summary>
    public SponsorBlockCategory Category { get; }

    /// <summary>
    /// 开始时间（秒）.
    /// </summary>
    public double Start { get; }

    /// <summary>
    /// 结束时间（秒）.
    /// </summary>
    public double End { get; }

    /// <summary>
    /// 片段唯一标识.
    /// </summary>
    public string? Uuid { get; }

    /// <summary>
    /// 片段时长（秒）.
    /// </summary>
    public double Duration => End - Start;

    /// <summary>
    /// 判断时间区间是否有效.
    /// </summary>
    /// <param name="start">开始时间（秒）.</param>
    /// <param name="end">结束时间（秒）.</param>
    /// <returns>区间有效时返回 <c>true</c>.</returns>
    public static bool IsValidRange(double start, double end)
        => !double.IsNaN(start)
            && !double.IsNaN(end)
            && !double.IsInfinity(start)
            && !double.IsInfinity(end)
            && start >= 0
            && end > start;
}
