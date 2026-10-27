// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.Json.Serialization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手 JSON 源生成上下文，保持 AOT 友好.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<SponsorBlockSegmentDto>))]
internal sealed partial class SponsorBlockJsonContext : JsonSerializerContext
{
}
