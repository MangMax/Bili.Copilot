// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 修饰键组合的纯逻辑判定。
/// </summary>
/// <remarks>
/// 按键状态由 <see cref="AppToolkit"/> 通过 P/Invoke 读取，这里只负责组合规则，
/// 以便在无 WinUI 依赖的测试项目中验证。
/// </remarks>
internal static class ModifierKeys
{
    /// <summary>
    /// 是否只按下了 Ctrl。
    /// </summary>
    public static bool IsOnlyCtrlPressed(bool isCtrlPressed, bool isShiftPressed, bool isAltPressed, bool isWinPressed)
        => isCtrlPressed && !isShiftPressed && !isAltPressed && !isWinPressed;

    /// <summary>
    /// 是否只按下了 Shift。
    /// </summary>
    public static bool IsOnlyShiftPressed(bool isCtrlPressed, bool isShiftPressed, bool isAltPressed, bool isWinPressed)
        => !isCtrlPressed && isShiftPressed && !isAltPressed && !isWinPressed;

    /// <summary>
    /// 是否只按下了 Alt。
    /// </summary>
    public static bool IsOnlyAltPressed(bool isCtrlPressed, bool isShiftPressed, bool isAltPressed, bool isWinPressed)
        => !isCtrlPressed && !isShiftPressed && isAltPressed && !isWinPressed;

    /// <summary>
    /// 是否没有按下任何修饰键（含 Win）。
    /// </summary>
    public static bool NotModifierKeyPressed(bool isCtrlPressed, bool isShiftPressed, bool isAltPressed, bool isWinPressed)
        => !isCtrlPressed && !isShiftPressed && !isAltPressed && !isWinPressed;
}
