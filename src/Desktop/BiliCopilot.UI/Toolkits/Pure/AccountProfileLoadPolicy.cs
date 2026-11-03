// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 资料加载失败后要执行的处理动作.
/// </summary>
public enum AccountProfileLoadDecision
{
    /// <summary>
    /// 保留当前登录态，再尝试加载一次.
    /// </summary>
    Retry,

    /// <summary>
    /// 清理登录态并重启应用.
    /// </summary>
    SignOutAndRestart,

    /// <summary>
    /// 保留登录态，直接退出应用.
    /// </summary>
    Exit,
}

/// <summary>
/// 根据资料加载失败的类别与用户的选择决定后续动作.
/// </summary>
/// <remarks>
/// 网络类失败永远不能触发退出登录：只有分类器确证登录态失效、
/// 且用户主动选择重新登录时，才允许清理登录态.
/// </remarks>
public static class AccountProfileLoadPolicy
{
    /// <summary>
    /// 计算资料加载失败后的处理动作.
    /// </summary>
    /// <param name="kind">失败类别.</param>
    /// <param name="userChosePrimaryButton">用户是否选择了对话框的主按钮.</param>
    /// <returns>应执行的处理动作.</returns>
    public static AccountProfileLoadDecision Decide(AccountProfileLoadFailureKind kind, bool userChosePrimaryButton)
        => kind switch
        {
            AccountProfileLoadFailureKind.AuthenticationExpired when userChosePrimaryButton
                => AccountProfileLoadDecision.SignOutAndRestart,
            AccountProfileLoadFailureKind.Transient when userChosePrimaryButton
                => AccountProfileLoadDecision.Retry,
            _ => AccountProfileLoadDecision.Exit,
        };
}
