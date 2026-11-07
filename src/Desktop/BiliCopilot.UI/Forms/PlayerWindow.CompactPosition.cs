// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow
{
    private sealed class CompactPositionAdapter(AppWindow window) : IPlayerWindowPlacement
    {
        public PlacementBounds Bounds => new(window.Position.X, window.Position.Y, window.Size.Width, window.Size.Height);

        public PlacementPoint? ReadPosition()
        {
            // One scalar setting keeps the coordinate pair atomic and permits (0, 0).
            // No ordinary-window setting is consulted on first use.
            var value = SettingsToolkit.ReadLocalSetting(SettingNames.CompactOverlayPosition, string.Empty);
            return PlacementPoint.ParseSetting(value);
        }

        public void WritePosition(PlacementPoint point)
            => SettingsToolkit.WriteLocalSetting(SettingNames.CompactOverlayPosition,
                point.ToSetting());

        public PlacementBounds GetWorkArea(PlacementPoint point)
        {
            var area = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Nearest)
                ?? DisplayArea.Primary;
            var work = area.WorkArea;
            return new(work.X, work.Y, work.Width, work.Height);
        }

        public void Move(PlacementPoint point) => window.Move(new PointInt32(point.X, point.Y));
    }
}
