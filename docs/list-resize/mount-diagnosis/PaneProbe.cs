using System.Collections.ObjectModel;
using System.Text.Json;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Base.Repeater;
using Windows.Storage;

namespace Acceptance5053;
public partial class App
{
    async Task RunPaneProbe()
    {
        if (matrixRunning) return;
        matrixRunning = true;
        var output = ApplicationData.Current.LocalFolder.Path;
        var mode = File.ReadAllText(Path.Combine(output, "related-family.txt")).Trim();
        var cache = double.Parse(File.ReadAllText(Path.Combine(output, "mount-cache.txt")), System.Globalization.CultureInfo.InvariantCulture);
        var samples = new List<object>();
        try
        {
            ImageExBase.EnableBackgroundDecoding = false;
            UserItemViewModel.LegacyPaneData = true;
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Repeater.xaml") });
            slot.Children.Clear();
            window.AppWindow.Resize(new(1660, 1060));
            slot.ColumnDefinitions[0].Width = new GridLength(400);
            var templates = new MatrixTemplates();
            var shared = (UniformGridLayout)Resources["UserLayout"];
            var independent = new UniformGridLayout {
                ItemsStretch = shared.ItemsStretch, MinColumnSpacing = shared.MinColumnSpacing,
                MinItemHeight = shared.MinItemHeight, MinItemWidth = shared.MinItemWidth,
                MinRowSpacing = shared.MinRowSpacing
            };
            var template = (DataTemplate)templates[mode.StartsWith("PaneLegacy") ? "UserRed" : "User"];
            ObservableCollection<object> Items() => new(Enumerable.Range(0, 500).Select(_ => (object)new UserItemViewModel()));
            var left = new AppRepeater { Layout = shared, ItemTemplate = template, ItemsSource = Items() };
            var right = new AppRepeater { Layout = mode.EndsWith("Independent") ? independent : shared, ItemTemplate = template, ItemsSource = Items() };
            RepeaterCachePolicy.SetVerticalCacheLength(left, cache);
            RepeaterCachePolicy.SetVerticalCacheLength(right, cache);
            var a = new IncrementVerticalScrollControl { Element = left, Height = 400 };
            var b = new IncrementVerticalScrollControl { Element = right, Height = 400 };
            var dual = !mode.EndsWith("Single");
            left.Loaded += (_, _) => Trace.Write("mount-phase", "left-Loaded");
            right.Loaded += (_, _) => Trace.Write("mount-phase", "right-Loaded");
            Grid.SetRow(a, 1); Grid.SetRow(b, 1); Grid.SetColumn(b, 1);
            Trace.Write("mount-phase", "pane-mount");
            slot.Children.Add(a); if (dual) slot.Children.Add(b);
            Trace.Write("mount-phase", "pane-measure");
            if(!mode.Contains("Natural"))slot.UpdateLayout();
            Trace.Write("mount-phase", mode.Contains("Natural") ? "pane-measure-scheduled" : "pane-measure-returned");
            await Task.Delay(2000);
            var av = (ScrollView)a.FindName("View"); var bv = (ScrollView)b.FindName("View");
            var ai = (ItemsRepeater)left.FindName("Repeater"); var bi = (ItemsRepeater)right.FindName("Repeater");
            int[] Range(ItemsRepeater ir) => Enumerable.Range(0, 500).Where(i => ir.TryGetElement(i) != null).ToArray();
            object Pane(ScrollView v, ItemsRepeater ir) => new { v.VerticalOffset, v.ViewportHeight, v.ViewportWidth, v.ExtentHeight, ir.VerticalCacheLength, realized = Range(ir) };
            void Sample(string phase) {
                samples.Add(new { phase, left = Pane(av, ai), right = dual ? Pane(bv, bi) : null });
                File.WriteAllText(Path.Combine(output, "matrix-panes.json"), JsonSerializer.Serialize(new { mode, cache, shared = ReferenceEquals(left.Layout, right.Layout), samples }, new JsonSerializerOptions { WriteIndented = true }));
            }
            void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
            Require(av.ViewportHeight > 0 && av.ViewportWidth > 0, "left finite viewport");
            Require(ai.VerticalCacheLength == cache && (!dual || bi.VerticalCacheLength == cache), "requested cache");
            if (dual) Require(bv.ViewportWidth > av.ViewportWidth, "distinct pane widths");
            Sample("mounted");
            Trace.Write("mount-phase", "pane-scroll-start");
            var rightRange = dual ? Range(bi) : [];
            var rightOffset = bv.VerticalOffset;
            await ScrollAndWait(av, av.ScrollableHeight); await Task.Delay(700); Sample("left-end");
            Require(ai.TryGetElement(499) != null, "left reaches final item");
            if (dual)
            {
                Require(Math.Abs(bv.VerticalOffset - rightOffset) < 1 && Range(bi).SequenceEqual(rightRange), "left scroll changes right offset/range");
                var leftRange = Range(ai); var leftOffset = av.VerticalOffset;
                await ScrollAndWait(bv, bv.ScrollableHeight); await Task.Delay(700); Sample("right-end");
                Require(bi.TryGetElement(499) != null && Math.Abs(av.VerticalOffset - leftOffset) < 1 && Range(ai).SequenceEqual(leftRange), "right scroll changes left offset/range");
                await ScrollAndWait(av, 0); await Task.Delay(700); Sample("left-start-right-end");
                Require(ai.TryGetElement(0) != null && bi.TryGetElement(499) != null && Math.Abs(bv.VerticalOffset - bv.ScrollableHeight) < 1, "independent opposite ranges");
            }
            await Capture(slot, Path.Combine(output, "matrix-panes.png"));
            File.WriteAllText(Path.Combine(output, "matrix-checks.json"), JsonSerializer.Serialize(new[] { "PaneIndependentOffsetsAndRealizedRanges/PASS" }));
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "matrix-error.txt"), e.ToString()); }
        File.WriteAllText(Path.Combine(output, "matrix-finished.txt"), DateTimeOffset.UtcNow.ToString("O"));
    }
}
