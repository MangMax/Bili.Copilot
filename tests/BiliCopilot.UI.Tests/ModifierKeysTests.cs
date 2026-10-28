using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 播放器快捷键修饰键组合的判定规则。
/// </summary>
public class ModifierKeysTests
{
    [Theory]
    [InlineData(false, false, false, false, true)] // 无修饰键：方向键按普通快捷键处理
    [InlineData(true, false, false, false, false)] // 仅 Ctrl
    [InlineData(false, true, false, false, false)] // 仅 Shift
    [InlineData(false, false, true, false, false)] // 仅 Alt
    [InlineData(false, false, false, true, false)] // 仅 Win：不能当作普通方向键
    [InlineData(true, false, false, true, false)] // Win + Ctrl
    [InlineData(false, true, false, true, false)] // Win + Shift
    [InlineData(false, false, true, true, false)] // Win + Alt
    [InlineData(true, true, false, false, false)] // Ctrl + Shift
    public void NotModifierKeyPressed_ReturnsTrueOnlyWhenNoModifierIsHeld(
        bool ctrl,
        bool shift,
        bool alt,
        bool win,
        bool expected)
    {
        Assert.Equal(expected, ModifierKeys.NotModifierKeyPressed(ctrl, shift, alt, win));
    }

    [Theory]
    [InlineData(true, false, false, false, true)] // Ctrl 单独按下
    [InlineData(true, false, true, false, false)] // Ctrl + Alt
    [InlineData(true, true, false, false, false)] // Ctrl + Shift
    [InlineData(true, false, false, true, false)] // Win + Ctrl：属于系统组合键
    [InlineData(true, true, true, true, false)] // 全部按下
    [InlineData(false, false, false, false, false)] // 没有 Ctrl
    public void IsOnlyCtrlPressed_RequiresCtrlAlone(
        bool ctrl,
        bool shift,
        bool alt,
        bool win,
        bool expected)
    {
        Assert.Equal(expected, ModifierKeys.IsOnlyCtrlPressed(ctrl, shift, alt, win));
    }

    [Theory]
    [InlineData(false, true, false, false, true)] // Shift 单独按下
    [InlineData(true, true, false, false, false)] // Ctrl + Shift
    [InlineData(false, true, true, false, false)] // Shift + Alt
    [InlineData(false, true, false, true, false)] // Win + Shift：属于系统组合键
    [InlineData(false, false, false, false, false)] // 没有 Shift
    public void IsOnlyShiftPressed_RequiresShiftAlone(
        bool ctrl,
        bool shift,
        bool alt,
        bool win,
        bool expected)
    {
        Assert.Equal(expected, ModifierKeys.IsOnlyShiftPressed(ctrl, shift, alt, win));
    }

    [Theory]
    [InlineData(false, false, true, false, true)] // Alt 单独按下
    [InlineData(true, false, true, false, false)] // Ctrl + Alt
    [InlineData(false, true, true, false, false)] // Shift + Alt
    [InlineData(false, false, true, true, false)] // Win + Alt：属于系统组合键
    [InlineData(false, false, false, false, false)] // 没有 Alt
    public void IsOnlyAltPressed_RequiresAltAlone(
        bool ctrl,
        bool shift,
        bool alt,
        bool win,
        bool expected)
    {
        Assert.Equal(expected, ModifierKeys.IsOnlyAltPressed(ctrl, shift, alt, win));
    }
}
