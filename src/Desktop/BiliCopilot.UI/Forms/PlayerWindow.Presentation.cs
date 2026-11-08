// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
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
    private sealed class PlayerWindowSurface(PlayerWindow owner, ILogger logger) : IPlayerWindowSurface
    {
        /// <summary>DWM_CLOAKED_APP: the window is cloaked by its owning application.</summary>
        private const uint AppCloakReason = 0x00000001;

        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

        private WindowProtection _protection;
        private bool _restoreForeground;

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

        public WindowProtectionResult SetOccluded(bool occluded)
        {
            if (owner.IsClosed || owner.IsDisposed)
            {
                // Nothing is left to protect or reveal; do not touch a destroyed window.
                _protection = WindowProtection.None;
                return WindowProtectionResult.Ok;
            }

            return occluded ? Protect() : Reveal();
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

        public Task<WindowLayoutResult> WaitForLayoutAsync(WindowLayoutTarget target, TimeSpan budget,
            CancellationToken token)
        {
            var degraded = false;
            return WindowLayoutSettler.WaitAsync(
                target,
                budget,
                () => owner.IsClosed || owner.IsDisposed,
                () => new WindowLayoutSample(PresenterMode, Bounds, IsMaximized),
                TickAsync,
                token);

            async Task TickAsync(TimeSpan _, CancellationToken cancellation)
            {
                // Off the UI thread so a stalled composition boundary cannot deadlock the
                // thread that has to process the next window message. The settle loop owns
                // the budget: it stops observing even if this tick never returns.
                var result = await Task.Run(static () => PInvoke.DwmFlush(), CancellationToken.None);
                if (result.Succeeded)
                {
                    return;
                }

                if (!degraded)
                {
                    degraded = true;
                    logger.LogWarning(
                        "DwmFlush failed ({Result}); the layout is observed without a composition boundary.",
                        result);
                }

                // Pacing only. A failed composition query is never evidence that a frame
                // is ready, so the next observation still has to prove the state.
                await Task.Delay(FrameInterval, cancellation);
            }
        }

        private WindowProtectionResult Protect()
        {
            if (TrySetCloak(true))
            {
                _protection = WindowProtection.Cloaked;
                return WindowProtectionResult.Ok;
            }

            // Cloaking is the intended protection; hiding is the fallback if DWM
            // rejects it. Restore the foreground state on reveal.
            var foreground = PInvoke.GetForegroundWindow() == new HWND(owner.Handle);
            owner._rootWindow.Hide();
            if (owner._rootWindow.IsVisible)
            {
                _protection = WindowProtection.None;
                return WindowProtectionResult.Failed(
                    "neither DWMWA_CLOAK nor Hide kept the player window off screen.");
            }

            _protection = WindowProtection.Hidden;
            _restoreForeground = foreground;
            logger.LogWarning("DWM rejected DWMWA_CLOAK; the player window is hidden for the transition instead.");
            return WindowProtectionResult.Degraded("DWM rejected DWMWA_CLOAK; the window was hidden instead.");
        }

        private WindowProtectionResult Reveal()
        {
            // Hide/Show does not clear an application cloak: the documented way is the
            // same DWMWA_CLOAK attribute with a zero value, verified by reading
            // DWMWA_CLOAKED. Never assume Show made the window visible again.
            var cloaked = ReadAppCloak();
            if (cloaked == true || (cloaked is null && _protection == WindowProtection.Cloaked))
            {
                if (!TrySetCloak(false))
                {
                    return WindowProtectionResult.Failed(
                        "DWMWA_CLOAK is still set after clearing it; the player window stays invisible.");
                }
            }

            if (_protection == WindowProtection.Hidden)
            {
                owner._rootWindow.Show(_restoreForeground);
                if (!owner._rootWindow.IsVisible)
                {
                    return WindowProtectionResult.Failed(
                        "the hidden player window did not become visible again.");
                }

                if (_restoreForeground)
                {
                    PInvoke.SetForegroundWindow(new(owner.Handle));
                    PInvoke.SetFocus(new(owner.Handle));
                }
            }

            _protection = WindowProtection.None;
            return WindowProtectionResult.Ok;
        }

        private unsafe bool TrySetCloak(bool cloaked)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var value = cloaked ? 1 : 0;
                var result = PInvoke.DwmSetWindowAttribute(new(owner.Handle), DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                    &value, (uint)sizeof(int));
                if (result.Failed)
                {
                    continue;
                }

                var observed = ReadAppCloak();
                if (observed is null || observed == cloaked)
                {
                    return true;
                }

                // Reissue the attribute if DWM still reports the old state. Do not wait
                // for composition on the UI thread: DwmFlush has no timeout and would
                // bypass the transition budget. Layout confirmation owns frame waits.
            }

            return false;
        }

        private unsafe bool? ReadAppCloak()
        {
            var value = 0u;
            var result = PInvoke.DwmGetWindowAttribute(new(owner.Handle), DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
                &value, (uint)sizeof(uint));
            return result.Failed ? null : (value & AppCloakReason) != 0;
        }

        private enum WindowProtection
        {
            None,
            Cloaked,
            Hidden,
        }
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
