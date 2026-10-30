using BiliCopilot.UI.Controls.Markdown;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices;

namespace Acceptance;

// Fixture only. No historical diagnostic dictionaries or Image-retaining logs are installed.
public partial class App : Application
{
    Window window;
    StackPanel host;
    TextBlock status;
    Http52 server;
    readonly List<object> records = new(); // scalar snapshots only
    readonly string output = Path.Combine(AppContext.BaseDirectory, "experiment");
    int step;
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    public App() {
        Directory.CreateDirectory(output);
        UnhandledException += (_, e) => File.AppendAllText(Path.Combine(output, "unhandled.txt"), e.Exception + "\n" + e.Message);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => File.AppendAllText(Path.Combine(output, "unhandled-domain.txt"), e.ExceptionObject.ToString());
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Directory.CreateDirectory(output);
        server = new Http52();
        window = new Window { Title = "52 SVG and resource experiment 741" };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 900));
        window.AppWindow.Move(new Windows.Graphics.PointInt32(40, 40));
        var root = new Grid { Padding = new Thickness(16), RowSpacing = 8 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var next = new Button { Content = "Next experiment" }; AutomationProperties.SetAutomationId(next, "Next");
        next.Click += async (_, _) => {
            next.IsEnabled = false;
            try {
                status.Text = "Running";
                if (step == 0) await Svg();
                else if (step == 1) await Portrait();
                else if (step == 2) await MoveMonitor(false);
                else if (step == 3) await MoveMonitor(true);
                else if (step == 4) await MeasureResources();
                else throw new InvalidOperationException("All experiments already run");
                step++;
                Save(); status.Text = $"Ready {step}";
            } catch (Exception e) { records.Add(new { failure = e.ToString() }); Save(); status.Text = "FAIL " + e.Message; }
            finally { next.IsEnabled = true; }
        };
        var close = new Button { Content = "Close" }; AutomationProperties.SetAutomationId(close, "Close");
        close.Click += (_, _) => window.Close();
        toolbar.Children.Add(next); toolbar.Children.Add(close);
        status = new TextBlock { Text = "Idle", TextWrapping = TextWrapping.Wrap }; AutomationProperties.SetAutomationId(status, "Status");
        host = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        var scroll = new ScrollViewer { Content = host, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(toolbar); root.Children.Add(status); root.Children.Add(scroll);
        Grid.SetRow(status, 1); Grid.SetRow(scroll, 2);
        window.Content = root; window.Closed += (_, _) => server.Dispose(); window.Activate();
        File.WriteAllBytes(Path.Combine(output, "portrait.bmp"), server.Bytes("portrait"));
        File.WriteAllText(Path.Combine(output, "fixtures.json"), JsonSerializer.Serialize(server.Manifest));
    }
    void Save() => File.WriteAllText(Path.Combine(output, "records.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    async Task Flush()
    {
        var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => t.TrySetResult());
        await t.Task.WaitAsync(TimeSpan.FromSeconds(5)); host.UpdateLayout();
    }
    async Task Frames()
    {
        var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int n = 0;
        EventHandler<object> handler = (_, _) => { if (++n >= 2) t.TrySetResult(); };
        CompositionTarget.Rendering += handler;
        try { await t.Task.WaitAsync(TimeSpan.FromSeconds(8)); }
        finally { CompositionTarget.Rendering -= handler; }
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var c = VisualTreeHelper.GetChild(parent, i); yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }
    async Task Ready(FrameworkElement element, int count)
    {
        bool IsReady() {
            var images = (element is Image im ? new[] { im } : Descendants(element).OfType<Image>()).ToArray();
            return element.IsLoaded && images.Length == count && images.All(i => i.IsLoaded && i.ActualWidth > 0 && i.ActualHeight > 0 &&
                (i.Source is SvgImageSource || i.Source is BitmapImage { PixelWidth: > 0, PixelHeight: > 0 }));
        }
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(15)) {
            var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Check() { if (IsReady()) t.TrySetResult(); }
            EventHandler<object> handler = (_, _) => Check(); host.LayoutUpdated += handler;
            try { await Flush(); Check(); await t.Task.WaitAsync(TimeSpan.FromSeconds(15) - clock.Elapsed); }
            finally { host.LayoutUpdated -= handler; }
            await Frames();
            if (IsReady()) return;
            // A ready Source can be replaced during subsequent inline layout. Preserve this observation.
            records.Add(new { experiment = "readiness-recheck", elapsedMs = clock.ElapsedMilliseconds, images = Descendants(element).OfType<Image>().Select(i => new { source = i.Source?.GetType().Name, i.IsLoaded }).ToArray() });
        }
        throw new TimeoutException("Image source/layout did not remain ready across presentation frames");
    }
    object ImageState(Image i)
    {
        var b = i.Source as BitmapImage; var p = i.TransformToVisual(host).TransformPoint(new Windows.Foundation.Point());
        return new { source = i.Source?.GetType().FullName, width = i.ActualWidth, height = i.ActualHeight, x = p.X, y = p.Y,
            scale = i.XamlRoot.RasterizationScale, sourceWidth = b?.PixelWidth, sourceHeight = b?.PixelHeight,
            requestWidth = b?.DecodePixelWidth, requestHeight = b?.DecodePixelHeight };
    }
    MarkdownTextBlock Document(string path, double width, MarkdownConfig config = null) => new() {
        Width = width, HorizontalAlignment = HorizontalAlignment.Left, Config = config ?? new MarkdownConfig(), Text = $"![fixture]({server.Url}{path})"
    };
    async Task Clear()
    {
        host.Children.Clear(); await Flush(); await Flush(); await Frames();
        if (host.Children.Count != 0) throw new Exception("Host not empty");
    }
    sealed class RecordingRenderer(string directory, string name) : ISVGRenderer
    {
        public string Input;
        public async Task<Image> SvgToImage(string text)
        {
            Input = text;
            File.WriteAllText(Path.Combine(directory, name + ".txt"), text, new UTF8Encoding(false));
            // Exact StreamWriter encoding used by the unmodified DefaultSVGRenderer, no BOM.
            File.WriteAllBytes(Path.Combine(directory, name + ".utf8.bin"), Encoding.UTF8.GetBytes(text));
            return await new DefaultSVGRenderer().SvgToImage(text);
        }
    }
    async Task Svg()
    {
        await Clear();
        foreach (var kind in new[] { "path", "text" }) {
            string body = kind == "path" ? "<path d=\"M120 5 L142 35 L195 35 L161 58 L177 94 L120 73 L63 94 L79 58 L45 35 L98 35 Z\" fill=\"#008080\"/>" : "<text x=\"20\" y=\"65\" font-family=\"Arial\" font-size=\"48\" fill=\"#008080\">café</text>";
            string text = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"240\" height=\"100\" viewBox=\"0 0 240 100\">" + body + "</svg>";
            var bytes = Encoding.Latin1.GetBytes(text); server.SetSvg(bytes);
            File.WriteAllBytes(Path.Combine(output, kind + ".http-latin1.bin"), bytes);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 }; host.Children.Add(row);
            foreach (var route in new[] { "MyImage", "DefaultSVGRenderer", "SvgImageSource" }) {
                var panel = new StackPanel { Spacing = 8 }; row.Children.Add(panel);
                panel.Children.Add(new TextBlock { Text = kind + " / " + route });
                var box = new Border { Width = 242, Height = 102, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray), Background = new SolidColorBrush(Microsoft.UI.Colors.White) }; panel.Children.Add(box);
                var name = kind + "-" + route;
                if (route == "MyImage") {
                    var renderer = new RecordingRenderer(output, name);
                    var doc = Document("svg?" + kind, 240, new MarkdownConfig { SVGRenderer = renderer }); box.Child = doc;
                    await Ready(doc, 1);
                    if (renderer.Input != text) throw new Exception("Production charset changed input");
                    records.Add(new { experiment = name, inputExact = true, image = ImageState(Descendants(doc).OfType<Image>().Single()) });
                } else {
                    // Same semantic string as MyImage receives; bypass both HTTP and loader.
                    File.WriteAllText(Path.Combine(output, name + ".txt"), text, new UTF8Encoding(false));
                    File.WriteAllBytes(Path.Combine(output, name + ".utf8.bin"), Encoding.UTF8.GetBytes(text));
                    Image image;
                    if (route == "DefaultSVGRenderer") image = await new DefaultSVGRenderer().SvgToImage(text);
                    else {
                        var source = new SvgImageSource(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
                        var result = await source.SetSourceAsync(stream.AsRandomAccessStream());
                        if (result != SvgImageSourceLoadStatus.Success) throw new Exception("Direct SVG status " + result);
                        image = new Image { Source = source, Width = 240, Height = 100 };
                        records.Add(new { experiment = name, setSource = result.ToString() });
                    }
                    box.Child = image; await Ready(image, 1);
                    records.Add(new { experiment = name, inputExact = true, image = ImageState(image) });
                }
            }
        }
        host.Children.Add(new TextBlock { Text = "Text fixture expects café. Empty boxes are not a glyph pass; borders belong to the host." });
        await Frames();
    }
    async Task Portrait()
    {
        await Clear();
        host.Children.Add(new TextBlock { Text = "Production MyImage / portrait 1200×2400 / host width 240 DIP" });
        var doc = Document("portrait", 240); host.Children.Add(doc); await Ready(doc, 1);
        records.Add(new { experiment = "portrait", image = ImageState(Descendants(doc).OfType<Image>().Single()) });
    }
    async Task MoveMonitor(bool returnToRight)
    {
        var monitors = new List<(IntPtr handle, Rect rect)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr dc, ref Rect r, IntPtr data) => { monitors.Add((m, r)); return true; }, IntPtr.Zero);
        if (monitors.Count < 2) { records.Add(new { experiment = "dpi", blocked = "Only one actual monitor" }); return; }
        var target = returnToRight ? monitors.OrderByDescending(m => m.rect.Left).First() : monitors.OrderBy(m => m.rect.Left).First();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var before = GetDpiForWindow(hwnd); var requestsBefore = server.Requests;
        window.AppWindow.Move(new Windows.Graphics.PointInt32(target.rect.Left + 40, target.rect.Top + 40));
        await Flush(); await Frames();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check() {
            var image = Descendants(host).OfType<Image>().SingleOrDefault();
            var scale = host.XamlRoot.RasterizationScale;
            if (MonitorFromWindow(hwnd, 2) == target.handle && image?.Source is BitmapImage b &&
                b.DecodePixelWidth == (int)(240 * scale) && b.DecodePixelHeight == (int)(480 * scale)) done.TrySetResult();
        }
        EventHandler<object> handler = (_, _) => Check(); host.LayoutUpdated += handler;
        try { Check(); await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); } finally { host.LayoutUpdated -= handler; }
        await Frames();
        records.Add(new { experiment = returnToRight ? "dpi-right" : "dpi-left", beforeDpi = before, afterDpi = GetDpiForWindow(hwnd),
            targetMonitor = target.handle.ToInt64(), actualMonitor = MonitorFromWindow(hwnd, 2).ToInt64(), requestsBefore, requestsAfter = server.Requests,
            image = ImageState(Descendants(host).OfType<Image>().Single()) });
    }
    // Full-size fixture traverses the same shared loader and metadata decoder. Only requested decode size differs.
    sealed class FullSizeProvider(double width) : IImageProvider
    {
        public bool ShouldUseThisProvider(string url) => true;
        public async Task<Image> GetImage(string url)
        {
            Image result = null;
            await MarkdownImageLoader.Shared.LoadAsync(new Uri(url), async (stream, type, token) => {
                using var random = stream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(random).AsTask(token);
                var source = new BitmapImage { DecodePixelType = DecodePixelType.Physical, DecodePixelWidth = (int)decoder.OrientedPixelWidth, DecodePixelHeight = (int)decoder.OrientedPixelHeight };
                random.Seek(0); await source.SetSourceAsync(random).AsTask(token);
                result = new Image { Source = source, Width = width, Height = width * decoder.OrientedPixelHeight / decoder.OrientedPixelWidth };
            }, CancellationToken.None);
            return result;
        }
    }
    object Sample(int pair, string mode, string phase, long elapsed)
    {
        if (phase.StartsWith("loaded")) {
            var current = Descendants(host).OfType<Image>().Single();
            if (!current.IsLoaded || current.Source is not BitmapImage { PixelWidth: 1200, PixelHeight: 2400 }) throw new Exception("Loaded sampling boundary lost ready image");
        }
        using var p = Process.GetCurrentProcess(); p.Refresh();
        return new { experiment = "resources", pair, mode, phase, elapsedMs = elapsed, utc = DateTimeOffset.UtcNow,
            privateBytes = p.PrivateMemorySize64, workingSet = p.WorkingSet64, managed = GC.GetTotalMemory(false), allocated = GC.GetTotalAllocatedBytes(false),
            gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
            dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)), scale = host.XamlRoot.RasterizationScale,
            images = Descendants(host).OfType<Image>().Select(ImageState).ToArray() };
    }
    async Task LoadResource(string mode)
    {
        var doc = Document("portrait", 240, mode == "full" ? new MarkdownConfig { ImageProvider = new FullSizeProvider(240) } : null);
        host.Children.Add(doc); await Ready(doc, 1);
        var image = Descendants(doc).OfType<Image>().Single(); var b = (BitmapImage)image.Source;
        if (b.PixelWidth != 1200 || b.PixelHeight != 2400 || Math.Abs(image.ActualWidth - 240) > 1 || Math.Abs(image.ActualHeight - 480) > 1) throw new Exception("Unmatched resource source/layout");
        if (mode == "full" && (b.DecodePixelWidth != 1200 || b.DecodePixelHeight != 2400)) throw new Exception("Full decode request missing");
    }
    async Task MeasureResources()
    {
        await Clear();
        // Symmetric warmups excluded from pairs; no forced GC. Six pairs alternate order to expose drift.
        foreach (var mode in new[] { "limited", "full" }) { await LoadResource(mode); await Clear(); }
        for (int pair = 1; pair <= 6; pair++) {
            foreach (var mode in pair % 2 == 1 ? new[] { "limited", "full" } : new[] { "full", "limited" }) {
                var clock = Stopwatch.StartNew(); records.Add(Sample(pair, mode, "before", clock.ElapsedMilliseconds));
                await LoadResource(mode); clock.Restart(); records.Add(Sample(pair, mode, "loaded-0", clock.ElapsedMilliseconds));
                // These delays define resource observation windows, never readiness or functional success.
                await Task.Delay(250); records.Add(Sample(pair, mode, "loaded-250", clock.ElapsedMilliseconds));
                await Task.Delay(Math.Max(0, 1000 - (int)clock.ElapsedMilliseconds)); records.Add(Sample(pair, mode, "loaded-1000", clock.ElapsedMilliseconds));
                await Clear(); clock.Restart(); records.Add(Sample(pair, mode, "cleared-0", clock.ElapsedMilliseconds));
                await Task.Delay(250); records.Add(Sample(pair, mode, "cleared-250", clock.ElapsedMilliseconds));
                await Task.Delay(Math.Max(0, 1000 - (int)clock.ElapsedMilliseconds)); records.Add(Sample(pair, mode, "cleared-1000", clock.ElapsedMilliseconds));
                Save();
            }
        }
        host.Children.Add(new TextBlock { Text = "Six matched pairs complete; host cleared. Process memory is a proxy, not native decoder attribution." });
        await Frames();
    }
}
