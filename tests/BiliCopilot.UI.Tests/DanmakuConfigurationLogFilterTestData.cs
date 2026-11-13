// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 弹幕配置日志过滤测试共用的真实采集数据。
/// </summary>
internal static class DanmakuConfigurationLogFilterTestData
{
    public const string DanmakuSourceContext = "Richasy.Danmaku.DanmakuRenderer";

    public const string NoiseTemplate = "[Danmaku] DanmakuDirectXRender.cs(43)->UpdateConfiguration(): Configuration updated";

    public const string OutputTemplate = "{Message:lj}{NewLine}";
}
