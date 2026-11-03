// Copyright (c) Bili Copilot. All rights reserved.

using System.Reflection;
using Richasy.BiliKernel.Http;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// Reads the transport handler out of a production client so structural guards can assert that
/// no certificate-validation callback is configured. Reflection is confined to this test helper
/// and fails with an explicit message if the framework/kernel field layout changes.
/// </summary>
internal static class HttpClientHandlerInspector
{
    /// <summary>
    /// Gets the <see cref="HttpClientHandler"/> backing <paramref name="client"/>.
    /// </summary>
    /// <param name="client">Client whose handler is inspected.</param>
    /// <returns>The client's transport handler.</returns>
    public static HttpClientHandler GetHandler(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        // HttpClient keeps its handler on the HttpMessageInvoker base type, so the whole
        // hierarchy must be walked; private fields are not returned by GetFields on a derived type.
        var handler = FindFieldValue<HttpMessageHandler>(client)
            .FirstOrDefault(item => item.Name == "_handler")
            .Value
            ?? FindFieldValue<HttpMessageHandler>(client).Select(item => item.Value).FirstOrDefault();

        if (handler is not HttpClientHandler httpClientHandler)
        {
            throw new InvalidOperationException(
                $"无法从 {nameof(HttpClient)} 读取 {nameof(HttpClientHandler)}（实际处理器：{handler?.GetType().FullName ?? "null"}）。" +
                "证书校验结构守卫依赖该字段，请更新测试辅助类而不是删除守卫.");
        }

        return httpClientHandler;
    }

    /// <summary>
    /// Gets the inner <see cref="HttpClient"/> of a <see cref="BiliHttpClient"/>.
    /// </summary>
    /// <param name="client">Kernel client whose inner client is inspected.</param>
    /// <returns>The inner <see cref="HttpClient"/>.</returns>
    public static HttpClient GetInnerClient(BiliHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var inner = FindFieldValue<HttpClient>(client)
            .FirstOrDefault(item => item.Name == "_client")
            .Value
            ?? FindFieldValue<HttpClient>(client).Select(item => item.Value).FirstOrDefault();

        if (inner is null)
        {
            throw new InvalidOperationException(
                $"无法从 {nameof(BiliHttpClient)} 读取内部 {nameof(HttpClient)}。" +
                "证书校验结构守卫依赖该字段，请更新测试辅助类而不是删除守卫.");
        }

        return inner;
    }

    private static IEnumerable<(string Name, T? Value)> FindFieldValue<T>(object instance)
        where T : class
    {
        for (var current = instance.GetType(); current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return (field.Name, field.GetValue(instance) as T);
            }
        }
    }
}
