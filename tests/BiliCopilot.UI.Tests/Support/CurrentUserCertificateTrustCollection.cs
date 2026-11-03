// Copyright (c) Bili Copilot. All rights reserved.

using Xunit;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// TLS 证书测试共用当前用户的根证书存储（StoreName.Root / StoreLocation.CurrentUser），
/// 这是进程级共享状态：信任与撤销必须串行，否则并行运行的测试会看到对方临时安装的证书.
/// 因此相关测试类归入同一个禁用并行的集合，保证结果确定.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CurrentUserCertificateTrustCollection
{
    /// <summary>
    /// 集合名称，测试类用 <c>[Collection(Name)]</c> 加入.
    /// </summary>
    public const string Name = "CurrentUserCertificateTrust";
}
