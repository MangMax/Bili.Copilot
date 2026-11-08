// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32.Graphics.Dwm;
using WinUIEx;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow
{
    /// <summary>
    /// The only place that touches player-window presentation, geometry and the DWM
    /// composition boundary. The coordinator above it never calls a Win32/WinRT API.
    /// </summary>
    private sealed class PlayerWindowSurface(PlayerWindow owner) : IPlayerWindowSurface
    {
        private bool _cloaked;
        private bool _fallbackHidden;
        private bool _fallbackForeground;

        public WindowRect Bounds
        {
            get
            {
                var position = owner._rootWindow.Position;
                var size = owner._rootWindow.Size;
                return new WindowRect(position.X, position.Y, size.Width, size.Height);
            }
        }

        public WindowRect ClientBounds
        {
            get
            {
                var position = owner._rootWindow.Position;
                var size = owner._rootWindow.ClientSize;
                return new WindowRect(position.X, position.Y, size.Width, size.Height);
            }
        }

        public bool IsVisible => owner._rootWindow.IsVisible;

        public bool IsMaximized => PInvoke.IsZoomed(new(owner.Handle));

        public WindowPresentationMode PresenterMode => owner._rootWindow.Presenter.Kind switch
        {
            AppWindowPresenterKind.CompactOverlay => WindowPresentationMode.CompactOverlay,
            AppWindowPresenterKind.FullScreen => WindowPresentationMode.FullScreen,
            _ => WindowPresentationMode.Ordinary,
        };

        public WindowRect GetWorkArea(WindowPoint point)
        {
            var display = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Nearest)
                ?? DisplayArea.Primary;
            var area = display.WorkArea;
            return new WindowRect(area.X, area.Y, area.Width, area.Height);
        }

        public void SetPresenter(WindowPresentationMode mode)
            => owner._rootWindow.SetPresenter(mode switch
            {
                WindowPresentationMode.CompactOverlay => AppWindowPresenterKind.CompactOverlay,
                WindowPresentationMode.FullScreen => AppWindowPresenterKind.FullScreen,
                _ => AppWindowPresenterKind.Overlapped,
            });

        public void ApplyBounds(WindowRect bounds)
            => owner._rootWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        public void SetMaximized(bool maximized)
        {
            if (owner._rootWindow.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            if (maximized)
            {
                presenter.Maximize();
            }
            else
            {
                presenter.Restore();
            }
        }

        public unsafe void SetOccluded(bool occluded)
        {
            if (owner.IsClosed || owner.IsDisposed)
            {
                return;
            }

            if (occluded)
            {
                var value = 1;
                var result = PInvoke.DwmSetWindowAttribute(new(owner.Handle), DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                    &value, (uint)sizeof(int));
                if (result.Succeeded)
                {
                    _cloaked = true;
                    return;
                }

                // Cloaking is the intended protection; hiding is the fallback if DWM
                // rejects it. Restore the foreground state on reveal.
                _fallbackHidden = true;
                _fallbackForeground = PInvoke.GetForegroundWindow() == new HWND(owner.Handle);
                owner._rootWindow.Hide();
                return;
            }

            if (_cloaked)
            {
                var value = 0;
                _ = PInvoke.DwmSetWindowAttribute(new(owner.Handle), DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                    &value, (uint)sizeof(int));
                _cloaked = false;
            }
            else if (_fallbackHidden)
            {
                _fallbackHidden = false;
                owner._rootWindow.Show(_fallbackForeground);
                if (_fallbackForeground)
                {
                    PInvoke.SetForegroundWindow(new(owner.Handle));
                    PInvoke.SetFocus(new(owner.Handle));
                }
            }
        }

        public void Show(bool activate)
        {
            if (owner.IsClosed || owner.IsDisposed)
            {
                return;
            }

            owner._rootWindow.Show(activate);
            if (activate)
            {
                PInvoke.SetForegroundWindow(new(owner.Handle));
                PInvoke.SetFocus(new(owner.Handle));
            }
        }

        public async Task<WindowRect> WaitForPresenterAsync(WindowPresentationMode mode, CancellationToken token)
        {
            var last = Bounds;
            var stable = 0;
            for (var frame = 0; frame < 8; frame++)
            {
                if (owner.IsClosed || owner.IsDisposed || token.IsCancellationRequested)
                {
                    return Bounds;
                }

                await WaitForCompositionAsync(token);
                var current = Bounds;
                if (PresenterMode == mode && current == last)
                {
                    if (++stable >= 2)
                    {
                        return current;
                    }
                }
                else
                {
                    stable = 0;
                }

                last = current;
            }

            return Bounds;
        }

        public async Task<WindowRect> WaitForBoundsAsync(WindowRect target, WindowPresentationMode mode,
            CancellationToken token)
        {
            var stable = 0;
            for (var frame = 0; frame < 10; frame++)
            {
                if (owner.IsClosed || owner.IsDisposed || token.IsCancellationRequested)
                {
                    return Bounds;
                }

                await WaitForCompositionAsync(token);
                var current = Bounds;
                if (PresenterMode == mode && CloseEnough(current, target))
                {
                    if (++stable >= 2)
                    {
                        return current;
                    }
                }
                else
                {
                    stable = 0;
                }
            }

            return Bounds;
        }

        private static Task WaitForCompositionAsync(CancellationToken token)
            => Task.Run(() =>
            {
                // A composition frame confirms the window manager has accepted the
                // presenter/geometry change. This is a frame boundary, not a 500 ms guess.
                if (PInvoke.DwmFlush().Failed)
                {
                    Thread.Sleep(16);
                }
            }, token);

        private static bool CloseEnough(WindowRect left, WindowRect right)
            => Math.Abs(left.X - right.X) <= 1
                && Math.Abs(left.Y - right.Y) <= 1
                && Math.Abs(left.Width - right.Width) <= 1
                && Math.Abs(left.Height - right.Height) <= 1;
    }

    /// <summary>Persisted ordinary geometry and compact origin for one player window.</summary>
    private sealed class PlayerWindowState(PlayerWindow owner) : IPlayerWindowStateStore
    {
        public WindowRect? ReadOrdinaryBounds()
        {
            var left = SettingsToolkit.ReadLocalSetting(SettingNames.PlayerWindowPositionLeft, 0);
            var top = SettingsToolkit.ReadLocalSetting(SettingNames.PlayerWindowPositionTop, 0);
            var scale = Scale;
            var width = Convert.ToInt32(SettingsToolkit.ReadLocalSetting(SettingNames.PlayerWindowWidth, 1120d) * scale);
            var height = Convert.ToInt32(SettingsToolkit.ReadLocalSetting(SettingNames.PlayerWindowHeight, 740d) * scale);
            var display = DisplayArea.GetFromPoint(new PointInt32(left, top), DisplayAreaFallback.Primary)
                ?? DisplayArea.Primary;
            var area = display.WorkArea;
            height = Math.Min(height, Math.Max(WindowMinHeight, area.Height - 20));
            var isZeroPoint = left == 0 && top == 0;
            var isValidPosition = left >= area.X && top >= area.Y
                && left + width <= area.X + area.Width
                && top + height <= area.Y + area.Height;
            var x = isZeroPoint || !isValidPosition
                ? area.X + ((area.Width - width) / 2)
                : left;
            var y = isZeroPoint || !isValidPosition
                ? area.Y + ((area.Height - height) / 2)
                : top;
            return new WindowRect(x, y, width, height);
        }

        public bool ReadOrdinaryMaximized()
            => SettingsToolkit.ReadLocalSetting(SettingNames.IsPlayerWindowMaximized, false);

        public void WriteOrdinaryBounds(WindowRect bounds, bool maximized)
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.IsPlayerWindowMaximized, maximized);
            if (maximized)
            {
                return;
            }

            SettingsToolkit.WriteLocalSetting(SettingNames.PlayerWindowPositionLeft, bounds.X);
            SettingsToolkit.WriteLocalSetting(SettingNames.PlayerWindowPositionTop, bounds.Y);
            var scale = Scale;
            var width = bounds.Width / scale;
            var height = bounds.Height / scale;
            if (height >= WindowMinHeight && width >= WindowMinWidth)
            {
                SettingsToolkit.WriteLocalSetting(SettingNames.PlayerWindowHeight, height);
                SettingsToolkit.WriteLocalSetting(SettingNames.PlayerWindowWidth, width);
            }
        }

        public WindowPoint? ReadCompactPosition()
        {
            // One scalar setting keeps the coordinate pair atomic and permits (0, 0).
            // No ordinary-window setting is consulted on first use.
            var value = SettingsToolkit.ReadLocalSetting(SettingNames.CompactOverlayPosition, string.Empty);
            return WindowPoint.ParseSetting(value);
        }

        public void WriteCompactPosition(WindowPoint point)
            => SettingsToolkit.WriteLocalSetting(SettingNames.CompactOverlayPosition, point.ToSetting());

        private double Scale
            => owner.XamlRoot?.RasterizationScale
                ?? HwndExtensions.GetDpiForWindow(owner.Handle) / 96d;
    }
}
