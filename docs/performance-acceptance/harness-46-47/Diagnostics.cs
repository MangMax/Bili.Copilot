using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Acceptance;

public partial class App
{
    private sealed class ImageTrace
    {
        public int Id;
        public DateTimeOffset Created;
        public DateTimeOffset? Opened;
        public DateTimeOffset? FailedAt;
        public string Failed;
    }
    // Diagnostic-only retention, bounded by this finite fixture run; never production telemetry.
    private static readonly Dictionary<Image, ImageTrace> imageTraces = new();
    private static Action imageChanged;
    private TaskCompletionSource checkpoint;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private object Snapshot()
    {
        var doc = Document();
        var inlines = doc?.Blocks.OfType<Paragraph>().SelectMany(p => Inlines(p.Inlines)).ToArray() ?? [];
        var text = string.Concat(inlines.OfType<Run>().Select(r => r.Text));
        var visuals = Descendants(markdown).ToArray();
        var richTexts = visuals.OfType<RichTextBlock>().Select(t => string.Concat(t.Blocks.OfType<Paragraph>().SelectMany(p => Inlines(p.Inlines)).OfType<Run>().Select(r => r.Text))).ToArray();
        var images = visuals.OfType<Image>().ToArray();
        var links = inlines.OfType<Hyperlink>().Select(h => h.NavigateUri?.AbsoluteUri).ToArray();
        return new {
            elapsedMs = clock.Elapsed.TotalMilliseconds,
            text, richTexts, links, textEqualsFixture = markdown.Text == fixture,
            controlIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(markdown),
            blocks = Blocks(), loaded = markdown.IsLoaded,
            predicates = new {
                document = doc != null, blocksAtLeastSix = Blocks() >= 6,
                finalOmega = text.Contains("FINAL-OMEGA"),
                hyperlink = links.Contains("https://example.invalid/acceptance-4647"),
                image = images.Any(i => i.Source != null && i.ActualWidth > 0 && i.ActualHeight > 0),
                codeLabel = visuals.OfType<TextBlock>().Any(t => t.Text == "CSHARP"),
                tableFixture = richTexts.Contains("fixture"), table47 = richTexts.Contains("47"),
                codeBody = richTexts.Any(t => t.Contains("fixture-4647")),
            },
            images = images.Select(i => {
                imageTraces.TryGetValue(i, out var trace);
                var bitmap = i.Source as BitmapImage;
                return new { id = trace?.Id, source = bitmap?.UriSource?.ToString(), sourceType = i.Source?.GetType().Name,
                    width = i.ActualWidth, height = i.ActualHeight, i.IsLoaded,
                    pixelWidth = bitmap?.PixelWidth, pixelHeight = bitmap?.PixelHeight,
                    created = trace?.Created, opened = trace?.Opened, failed = trace?.Failed, failedAt = trace?.FailedAt };
            }).ToArray(),
            rendered = Rendered()
        };
    }

    private bool ImagesReady() => Descendants(markdown).OfType<Image>().Any(i =>
        i.Source != null && i.ActualWidth > 0 && i.ActualHeight > 0 &&
        imageTraces.TryGetValue(i, out var t) && t.Opened != null && t.Failed == null);

    private async Task ReadyRecord(string name, Func<bool> assertion)
    {
        var completion = new TaskCompletionSource<string>();
        var started = clock.Elapsed.TotalMilliseconds;
        void Evaluate(string trigger)
        {
            var images = Descendants(markdown).OfType<Image>().ToArray();
            if (images.Any(i => imageTraces.TryGetValue(i, out var t) && t.Failed != null)) completion.TrySetResult("ImageFailed");
            else if (markdown.IsLoaded && ImagesReady()) completion.TrySetResult(trigger);
        }
        EventHandler<object> layout = (_, _) => Evaluate("LayoutUpdated+ImageOpened");
        Action changed = () => Evaluate("ImageOpened+layout-size");
        host.LayoutUpdated += layout;
        imageChanged += changed;
        string terminal;
        try
        {
            Evaluate("already-opened-and-sized");
            terminal = await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException) { terminal = "timeout"; }
        finally { host.LayoutUpdated -= layout; imageChanged -= changed; }
        Record(name + "Ready", terminal != "timeout" && terminal != "ImageFailed" && ImagesReady() && assertion(),
            $"terminal={terminal}; waitedMs={clock.Elapsed.TotalMilliseconds - started:F3}; original assertion retained; ImageOpened and positive layout size required");
    }

    private async Task Checkpoint()
    {
        checkpoint = new TaskCompletionSource();
        try { await checkpoint.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { checkpoint = null; }
    }
}
