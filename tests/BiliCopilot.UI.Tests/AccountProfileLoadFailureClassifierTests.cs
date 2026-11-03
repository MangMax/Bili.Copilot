// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 账号资料加载失败的分类判定。
/// </summary>
/// <remarks>
/// 资料加载失败既可能是网络抖动，也可能是登录态失效。只有确认是后者，
/// 才能让用户重新登录并清理登录态；其余情况一律按可重试的网络错误处理，
/// 避免一次离线就把登录态清掉。
/// 内核的异常类型不在测试工程的引用范围内，这里用普通异常模拟同样的包装结构。
/// </remarks>
public class AccountProfileLoadFailureClassifierTests
{
    [Fact]
    public void Classify_Null_ReturnsTransient()
        => Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(null));

    [Fact]
    public void Classify_SocketFailureWrappedByKernel_ReturnsTransient()
    {
        var exception = new InvalidOperationException("请求失败", new System.Net.Sockets.SocketException(10061));

        Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Classify_HttpFailureWithoutStatusWrappedByKernel_ReturnsTransient()
    {
        var exception = new InvalidOperationException("请求失败", new HttpRequestException("由于目标计算机积极拒绝，无法连接。"));

        Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Classify_TaskCanceledByHttpClientTimeout_ReturnsTransient()
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(new TaskCanceledException("请求超时")));

    [Fact]
    public void Classify_TimeoutException_ReturnsTransient()
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(new TimeoutException("操作超时")));

    [Fact]
    public void Classify_IoFailure_ReturnsTransient()
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(new IOException("连接被重置")));

    [Fact]
    public void Classify_UnauthorizedStatus_ReturnsAuthenticationExpired()
        => Assert.Equal(
            AccountProfileLoadFailureKind.AuthenticationExpired,
            AccountProfileLoadFailureClassifier.Classify(
                new HttpRequestException("响应状态代码不表示成功: 401 (Unauthorized)", null, System.Net.HttpStatusCode.Unauthorized)));

    [Fact]
    public void Classify_ForbiddenStatus_ReturnsTransient()
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(
                new HttpRequestException("响应状态代码不表示成功: 403 (Forbidden)", null, System.Net.HttpStatusCode.Forbidden)));

    [Fact]
    public void Classify_UnauthorizedStatusWrappedByKernel_ReturnsAuthenticationExpired()
    {
        // 内核把 HttpRequestException 包进 KernelException，401 只有沿异常链内层才能看到。
        var exception = new InvalidOperationException(
            "请求失败",
            new HttpRequestException("响应状态代码不表示成功: 401 (Unauthorized)", null, System.Net.HttpStatusCode.Unauthorized));

        Assert.Equal(AccountProfileLoadFailureKind.AuthenticationExpired, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("需要令牌，但令牌不存在，请重新登录")]
    [InlineData("令牌已过期，请重新登录")]
    [InlineData("没有本地缓存的令牌信息，请重新登录")]
    [InlineData("令牌信息不完整，请重新登录")]
    public void Classify_MessageAskingToReSignIn_ReturnsAuthenticationExpired(string message)
        => Assert.Equal(
            AccountProfileLoadFailureKind.AuthenticationExpired,
            AccountProfileLoadFailureClassifier.Classify(new InvalidOperationException(message)));

    [Fact]
    public void Classify_MessageSayingLoginIsInvalid_ReturnsAuthenticationExpired()
        => Assert.Equal(
            AccountProfileLoadFailureKind.AuthenticationExpired,
            AccountProfileLoadFailureClassifier.Classify(new InvalidOperationException("登录已失效，请重新扫码")));

    [Fact]
    public void Classify_AuthenticationMarkerTwoLevelsDeep_ReturnsAuthenticationExpired()
    {
        var exception = new InvalidOperationException(
            "资料加载失败",
            new InvalidOperationException("内核调用失败", new InvalidOperationException("令牌已过期，请重新登录")));

        Assert.Equal(AccountProfileLoadFailureKind.AuthenticationExpired, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("哔哩哔哩返回了一个异常响应: 请求错误")]
    [InlineData("返回的用户数据为空")]
    [InlineData("未知错误：对象引用未设置为对象的实例")]
    public void Classify_OrdinaryServiceError_ReturnsTransient(string message)
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(new InvalidOperationException(message)));

    [Theory]
    [InlineData("{\"code\":-101,\"message\":\"账号未登录\"}")]
    [InlineData("{\"code\": -101, \"message\": \"账号未登录\"}")]
    public void Classify_ApiBodyReportingNoLogin_ReturnsAuthenticationExpired(string apiBody)
    {
        var exception = new InvalidOperationException("哔哩哔哩返回了一个异常响应: 请求失败", new Exception(apiBody));

        Assert.Equal(AccountProfileLoadFailureKind.AuthenticationExpired, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("{\"code\":-111,\"message\":\"csrf 校验失败\"}")]
    [InlineData("{\"code\": -111, \"message\": \"csrf 校验失败\"}")]
    public void Classify_ApiBodyReportingCsrfRejection_ReturnsTransient(string apiBody)
    {
        // csrf 被拒只说明这次请求缺少校验，不代表凭据失效，不能清除登录态。
        var exception = new InvalidOperationException("哔哩哔哩返回了一个异常响应: 请求失败", new Exception(apiBody));

        Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("{\"code\":-400,\"message\":\"请求错误\"}")]
    [InlineData("{\"code\":0,\"message\":\"0\"}")]
    [InlineData("{\"code\":-509,\"message\":\"请求过于频繁\"}")]
    public void Classify_ApiBodyReportingOrdinaryError_ReturnsTransient(string apiBody)
    {
        var exception = new InvalidOperationException("哔哩哔哩返回了一个异常响应: 请求失败", new Exception(apiBody));

        Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Classify_ServerErrorStatus_ReturnsTransient()
        => Assert.Equal(
            AccountProfileLoadFailureKind.Transient,
            AccountProfileLoadFailureClassifier.Classify(
                new HttpRequestException("响应状态代码不表示成功: 500 (Internal Server Error)", null, System.Net.HttpStatusCode.InternalServerError)));

    [Fact]
    public void Classify_AggregateOfNetworkFailures_ReturnsTransient()
    {
        var exception = new AggregateException(
            new System.Net.Sockets.SocketException(10054),
            new TaskCanceledException("请求超时"));

        Assert.Equal(AccountProfileLoadFailureKind.Transient, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Classify_AggregateContainingAuthenticationFailure_ReturnsAuthenticationExpired()
    {
        var exception = new AggregateException(
            new System.Net.Sockets.SocketException(10054),
            new InvalidOperationException("令牌已过期，请重新登录"));

        Assert.Equal(AccountProfileLoadFailureKind.AuthenticationExpired, AccountProfileLoadFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Classify_AuthenticationMarkerBuriedDeepInChain_ReturnsAuthenticationExpired()
    {
        Exception exception = new InvalidOperationException("令牌已过期，请重新登录");
        for (var level = 0; level < 10; level++)
        {
            exception = new InvalidOperationException($"第 {level} 层包装", exception);
        }

        Assert.Equal(AccountProfileLoadFailureKind.AuthenticationExpired, AccountProfileLoadFailureClassifier.Classify(exception));
    }
}
