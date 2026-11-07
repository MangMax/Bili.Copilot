// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow
{
    private CompactOverlayRatioSession _ratioSession;
    private bool _fittingRatioImmediately;

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
        if (IsClosed || IsDisposed || _rootWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay
            || (IsPresenterChanging && !_fittingRatioImmediately))
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

    private async Task ChangeRatioPresenterAsync(AppWindowPresenterKind kind)
    {
        var mode = kind switch
        {
            AppWindowPresenterKind.CompactOverlay => WindowPlacementMode.CompactOverlay,
            AppWindowPresenterKind.FullScreen => WindowPlacementMode.FullScreen,
            _ => WindowPlacementMode.Ordinary,
        };

        await _ratioSession.ChangePresenterAsync(
            () => _presenterChange.ChangeAsync(mode, () => _rootWindow.SetPresenter(kind), () =>
            {
                if (kind == AppWindowPresenterKind.CompactOverlay)
                {
                    FitRatioImmediately();
                }
                else if (kind == AppWindowPresenterKind.Overlapped)
                {
                    // 回到普通窗口时立刻回到记忆的普通窗口几何，而不是留在小窗的大小和位置。
                    RestoreOrdinaryGeometry();
                }
            }),
            () => !IsClosed && !IsDisposed && !IsPresenterChanging && _rootWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay);

        if (kind == AppWindowPresenterKind.Overlapped)
        {
            // The presenter reports its kind a frame later on some systems; restore once settled.
            MoveAndResize();
        }
    }

    private void FitRatioImmediately()
    {
        if (IsClosed || IsDisposed || _rootWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay)
        {
            return;
        }

        // The presenter is still settling in this batch, so bypass the settle guard for this
        // single fit: a portrait window must not show the presenter's landscape default first.
        _fittingRatioImmediately = true;
        try
        {
            _ratioSession.ApplyNow();
        }
        finally
        {
            _fittingRatioImmediately = false;
        }
    }
}
