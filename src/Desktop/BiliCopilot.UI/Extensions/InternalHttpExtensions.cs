// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.Extensions;

internal static partial class InternalHttpExtensions
{
    /// <summary>
    /// 视频用户代理.
    /// </summary>
    public const string VideoUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36 Edg/116.0.1938.69";

    /// <summary>
    /// 直播用户代理.
    /// </summary>
    public const string LiveUserAgent = "Mozilla/5.0 BiliDroid/1.12.0 (bbcallen@gmail.com)";

    /// <summary>
    /// 视频来源.
    /// </summary>
    public const string VideoReferer = "https://www.bilibili.com";

    /// <summary>
    /// 直播来源.
    /// </summary>
    public const string LiveReferer = "https://live.bilibili.com";

    public static readonly HttpClient ImageClient = GetImageClient();

    /// <summary>
    /// 获取图片下载客户端.
    /// </summary>
    /// <returns></returns>
    private static HttpClient GetImageClient()
    {
        var handler = new CustomHttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            PreAuthenticate = true,
            ClientCertificateOptions = ClientCertificateOption.Automatic,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
            UseCookies = false, // Cover requests do not rely on cookies
        };

        return CreateImageClient(handler);
    }

    /// <summary>
    /// 用指定的传输处理器构建图片下载客户端.
    /// </summary>
    /// <remarks>
    /// 生产路径不设置任何证书回调，证书校验由平台完成；传输处理器参数只用于测试中
    /// 显式注入“仅信任本机测试服务器证书”的配置，避免测试改写系统证书存储.
    /// </remarks>
    /// <param name="handler">传输处理器，由返回的 <see cref="HttpClient"/> 负责释放.</param>
    /// <returns>图片下载客户端.</returns>
    internal static HttpClient CreateImageClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", VideoUserAgent);
        return client;
    }

    private sealed partial class CustomHttpClientHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Version = HttpVersion.Version20;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
