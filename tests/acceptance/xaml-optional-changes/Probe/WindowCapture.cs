// Copyright (c) Bili Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace BiliCopilot.Acceptance.XamlOptionalChanges;

/// <summary>
/// Win32 窗口定位与 PrintWindow 截图。输出 24 位 BMP（无 alpha，避免 GDI+ 读回时整块透明），
/// 由 Measure.ps1 转成 PNG 后做像素对比。
/// </summary>
internal static class WindowCapture
{
    private const uint PwRenderFullContent = 2;

    public readonly record struct Bounds(int Left, int Top, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public int Colors;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr targetDc, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr dc,
        IntPtr bitmap,
        uint start,
        uint lines,
        byte[] bits,
        ref BitmapInfo info,
        uint usage);

    /// <summary>
    /// 进程内所有可见的顶层窗口，按面积从大到小排列（弹出菜单通常排第二）。
    /// </summary>
    public static IReadOnlyList<IntPtr> GetVisibleWindows(uint processId, IntPtr exclude)
    {
        var windows = new List<IntPtr>();
        EnumWindows(
            (hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out var owner);
                if (owner == processId && hWnd != exclude && IsWindowVisible(hWnd))
                {
                    windows.Add(hWnd);
                }

                return true;
            },
            IntPtr.Zero);
        windows.Sort((a, b) => Area(b).CompareTo(Area(a)));
        return windows;
    }

    private static long Area(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out var rect))
        {
            return 0;
        }

        return (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
    }

    public static uint GetDpi(IntPtr hWnd) => GetDpiForWindow(hWnd);

    public static bool TryGetBounds(IntPtr hWnd, out Bounds bounds)
    {
        if (!GetWindowRect(hWnd, out var rect))
        {
            bounds = default;
            return false;
        }

        bounds = new Bounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    /// <summary>
    /// PrintWindow(PW_RENDERFULLCONTENT) 抓取窗口并写成 24 位 BMP。
    /// 返回 false 时不会落盘半个文件。
    /// </summary>
    public static bool TryCapture(IntPtr hWnd, string path, out Bounds bounds)
    {
        bounds = default;
        if (!TryGetBounds(hWnd, out bounds))
        {
            return false;
        }

        var width = bounds.Width;
        var height = bounds.Height;
        var windowDc = GetWindowDC(hWnd);
        if (windowDc == IntPtr.Zero)
        {
            return false;
        }

        var memoryDc = CreateCompatibleDC(windowDc);
        var bitmap = CreateCompatibleBitmap(windowDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            if (!PrintWindow(hWnd, memoryDc, PwRenderFullContent))
            {
                return false;
            }

            var stride = (width * 3 + 3) / 4 * 4;
            var pixels = new byte[stride * height];
            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = height,
                    Planes = 1,
                    BitCount = 24,
                    Compression = 0,
                    SizeImage = pixels.Length,
                },
            };

            if (GetDIBits(windowDc, bitmap, 0, (uint)height, pixels, ref info, 0) == 0)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(14 + 40 + pixels.Length);
            writer.Write(0);
            writer.Write(14 + 40);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixels.Length);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            writer.Write(pixels);
            return true;
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(hWnd, windowDc);
        }
    }
}
