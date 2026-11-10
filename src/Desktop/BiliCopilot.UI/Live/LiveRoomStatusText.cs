// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播间状态与在线人数的纯格式化逻辑。
/// </summary>
/// <remarks>
/// 与 WinUI 无关：单位与模板由调用方传入，测试可直接断言输出。
/// 在线人数与 <c>AppToolkit.FormatCount</c> 使用同一套规则（万/亿、两位小数），
/// 开播时长按分钟/小时展示，不复用视频的 <c>hh:mm:ss</c> 进度格式。
/// </remarks>
public static class LiveRoomStatusText
{
    /// <summary>
    /// 按中文单位格式化在线人数。
    /// </summary>
    /// <param name="count">人数.</param>
    /// <param name="tenThousandsUnit">万单位文案.</param>
    /// <param name="hundredMillionsUnit">亿单位文案.</param>
    /// <returns>格式化文本；负数返回空字符串.</returns>
    public static string FormatCount(long count, string tenThousandsUnit, string hundredMillionsUnit)
    {
        if (count < 0)
        {
            return string.Empty;
        }

        if (count >= 100_000_000)
        {
            return Math.Round(count / 100_000_000d, 2) + hundredMillionsUnit;
        }

        if (count >= 10_000)
        {
            return Math.Round(count / 10_000d, 2) + tenThousandsUnit;
        }

        return count.ToString();
    }

    /// <summary>
    /// 格式化开播时长：不足一小时只显示分钟，超过一小时显示小时与分钟。
    /// </summary>
    /// <param name="duration">已开播时长.</param>
    /// <param name="minutesFormat">分钟模板，如 <c>{0}分钟</c>.</param>
    /// <param name="hoursFormat">小时模板，如 <c>{0}小时</c>.</param>
    /// <param name="hoursMinutesFormat">小时加分钟模板，如 <c>{0}小时{1}分钟</c>.</param>
    /// <returns>格式化文本.</returns>
    public static string FormatDuration(TimeSpan duration, string minutesFormat, string hoursFormat, string hoursMinutesFormat)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        if (hours <= 0)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, minutesFormat, minutes);
        }

        return minutes == 0
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, hoursFormat, hours)
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, hoursMinutesFormat, hours, minutes);
    }
}
