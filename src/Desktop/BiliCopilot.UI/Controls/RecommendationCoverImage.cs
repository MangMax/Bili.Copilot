// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Richasy.WinUIKernel.Share.Base;
using Windows.Graphics.Imaging;

namespace BiliCopilot.UI.Controls;

/// <summary>Decodes recommendation covers at their displayed physical size.</summary>
public sealed partial class RecommendationCoverImage : UserControl
{
    // Reuse the existing disk-cache naming, expiry and clear-cache contract.
    // This helper never enters the visual tree or starts a Win2D image load.
    private static readonly BasicCoverImage Cache = new();
    private static readonly SemaphoreSlim Downloads = new(8);
    // Cached/off-screen realized covers share this budget too. A resize must not
    // enqueue a native decode and a UI surface upload for every cached card at once.
    private static readonly SemaphoreSlim Decodes = new(2);
    private static readonly Dictionary<string, Task<byte[]>> Pending = new();
    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private readonly Shimmer _loading = new();
    private readonly Grid _root = new();
    private CancellationTokenSource? _request;
    private XamlRoot? _observedRoot;
    private CanvasDevice? _device;
    private long _lastRecovery;
    private CancellationTokenSource? _recoveryDelay;
    private long? _cornerRadiusToken;
    private (Uri? Source, int Width, int Height) _requested;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _resizeTimer;
    private (int Width, int Height) _pendingSize;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(Uri), typeof(RecommendationCoverImage), new PropertyMetadata(null, OnSourceChanged));

    public Uri? Source
    {
        get => (Uri?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public RecommendationCoverImage()
    {
        _root.Children.Add(_image);
        _root.Children.Add(_loading);
        Content = _root;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var cover = (RecommendationCoverImage)sender;
        cover._resizeTimer?.Stop();
        cover.Reload();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_cornerRadiusToken is null)
        {
            // Track later changes too: a one-off copy in Loaded goes stale when a
            // template or style updates the corner radius after first layout.
            _cornerRadiusToken = RegisterPropertyChangedCallback(
                CornerRadiusProperty, OnCornerRadiusChanged);
        }

        _root.CornerRadius = CornerRadius;
        CompositionTarget.SurfaceContentsLost += OnSurfaceContentsLost;
        _observedRoot = XamlRoot;
        if (_observedRoot is not null)
        {
            _observedRoot.Changed += OnRootChanged;
        }

        Reload();
    }

    private void OnCornerRadiusChanged(DependencyObject sender, DependencyProperty property)
        => _root.CornerRadius = CornerRadius;

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _resizeTimer?.Stop();
        _pendingSize = default;
        if (_cornerRadiusToken is not null)
        {
            UnregisterPropertyChangedCallback(CornerRadiusProperty, _cornerRadiusToken.Value);
            _cornerRadiusToken = null;
        }

        CompositionTarget.SurfaceContentsLost -= OnSurfaceContentsLost;
        if (_device is not null)
        {
            _device.DeviceLost -= OnDeviceLost;
            _device = null;
        }

        if (_observedRoot is not null)
        {
            _observedRoot.Changed -= OnRootChanged;
            _observedRoot = null;
        }

        _request?.Cancel();
        _request = null;
        _recoveryDelay?.Cancel();
        _recoveryDelay = null;
        _requested = default;
        _image.Source = null;
        _loading.IsActive = false;
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ScheduleResize();

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => ScheduleResize();

    private void ScheduleResize()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0 || XamlRoot is null)
        {
            return;
        }

        var size = ((int)Math.Ceiling(ActualWidth * XamlRoot.RasterizationScale),
            (int)Math.Ceiling(ActualHeight * XamlRoot.RasterizationScale));
        if (size == (_requested.Width, _requested.Height))
        {
            _resizeTimer?.Stop();
            return;
        }

        // XamlRoot.Changed also fires without a change in physical image size.
        // Do not postpone the pending decode for those notifications.
        if (_resizeTimer?.IsRunning == true && _pendingSize == size)
        {
            return;
        }

        _pendingSize = size;
        if (_resizeTimer is null)
        {
            _resizeTimer = DispatcherQueue.CreateTimer();
            _resizeTimer.IsRepeating = false;
            _resizeTimer.Interval = TimeSpan.FromMilliseconds(200);
            _resizeTimer.Tick += (_, _) => Reload();
        }

        // Keep the current surface while layout stretches it. Starting WIC on
        // every pixel of a resize creates work that cannot safely be cancelled.
        // Decode at the final physical size after the resize settles, including
        // DPI-only changes, without delaying layout or source changes.
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void OnSurfaceContentsLost(object? sender, object args)
    {
        _requested = default;
        Reload();
    }

    private void OnDeviceLost(CanvasDevice sender, object args)
    {
        // Win2D can notify from another thread. Ignore queued callbacks after
        // recycling and never dispose a device shared with other controls.
        DispatcherQueue.TryEnqueue(() => _ = RecoverDeviceAsync(sender));
    }

    private async Task RecoverDeviceAsync(CanvasDevice sender)
    {
        if (_recoveryDelay is not null || !IsLoaded || !ReferenceEquals(sender, _device))
        {
            return;
        }

        using var recovery = new CancellationTokenSource();
        _recoveryDelay = recovery;
        try
        {
            // Throttle consecutive losses without dropping the recovery request.
            var elapsed = Environment.TickCount64 - _lastRecovery;
            if (_lastRecovery != 0 && elapsed < 3000)
            {
                await Task.Delay((int)(3000 - elapsed), recovery.Token);
            }

            if (recovery.IsCancellationRequested || !IsLoaded || !ReferenceEquals(sender, _device))
            {
                return;
            }

            _lastRecovery = Environment.TickCount64;
            _requested = default;
            Reload();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.Get<ILogger<RecommendationCoverImage>>().LogWarning(ex, "Unable to recover recommendation cover device");
        }
        finally
        {
            if (ReferenceEquals(_recoveryDelay, recovery))
            {
                _recoveryDelay = null;
            }
        }
    }

    private async void Reload()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var scale = XamlRoot.RasterizationScale;
        var key = (Source, (int)Math.Ceiling(ActualWidth * scale), (int)Math.Ceiling(ActualHeight * scale));
        if (_requested == key)
        {
            return;
        }

        var sourceChanged = _requested.Source != Source;
        _requested = key;
        _request?.Cancel();
        using var request = new CancellationTokenSource();
        _request = request;
        var token = request.Token;
        if (sourceChanged)
        {
            _image.Source = null;
        }

        _loading.IsActive = Source is not null && _image.Source is null;
        _loading.Visibility = _loading.IsActive ? Visibility.Visible : Visibility.Collapsed;
        try
        {
            if (key.Source is null)
            {
                return;
            }

            using var stream = await OpenAsync(key.Source, token);
            using var random = stream.AsRandomAccessStream();
            await Decodes.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                // Canceling the WinRT wrapper can complete before WIC stops using the
                // adapted stream (native AV on rapid unload/remount). Keep the stream
                // alive until native decoding completes, then reject stale results.
                var decoder = await BitmapDecoder.CreateAsync(random).AsTask();
                token.ThrowIfCancellationRequested();
                // Never enlarge the source; UniformToFill needs enough pixels on BOTH axes.
                var (outputWidth, outputHeight) = CoverDecodeSize.Calculate(
                    decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, key.Item2, key.Item3, 1);
                var width = decoder.PixelWidth == decoder.OrientedPixelWidth ? outputWidth : outputHeight;
                var height = decoder.PixelHeight == decoder.OrientedPixelHeight ? outputHeight : outputWidth;
                ImageSource result;
                var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth = width, ScaledHeight = height, InterpolationMode = BitmapInterpolationMode.Fant },
                    ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask();
                token.ThrowIfCancellationRequested();
                var device = CanvasDevice.GetSharedDevice();
                if (!ReferenceEquals(_device, device))
                {
                    if (_device is not null)
                    {
                        _device.DeviceLost -= OnDeviceLost;
                    }

                    _device = device;
                    device.DeviceLost += OnDeviceLost;
                }

                using var bitmap = CanvasBitmap.CreateFromBytes(device, pixels.DetachPixelData(),
                    (int)outputWidth, (int)outputHeight, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                var surface = new CanvasImageSource(device, outputWidth, outputHeight, 96);
                using (var session = surface.CreateDrawingSession(Colors.Transparent))
                {
                    session.DrawImage(bitmap);
                }

                result = surface;
                token.ThrowIfCancellationRequested();
                if (ReferenceEquals(_request, request) && IsLoaded)
                {
                    _image.Source = result;
                }
            }
            finally
            {
                Decodes.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (_device?.IsDeviceLost(ex.HResult) == true)
        {
            _device.RaiseDeviceLost();
        }
        catch (Exception ex)
        {
            this.Get<ILogger<RecommendationCoverImage>>().LogWarning(ex, "Unable to load recommendation cover");
            // As with an ordinary cover without HolderImage: end loading and
            // leave the cover empty. A later source/reload may retry.
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
                _loading.IsActive = false;
                _loading.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static async Task<Stream> OpenAsync(Uri uri, CancellationToken token)
    {
        if (uri.IsFile)
        {
            return File.OpenRead(uri.LocalPath);
        }

        var cached = await Cache.GetCacheFilePathAsync(uri.ToString());
        token.ThrowIfCancellationRequested();
        if (cached is not null)
        {
            return File.OpenRead(cached);
        }

        Task<byte[]> pending;
        lock (Pending)
        {
            if (!Pending.TryGetValue(uri.ToString(), out pending!))
            {
                var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending = completion.Task;
                Pending.Add(uri.ToString(), pending);
                _ = DownloadAsync(uri, completion);
            }
        }

        return new MemoryStream(await pending.WaitAsync(token), writable: false);
    }

    private static async Task DownloadAsync(Uri uri, TaskCompletionSource<byte[]> completion)
    {
        try
        {
            await Downloads.WaitAsync();
            try
            {
                var bytes = await InternalHttpExtensions.ImageClient.GetByteArrayAsync(uri);
                // A recycled consumer must not cancel another consumer or leave
                // a partially written cache entry behind.
                await Cache.WriteCacheAsync(uri.ToString(), bytes);
                completion.TrySetResult(bytes);
            }
            finally
            {
                Downloads.Release();
            }
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
        finally
        {
            lock (Pending)
            {
                Pending.Remove(uri.ToString());
            }
        }
    }
}
