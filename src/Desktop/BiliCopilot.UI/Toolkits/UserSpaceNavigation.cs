// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.User;
using System.Windows.Input;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 卡片作者入口共用的用户空间导航：先准备头像串联，再执行卡片原有的导航命令.
/// </summary>
public static class UserSpaceNavigation
{
    /// <summary>
    /// 从卡片的作者入口进入用户空间.
    /// </summary>
    /// <param name="source">卡片上可见的头像元素；模板没有头像时可为 <see langword="null"/>.</param>
    /// <param name="profile">卡片对应的用户资料，必须与导航命令使用的数据源一致.</param>
    /// <param name="command">卡片已有的用户空间导航命令.</param>
    public static void Open(FrameworkElement? source, UserProfile? profile, ICommand command)
    {
        // 头像缺失、动画被关闭或资料不完整时 Prepare 会自行跳过，导航照常执行.
        AvatarConnectedAnimation.Prepare(source, profile);
        command.Execute(default);
    }
}
