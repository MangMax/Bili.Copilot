// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 记录 SMTC 入队回调最终有没有执行播放器命令；真实命令需要 WinUI，测试只记录动作。
/// </summary>
public sealed partial class PlayerViewModel
{
    internal List<SmtcPlayerAction> SmtcActions { get; } = [];

    partial void ExecuteSmtcPlayerAction(SmtcPlayerAction action) => SmtcActions.Add(action);
}
