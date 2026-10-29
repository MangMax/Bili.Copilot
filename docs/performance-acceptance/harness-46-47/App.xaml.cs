using BiliCopilot.UI.Controls.Markdown;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;

namespace Acceptance;

public partial class App : Application
{
    private Window window;
    private StackPanel host;
    private MarkdownTextBlock markdown;
    private TextBlock streaming;
    private TextBlock status;
    private string fixture;
    private StreamingResponseSession session;
    private readonly Queue<Action> pending = new();
    private readonly List<object> records = new();
    private int publications;
    private bool generating;
    private int unloads;
    private int loads;
    private int instance;

    public App() { InitializeComponent(); }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture.md"));
        window = new Window { Title = "Offline acceptance 46 47 — eeb28f2" };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1600, 1100));
        var root = new Grid { Padding = new Thickness(20), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Spacing = 8 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(row); toolbar.Children.Add(row2);
        Add(row, "Render", () => { markdown.Text = fixture; Observe("Render", () => Rendered(), "fixture text/table/code/link/image"); });
        Add(row, "Clear", () => { markdown.Text = ""; Observe("Clear", () => Blocks() == 0, "zero blocks"); });
        Add(row, "Reassign", () => { markdown.Text = fixture; Observe("Reassign", () => Rendered(), "full fixture restored"); });
        Add(row, "Detach", () => { host.Children.Remove(markdown); Observe("Detach", () => !markdown.IsLoaded && Blocks() == 0, "unloaded and blocks cleared"); });
        Add(row, "Attach", () => AttachAndObserve("SameInstanceUnchangedTextAttach"));
        Add(row, "Fresh", () => { host.Children.Remove(markdown); markdown = NewMarkdown(); AttachAndObserve("FreshReopen"); });
        Add(row2, "Stream", BeginStream);
        Add(row2, "Complete", () => {
            session.Append(fixture[(fixture.Length / 2)..]); session.Complete(); Drain();
            Observe("FinalFlush", () => publications == 2 && !generating && markdown.Text == fixture && Rendered(), "two publications; immediate final exact text including final tail");
        });
        Add(row2, "CancelLate", CancelLate);
        Add(row2, "CopyAnswer", () => { var data = new DataPackage(); data.SetText(markdown.Text); Clipboard.SetContent(data); Record("HarnessCopyAnswer", markdown.Text == fixture, "Known fixture only; harness copy, not AIViewModel.CopyResultCommand"); });
        Add(row2, "VerifyAnswerClipboard", async () => Record("AnswerClipboard", await Clipboard.GetContent().GetTextAsync() == fixture, "exact fixture including Markdown syntax"));
        Add(row2, "VerifyCodeClipboard", async () => Record("ProductionCodeClipboard", await Clipboard.GetContent().GetTextAsync() == "Console.WriteLine(\"fixture-4647\");", "exact production MyCodeBlock copy result"));
        status = new TextBlock { Text = "Ready", TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(status, "HarnessStatus");
        host = new StackPanel { Spacing = 12 };
        streaming = (TextBlock)XamlReader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Streaming.xaml")));
        streaming.Visibility = Visibility.Collapsed;
        host.Children.Add(streaming);
        markdown = NewMarkdown(); host.Children.Add(markdown);
        var scroll = new ScrollViewer { Content = host, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(toolbar); root.Children.Add(status); root.Children.Add(scroll);
        Grid.SetRow(status, 1); Grid.SetRow(scroll, 2);
        window.Content = root;
        window.Activate();
    }

    private MarkdownTextBlock NewMarkdown()
    {
        instance++;
        var value = new MarkdownTextBlock { Config = new MarkdownConfig { ImageProvider = new OfflineImageProvider() }, Text = fixture };
        AutomationProperties.SetAutomationId(value, "RealMarkdown");
        value.Unloaded += (_, _) => unloads++;
        value.Loaded += (_, _) => loads++;
        return value;
    }

    private void AttachAndObserve(string name)
    {
        RoutedEventHandler loaded = null;
        loaded = (_, _) => {
            markdown.Loaded -= loaded;
            Observe(name, () => markdown.IsLoaded && markdown.Text == fixture && Rendered(), "observe after actual Loaded; unchanged fixture must render");
        };
        markdown.Loaded += loaded;
        host.Children.Add(markdown);
    }

    private void BeginStream()
    {
        publications = 0; generating = true; pending.Clear(); markdown.Text = "";
        markdown.Visibility = Visibility.Collapsed; streaming.Visibility = Visibility.Visible;
        session = new StreamingResponseSession(pending.Enqueue, Publish);
        // Many chunks arrive before one controllable UI publication, no sleeps/network.
        foreach (var c in fixture[..(fixture.Length / 2)]) session.Append(c.ToString());
        var queued = pending.Count;
        Drain();
        Observe("CoalescedSelectableStreaming", () => queued == 1 && publications == 1 && generating && streaming.IsTextSelectionEnabled && streaming.Text == fixture[..(fixture.Length / 2)] && Blocks() == 0, "one queued publication; exact ordered selectable plain text; no Markdown blocks");
    }

    private void Publish(string text, bool final)
    {
        publications++; generating = !final;
        if (final) { streaming.Text = ""; streaming.Visibility = Visibility.Collapsed; markdown.Visibility = Visibility.Visible; markdown.Text = text; }
        else { streaming.Text = text; }
    }

    private void CancelLate()
    {
        pending.Clear(); publications = 0; generating = true;
        markdown.Text = ""; markdown.Visibility = Visibility.Collapsed; streaming.Visibility = Visibility.Visible;
        var old = new StreamingResponseSession(pending.Enqueue, Publish);
        old.Append("OLD-QUEUED"); var oldFlush = pending.Dequeue(); old.Cancel();
        var current = new StreamingResponseSession(pending.Enqueue, Publish);
        current.Append("NEW-ONLY"); Drain();
        old.Append("LATE-OLD"); old.Complete(); oldFlush();
        Record("CancelledLateBeforeNewCompletion", streaming.Text == "NEW-ONLY" && generating && publications == 1, "late old append, complete and queued flush cannot overwrite or stop new session");
        current.Append("-FINAL"); current.Complete(); oldFlush(); Drain();
        Observe("CancelledLateAfterNewCompletion", () => markdown.Text == "NEW-ONLY-FINAL" && !generating && publications == 2 && Blocks() > 0, "new final stays visible after old queued callbacks");
    }

    private void Drain() { while (pending.TryDequeue(out var callback)) callback(); }
    private void Add(StackPanel row, string id, Action action)
    {
        var button = new Button { Content = id };
        AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => { try { action(); } catch (Exception e) { Record(id, false, e.ToString()); } };
        row.Children.Add(button);
    }

    private void Observe(string name, Func<bool> assertion, string expected)
    {
        // Low-priority dispatcher observation lets real layout/Loaded/Unloaded run first.
        window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => { try { host.UpdateLayout(); Record(name, assertion(), expected); } catch (Exception e) { Record(name, false, e.ToString()); } });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private RichTextBlock Document() => Descendants(markdown).OfType<RichTextBlock>().FirstOrDefault();
    private int Blocks() => Document()?.Blocks.Count ?? 0;
    private static IEnumerable<Inline> Inlines(IEnumerable<Inline> items)
    {
        foreach (var inline in items) { yield return inline; if (inline is Span span) foreach (var nested in Inlines(span.Inlines)) yield return nested; }
    }
    private bool Rendered()
    {
        var document = Document();
        if (document == null) return false;
        var inlines = document.Blocks.OfType<Paragraph>().SelectMany(p => Inlines(p.Inlines)).ToArray();
        var text = string.Concat(inlines.OfType<Run>().Select(r => r.Text));
        var visuals = Descendants(markdown).ToArray();
        var richTexts = visuals.OfType<RichTextBlock>().Select(t => string.Concat(t.Blocks.OfType<Paragraph>().SelectMany(p => Inlines(p.Inlines)).OfType<Run>().Select(r => r.Text))).ToArray();
        return document.Blocks.Count >= 6 && text.Contains("FINAL-OMEGA")
            && inlines.OfType<Hyperlink>().Any(h => h.NavigateUri?.AbsoluteUri == "https://example.invalid/acceptance-4647")
            && visuals.OfType<Image>().Any(i => i.Source != null && i.ActualWidth > 0 && i.ActualHeight > 0)
            && visuals.OfType<TextBlock>().Any(t => t.Text == "CSHARP")
            && richTexts.Contains("fixture") && richTexts.Contains("47")
            && visuals.OfType<RichTextBlock>().Any(t => string.Concat(t.Blocks.OfType<Paragraph>().SelectMany(p => Inlines(p.Inlines)).OfType<Run>().Select(r => r.Text)).Contains("fixture-4647"));
    }

    private void Record(string name, bool passed, string expected)
    {
        status.Text = $"{name}: {(passed ? "PASS" : "FAIL")} | blocks={Blocks()}, loaded={markdown.IsLoaded}, instance={instance}, loads={loads}, unloads={unloads}";
        records.Add(new { name, passed, expected, blocks = Blocks(), loaded = markdown.IsLoaded, instance, loads, unloads, publications, generating, markdownText = markdown.Text, streamingText = streaming.Text, utc = DateTimeOffset.UtcNow });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "observations.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class OfflineImageProvider : IImageProvider
    {
        public bool ShouldUseThisProvider(string url) => url == "https://fixture.invalid/image.png";
        public Task<Image> GetImage(string url) => Task.FromResult(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Logo.png")), Width = 96, Height = 96 });
    }
}
