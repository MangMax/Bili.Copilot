// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Services.MomentNotifications;
using BiliCopilot.Visor.Models;
using Richasy.AgentKernel;
using System.Text.Json.Serialization;

namespace BiliCopilot.UI;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ChatClientConfiguration))]
[JsonSerializable(typeof(PinItem))]
[JsonSerializable(typeof(List<PinItem>))]
[JsonSerializable(typeof(VisorCommand))]
[JsonSerializable(typeof(VisorResponse))]
[JsonSerializable(typeof(MomentNotificationState))]
internal sealed partial class GlobalSerializeContext : JsonSerializerContext
{
}
