// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow
{
    private readonly CompactOverlayRatio _ratio;

    internal long BeginRatioMedia() => _ratio.BeginMedia();

    internal void UpdateRatioMedia(long generation, double width, double height)
        => _ratio.UpdateMedia(generation, width, height);
}
