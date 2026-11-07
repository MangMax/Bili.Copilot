// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 醒目留言的价格与剩余时间格式化：纯函数，时间由调用方传入，便于用假时钟测试。
/// </summary>
public static class LiveChatSuperChatTime
{
    /// <summary>
    /// 计算剩余时间；已结束时返回零。
    /// </summary>
    /// <param name="endTime">结束时间.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>剩余时间，最小为零.</returns>
    public static TimeSpan Remaining(DateTimeOffset endTime, DateTimeOffset now)
    {
        var remaining = endTime - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 判断醒目留言是否已经结束。
    /// </summary>
    /// <param name="endTime">结束时间.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>已结束时为 true.</returns>
    public static bool IsExpired(DateTimeOffset endTime, DateTimeOffset now) => endTime <= now;

    /// <summary>
    /// 把剩余时间格式化为 <c>分:秒</c>，超过一小时按总分钟数显示。
    /// </summary>
    /// <param name="remaining">剩余时间.</param>
    /// <returns>格式化文本，例如 <c>1:05</c>.</returns>
    public static string FormatRemaining(TimeSpan remaining)
    {
        var total = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        var minutes = (int)total.TotalMinutes;
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, total.Seconds);
    }

    /// <summary>
    /// 把金额格式化为带货币符号的文本。
    /// </summary>
    /// <param name="price">金额（元）.</param>
    /// <returns>格式化文本，例如 <c>¥30</c>.</returns>
    public static string FormatPrice(int price) => FormatPrice(price, DefaultPriceTemplate);

    /// <summary>
    /// 用指定的模板格式化金额；界面传入本地化模板，纯逻辑层使用默认模板。
    /// </summary>
    /// <param name="price">金额（元）.</param>
    /// <param name="template">带 <c>{0}</c> 占位符的模板.</param>
    /// <returns>格式化文本.</returns>
    public static string FormatPrice(int price, string template)
        => string.Format(CultureInfo.InvariantCulture, template, price);

    /// <summary>
    /// Gets 默认金额模板（中文界面的 <c>¥</c> 前缀）.
    /// </summary>
    public static string DefaultPriceTemplate => "¥{0}";
}
