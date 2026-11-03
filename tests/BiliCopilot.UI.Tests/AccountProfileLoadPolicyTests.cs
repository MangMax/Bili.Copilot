// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 资料加载失败后的处理决策。
/// </summary>
/// <remarks>
/// 网络失败只允许重试或退出，任何情况下都不能清理登录态；
/// 只有确证登录态失效、且用户主动选择重新登录时才允许退出登录。
/// </remarks>
public class AccountProfileLoadPolicyTests
{
    [Theory]
    [InlineData(AccountProfileLoadFailureKind.Transient, true, AccountProfileLoadDecision.Retry)]
    [InlineData(AccountProfileLoadFailureKind.Transient, false, AccountProfileLoadDecision.Exit)]
    [InlineData(AccountProfileLoadFailureKind.AuthenticationExpired, true, AccountProfileLoadDecision.SignOutAndRestart)]
    [InlineData(AccountProfileLoadFailureKind.AuthenticationExpired, false, AccountProfileLoadDecision.Exit)]
    public void Decide_KindAndChoice_ReturnsExpectedDecision(
        AccountProfileLoadFailureKind kind,
        bool userChosePrimaryButton,
        AccountProfileLoadDecision expected)
        => Assert.Equal(expected, AccountProfileLoadPolicy.Decide(kind, userChosePrimaryButton));

    [Fact]
    public void Decide_AnyTransientChoice_NeverSignsOutAndRestarts()
    {
        foreach (var userChosePrimaryButton in new[] { true, false })
        {
            var decision = AccountProfileLoadPolicy.Decide(AccountProfileLoadFailureKind.Transient, userChosePrimaryButton);

            Assert.NotEqual(AccountProfileLoadDecision.SignOutAndRestart, decision);
        }
    }

    [Fact]
    public void Decide_AuthenticationExpiredWithPrimaryChoice_SignsOutAndRestarts()
        => Assert.Equal(
            AccountProfileLoadDecision.SignOutAndRestart,
            AccountProfileLoadPolicy.Decide(AccountProfileLoadFailureKind.AuthenticationExpired, true));
}
