// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

internal readonly record struct PlacementPoint(int X, int Y)
{
    public string ToSetting() => FormattableString.Invariant($"{X},{Y}");

    public static PlacementPoint? ParseSetting(string value)
    {
        var coordinates = value.Split(',');
        return coordinates.Length == 2
            && int.TryParse(coordinates[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var x)
            && int.TryParse(coordinates[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var y)
            ? new PlacementPoint(x, y)
            : null;
    }
}

internal readonly record struct PlacementBounds(int X, int Y, int Width, int Height);

internal enum WindowPlacementMode
{
    Ordinary,
    CompactOverlay,
    FullScreen,
}

/// <summary>
/// Window and persisted position boundary. All coordinates and sizes are physical pixels.
/// </summary>
internal interface IPlayerWindowPlacement
{
    PlacementBounds Bounds { get; }

    PlacementPoint? ReadPosition();

    void WritePosition(PlacementPoint point);

    PlacementBounds GetWorkArea(PlacementPoint point);

    void Move(PlacementPoint point);
}

/// <summary>
/// Coordinates stable compact placement with presenter changes on the window's UI context.
/// </summary>
internal sealed class PlayerWindowPlacementSession(IPlayerWindowPlacement window, Func<Task> delay)
{
    private readonly PresenterChangeSession _presenter = new(delay);
    private WindowPlacementMode _mode;
    private long _generation;
    private bool _closed;
    private bool _restoring;

    public bool IsChanging => _presenter.IsChanging || _restoring;

    public bool CanSaveOrdinaryWindow => !_closed && !IsChanging && _mode == WindowPlacementMode.Ordinary;

    public async Task ChangeAsync(WindowPlacementMode mode, Action applyPresenter)
    {
        if (_closed)
        {
            return;
        }

        GeometryChanged();
        var generation = ++_generation;
        _restoring = true;
        try
        {
            await _presenter.ChangeAsync(() =>
            {
                // Reposition in the same synchronous batch as the presenter switch: the
                // compact window must never show up at the ordinary window's location
                // first and then jump to the remembered one.
                if (mode == WindowPlacementMode.CompactOverlay && window.ReadPosition() is { } hint)
                {
                    var hintArea = window.GetWorkArea(hint);
                    window.Move(Clamp(hint, window.Bounds, hintArea));
                }

                applyPresenter();
            });

            if (_closed || generation != _generation)
            {
                return;
            }

            if (mode == WindowPlacementMode.CompactOverlay && window.ReadPosition() is { } position)
            {
                var area = window.GetWorkArea(position);
                window.Move(Clamp(position, window.Bounds, area));
                if (_closed || generation != _generation)
                {
                    return;
                }

                // Crossing monitors can synchronously change the OS-constrained pixel
                // size. Recheck the destination size, without resizing the window.
                var bounds = window.Bounds;
                var current = new PlacementPoint(bounds.X, bounds.Y);
                var corrected = Clamp(current, bounds, area);
                if (corrected != current)
                {
                    window.Move(corrected);
                }
            }

            if (!_closed && generation == _generation)
            {
                _mode = mode;
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    public void GeometryChanged()
    {
        if (!_closed && !IsChanging && _mode == WindowPlacementMode.CompactOverlay)
        {
            var bounds = window.Bounds;
            window.WritePosition(new(bounds.X, bounds.Y));
        }
    }

    public void Close()
    {
        GeometryChanged();
        _closed = true;
        _generation++;
        _presenter.Close();
    }

    private static PlacementPoint Clamp(PlacementPoint position, PlacementBounds bounds, PlacementBounds area)
    {
        // Both APIs use physical pixels; never multiply a work area by DPI again.
        // A compact window may hang over an edge by design, so the remembered position
        // survives unless the whole window is hidden (removed monitor, stale coordinates).
        return new(
            CompactOverlayPlacement.RecoverAxis(position.X, bounds.Width, area.X, area.Width),
            CompactOverlayPlacement.RecoverAxis(position.Y, bounds.Height, area.Y, area.Height));
    }
}
