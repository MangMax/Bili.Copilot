// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow
{
    private CompactOverlayRatioSession _ratioSession;

    internal long BeginRatioMedia() => _ratioSession.BeginMedia();

    internal void UpdateRatioMedia(long generation, double width, double height)
        => _ratioSession.UpdateMedia(generation, width, height);

    private void InitializeRatio()
        => _ratioSession = new(SettingsToolkit.CompactOverlayRatio,
            () => new(_rootWindow.Position.X, _rootWindow.Position.Y, _rootWindow.ClientSize.Width, _rootWindow.ClientSize.Height),
            () =>
            {
                var area = DisplayArea.GetFromWindowId(_rootWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
                return new(area.X, area.Y,
                    area.Width - (_rootWindow.Size.Width - _rootWindow.ClientSize.Width),
                    area.Height - (_rootWindow.Size.Height - _rootWindow.ClientSize.Height));
            }, ApplyRatioBounds);

    private void ApplyRatioBounds(RatioWindowBounds bounds)
    {
        if (IsClosed || IsDisposed || IsPresenterChanging || _rootWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay)
        {
            return;
        }

        // Ratio describes the video/client area; add the actual non-client frame in physical pixels.
        var width = bounds.Width + _rootWindow.Size.Width - _rootWindow.ClientSize.Width;
        var height = bounds.Height + _rootWindow.Size.Height - _rootWindow.ClientSize.Height;
        _rootWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, width, height));

        // The presenter may impose minimum/maximum sizes. Accept its size, only recover visibility.
        var area = DisplayArea.GetFromWindowId(_rootWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var position = _rootWindow.Position;
        var size = _rootWindow.Size;
        var x = CompactOverlayPlacement.RecoverAxis(position.X, size.Width, area.X, area.Width);
        var y = CompactOverlayPlacement.RecoverAxis(position.Y, size.Height, area.Y, area.Height);
        if (x != position.X || y != position.Y)
        {
            _rootWindow.Move(new PointInt32(x, y));
        }
    }

    private Task ChangeRatioPresenterAsync(AppWindowPresenterKind kind)
        => _ratioSession.ChangePresenterAsync(
            () => _presenterChange.ChangeAsync(kind switch
            {
                AppWindowPresenterKind.CompactOverlay => WindowPlacementMode.CompactOverlay,
                AppWindowPresenterKind.FullScreen => WindowPlacementMode.FullScreen,
                _ => WindowPlacementMode.Ordinary,
            }, () => _rootWindow.SetPresenter(kind)),
            () => !IsClosed && !IsDisposed && !IsPresenterChanging && _rootWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay);
}
