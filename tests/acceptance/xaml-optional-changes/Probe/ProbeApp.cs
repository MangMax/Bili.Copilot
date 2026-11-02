// Copyright (c) Bili Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BiliCopilot.Acceptance.XamlOptionalChanges;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace BiliCopilot.Acceptance.XamlOptionalChanges;

/// <summary>
/// 探针窗口与观察逻辑。没有 App.xaml：探针不需要产品资源，
/// 也不该因为探针自己的资源字典影响被测的默认样式行为。
/// </summary>
public sealed class ProbeApp : Application
{
    private const string XamlNamespace = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

    private readonly ProbeOptions _options;
    private readonly ProbeJournal _journal;
    private readonly Stopwatch _boot = Stopwatch.StartNew();
    private readonly List<string> _screenshots = new();
    private Window? _window;
    private IntPtr _windowHandle;
    private FontIcon? _fontIcon;
    private SymbolIcon? _symbolIcon;
    private Button? _xamlButton;
    private Button? _menuButton;
    private MenuFlyout? _menuFlyout;
    private TextBlock? _textBlock;
    private RichTextBlock? _richTextBlock;
    private bool _xamlButtonTemplateBeforeTree;
    private bool _codeButtonTemplateBeforeTree;
    private string _fontIconTree = "none";
    private string _symbolIconTree = "none";

    public ProbeApp()
    {
        Program.LogTrace("app: enter constructor");
        _options = Program.PendingOptions
            ?? throw new InvalidOperationException("Probe options were not configured before Application.Start.");
        _journal = Program.PendingJournal
            ?? throw new InvalidOperationException("Probe journal was not configured before Application.Start.");
        UnhandledException += (_, e) =>
        {
            Program.LogTrace("Application.UnhandledException: " + e.Exception);
            _journal.Error = e.Exception?.ToString();
            WriteJournal();
        };
        Program.LogTrace("app: constructor");
        try
        {
            _journal.StatesInsideStart = Program.CaptureStates();
            BuildUi();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private void BuildUi()
    {
        Program.LogTrace("build: start");
        _window = new Window { Title = "XAML optional changes probe" };
        _window.AppWindow.Resize(new SizeInt32(1200, 800));
        _windowHandle = WindowNative.GetWindowHandle(_window);

        var root = new Grid
        {
            Padding = new Thickness(24),
            RowSpacing = 16,
            ColumnSpacing = 16,
            RequestedTheme = ElementTheme.Light,
            Background = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke),
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "XAML optional changes probe",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = "Fixed gallery: icons, buttons, programmatic MenuFlyout, rich text and theme surface",
            FontSize = 13,
            Opacity = 0.7,
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var gallery = new Grid { ColumnSpacing = 24, RowSpacing = 20 };
        gallery.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gallery.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gallery.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gallery.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        gallery.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _fontIcon = new FontIcon { Glyph = "\uE700", FontSize = 28 };
        var fontIconHost = NewIconHost(_fontIcon);
        _symbolIcon = (SymbolIcon)XamlReader.Load(
            "<SymbolIcon " + XamlNamespace + " Symbol='Accept' />");
        var symbolIconHost = NewIconHost(_symbolIcon);

        var iconPanel = NewGroup("Icons");
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        iconRow.Children.Add(fontIconHost);
        iconRow.Children.Add(symbolIconHost);
        iconPanel.Children.Add(iconRow);
        iconPanel.Children.Add(new TextBlock { Text = "FontIcon / SymbolIcon", FontSize = 12, Opacity = 0.6 });
        Grid.SetColumn(iconPanel, 0);
        gallery.Children.Add(iconPanel);

        var buttonPanel = NewGroup("Buttons");
        _xamlButton = (Button)XamlReader.Load(
            "<Button " + XamlNamespace + " Content='XamlReader button' />");
        _xamlButtonTemplateBeforeTree = _xamlButton.Template is not null;
        var codeButton = new Button { Content = "Code button" };
        _codeButtonTemplateBeforeTree = codeButton.Template is not null;
        var accentButton = new Button
        {
            Content = "Accent button",
            Style = Application.Current.Resources["AccentButtonStyle"] as Style,
        };
        var disabledButton = new Button { Content = "Disabled button", IsEnabled = false };
        _menuButton = new Button { Content = "Open menu" };
        _menuFlyout = new MenuFlyout();
        _menuFlyout.Items.Add(new MenuFlyoutItem { Text = "Copy" });
        _menuFlyout.Items.Add(new MenuFlyoutItem { Text = "Paste" });
        _menuFlyout.Items.Add(new MenuFlyoutSeparator());
        _menuFlyout.Items.Add(new MenuFlyoutItem { Text = "Select all", Icon = new SymbolIcon { Symbol = Symbol.SelectAll } });
        _menuFlyout.Items.Add(new MenuFlyoutItem { Text = "Delete", Icon = new SymbolIcon { Symbol = Symbol.Delete } });
        _menuButton.Flyout = _menuFlyout;
        var buttonRow = new StackPanel { Spacing = 6 };
        buttonRow.Children.Add(_xamlButton);
        buttonRow.Children.Add(codeButton);
        buttonRow.Children.Add(accentButton);
        buttonRow.Children.Add(disabledButton);
        buttonPanel.Children.Add(buttonRow);
        buttonPanel.Children.Add(new TextBlock { Text = "Default / accent / disabled / MenuFlyout", FontSize = 12, Opacity = 0.6 });
        Grid.SetColumn(buttonPanel, 1);
        gallery.Children.Add(buttonPanel);

        var textPanel = NewGroup("Rich text");
        _richTextBlock = new RichTextBlock { Width = 230, TextWrapping = TextWrapping.Wrap };
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run { Text = "RichTextBlock sample text." });
        _richTextBlock.Blocks.Add(paragraph);
        textPanel.Children.Add(_richTextBlock);
        // Selectable TextBlock / RichTextBlock are the controls whose default
        // TextControlCommandBarContextFlyout DeferContextFlyoutInit changes.
        _textBlock = new TextBlock
        {
            Text = "Selectable TextBlock sample text.",
            Width = 230,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        textPanel.Children.Add(_textBlock);
        textPanel.Children.Add(new TextBlock
        {
            Text = "TextBox / RichEditBox cannot be instantiated in this probe: see README limits.",
            Width = 230,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.6,
        });
        Grid.SetColumn(textPanel, 2);
        gallery.Children.Add(textPanel);

        var themePanel = NewGroup("Default styles and theme");
        var themeBorder = new Border
        {
            Width = 230,
            Height = 60,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Child = new TextBlock
            {
                Text = "Theme surface",
                Margin = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        themePanel.Children.Add(themeBorder);
        themePanel.Children.Add(new TextBlock
        {
            Text = "Button / icon default styles are exercised above.",
            Width = 230,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.6,
        });
        Grid.SetColumn(themePanel, 0);
        Grid.SetRow(themePanel, 1);
        gallery.Children.Add(themePanel);

        var menuHint = NewGroup("Menu gallery");
        menuHint.Children.Add(_menuButton);
        menuHint.Children.Add(new TextBlock
        {
            Text = "A MenuFlyout is opened programmatically after load; the popup window is captured separately.",
            Width = 460,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.7,
        });
        Grid.SetColumn(menuHint, 1);
        Grid.SetRow(menuHint, 1);
        Grid.SetColumnSpan(menuHint, 2);
        gallery.Children.Add(menuHint);

        Grid.SetRow(gallery, 1);
        root.Children.Add(gallery);

        var footer = new TextBlock
        {
            Text = "Fixed 1200x800 window, light theme, deterministic content.",
            FontSize = 12,
            Opacity = 0.6,
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _window.Content = root;
        root.Loaded += OnLoaded;
        Program.LogTrace("build: activating");
        _window.Activate();
        Program.LogTrace("build: activated");
    }

    private static Border NewIconHost(IconElement icon) => new()
    {
        Width = 44,
        Height = 44,
        Child = icon,
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 0, 0)),
        CornerRadius = new CornerRadius(4),
    };

    private static StackPanel NewGroup(string title)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        return panel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Program.LogTrace("loaded: enter");
        // WinUI 3 没有 SynchronizationContext，await 的续体会落到线程池；
        // 用 DispatcherQueue 定时器保证后续观察与截图都在 UI 线程。
        RunAfter(700, () =>
        {
            try
            {
                Program.LogTrace("loaded: delay done");
                CaptureObservations();
                Program.LogTrace("loaded: observations captured");

                if (_options.CaptureMenu && _menuButton is not null && _menuFlyout is not null)
                {
                    Program.LogTrace("loaded: showing flyout");
                    _menuFlyout.ShowAt(_menuButton);
                    RunAfter(600, () =>
                    {
                        Program.LogTrace("loaded: capturing popup");
                        CapturePopup("gallery-menu", "menuPopupCaptured");
                        _menuFlyout.Hide();
                        RunAfter(300, CaptureTextFlyouts);
                    });
                    return;
                }

                CaptureTextFlyouts();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        });
    }

    private void CompleteCapture()
    {
        try
        {
            Program.LogTrace("loaded: capturing window");
            CaptureWindow();
            Program.LogTrace("loaded: finishing");
            Finish();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private void RunAfter(int milliseconds, Action action)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private void CaptureObservations()
    {
        if (WindowCapture.TryGetBounds(_windowHandle, out var windowBounds))
        {
            _journal.WindowSize = $"{windowBounds.Width}x{windowBounds.Height}";
            _journal.Dpi = WindowCapture.GetDpi(_windowHandle);
        }

        _fontIconTree = DescribeTree(_fontIcon!, 0, 3);
        _symbolIconTree = DescribeTree(_symbolIcon!, 0, 3);

        var observed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void AddState(string prefix, Dictionary<string, bool>? states)
        {
            observed[$"{prefix}.IsLocked"] = Program.Format(states?["IsLocked"] ?? false);
            foreach (var name in ProbeSwitches.All)
            {
                observed[$"{prefix}.{name}"] = Program.Format(states?.GetValueOrDefault(name) ?? false);
            }
        }

        AddState("before", _journal.StatesBeforeStart);
        AddState("afterEnable", _journal.StatesAfterEnable);
        AddState("inside", _journal.StatesInsideStart);

        observed["fontIconShape"] = FirstChildType(_fontIcon!);
        observed["symbolIconShape"] = FirstChildType(_symbolIcon!);
        observed["fontIconTree"] = _fontIconTree;
        observed["symbolIconTree"] = _symbolIconTree;
        observed["xamlButtonTemplateBeforeTree"] = Program.Format(_xamlButtonTemplateBeforeTree);
        observed["codeButtonTemplateBeforeTree"] = Program.Format(_codeButtonTemplateBeforeTree);
        observed["xamlButtonTemplateAfterTree"] = Program.Format(_xamlButton?.Template is not null);
        observed["enableErrorCount"] = _journal.EnableErrors.Count.ToString(CultureInfo.InvariantCulture);
        observed["bootMs"] = _boot.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
        observed["menuItemCount"] = (_menuFlyout?.Items.Count ?? 0).ToString(CultureInfo.InvariantCulture);
        observed["windowSize"] = _journal.WindowSize;

        _journal.Observed = observed;
    }

    private static string FirstChildType(DependencyObject node)
    {
        if (VisualTreeHelper.GetChildrenCount(node) == 0)
        {
            return "none";
        }

        return VisualTreeHelper.GetChild(node, 0).GetType().Name;
    }

    private static string DescribeTree(DependencyObject node, int depth, int maxDepth)
    {
        if (depth >= maxDepth)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            var name = child.GetType().Name;
            var nested = DescribeTree(child, depth + 1, maxDepth);
            parts.Add(string.IsNullOrEmpty(nested) ? name : $"{name}({nested})");
        }

        return string.Join(",", parts);
    }

    private void CapturePopup(string fileStem, string observedKey)
    {
        if (_options.ScreenshotDirectory is null)
        {
            return;
        }

        var windows = WindowCapture.GetVisibleWindows((uint)Environment.ProcessId, _windowHandle);
        _journal.PopupWindowCount = windows.Count;
        if (windows.Count == 0)
        {
            _journal.Observed[observedKey] = "no-popup";
            return;
        }

        var path = Path.Combine(_options.ScreenshotDirectory, fileStem + ".bmp");
        if (WindowCapture.TryCapture(windows[0], path, out var bounds))
        {
            _screenshots.Add($"{path}|{bounds.Width}x{bounds.Height}");
            _journal.Observed[observedKey] = "ok";
            _journal.Observed[observedKey.Replace("Captured", "Width")] = bounds.Width.ToString(CultureInfo.InvariantCulture);
            _journal.Observed[observedKey.Replace("Captured", "Height")] = bounds.Height.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            _journal.Observed[observedKey] = "failed";
        }
    }

    private void CaptureTextFlyouts()
    {
        var targets = new (string Key, string FileStem, FrameworkElement? Element)[]
        {
            ("textBlock", "gallery-text-block", _textBlock),
            ("richText", "gallery-rich-text", _richTextBlock),
        };
        CaptureTextFlyoutAt(targets, 0);
    }

    private void CaptureTextFlyoutAt(
        (string Key, string FileStem, FrameworkElement? Element)[] targets,
        int index)
    {
        if (index >= targets.Length)
        {
            CompleteCapture();
            return;
        }

        var (key, fileStem, element) = targets[index];
        var capturedKey = key + "FlyoutCaptured";
        var typeKey = key + "FlyoutType";
        if (element is null)
        {
            _journal.Observed[capturedKey] = "not-observable";
            _journal.Observed[typeKey] = "null";
            RunAfter(100, () => CaptureTextFlyoutAt(targets, index + 1));
            return;
        }

        FlyoutBase? flyout;
        try
        {
            flyout = element.ContextFlyout;
        }
        catch (Exception ex)
        {
            Program.LogTrace($"text flyout {key}: ContextFlyout threw {ex.GetType().Name}: {ex.Message}");
            _journal.Observed[typeKey] = "error";
            _journal.Observed[capturedKey] = "not-observable";
            RunAfter(100, () => CaptureTextFlyoutAt(targets, index + 1));
            return;
        }

        Program.LogTrace($"text flyout {key}: type={flyout?.GetType().Name ?? "null"}");
        _journal.Observed[typeKey] = flyout?.GetType().Name ?? "null";
        if (flyout is null)
        {
            // The default TextControlCommandBarContextFlyout is not exposed by
            // the ContextFlyout getter; record that instead of implying cover.
            _journal.Observed[capturedKey] = "not-observable";
            RunAfter(100, () => CaptureTextFlyoutAt(targets, index + 1));
            return;
        }

        try
        {
            flyout.ShowAt(element);
        }
        catch (Exception ex)
        {
            Program.LogTrace($"text flyout {key}: ShowAt threw {ex.GetType().Name}: {ex.Message}");
            _journal.Observed[capturedKey] = "failed";
            RunAfter(100, () => CaptureTextFlyoutAt(targets, index + 1));
            return;
        }

        RunAfter(600, () =>
        {
            CapturePopup(fileStem, capturedKey);
            flyout.Hide();
            RunAfter(300, () => CaptureTextFlyoutAt(targets, index + 1));
        });
    }

    private void CaptureWindow()
    {
        if (!WindowCapture.TryGetBounds(_windowHandle, out var bounds))
        {
            _journal.Observed["screenshotMain"] = "no-window";
            return;
        }

        _journal.WindowSize = $"{bounds.Width}x{bounds.Height}";
        _journal.Dpi = WindowCapture.GetDpi(_windowHandle);
        _journal.Observed["windowWidth"] = bounds.Width.ToString(CultureInfo.InvariantCulture);
        _journal.Observed["windowHeight"] = bounds.Height.ToString(CultureInfo.InvariantCulture);
        _journal.Observed["dpi"] = _journal.Dpi.ToString(CultureInfo.InvariantCulture);
        _journal.Observed["windowSize"] = _journal.WindowSize;

        if (_options.ScreenshotDirectory is null)
        {
            _journal.Observed["screenshotMain"] = "skipped";
            return;
        }

        var path = Path.Combine(_options.ScreenshotDirectory, "gallery.bmp");
        if (WindowCapture.TryCapture(_windowHandle, path, out _))
        {
            _screenshots.Add(path);
            _journal.Observed["screenshotMain"] = "ok";
        }
        else
        {
            _journal.Observed["screenshotMain"] = "failed";
        }
    }

    private void Finish()
    {
        _journal.FinishedAtUtc = DateTimeOffset.UtcNow;
        _journal.BootMs = _boot.Elapsed.TotalMilliseconds;
        _journal.Screenshots = _screenshots.ToArray();
        var results = ProbeExpectations.Evaluate(_options.Expectations, _journal.Observed);
        _journal.Expectations = results.ToList();
        var failures = results.Where(x => !x.Passed)
            .Select(x => $"{x.Name}: expected '{x.Expected}', actual '{x.Actual}'")
            .Concat(_journal.EnableErrors)
            .ToArray();
        _journal.FailureReasons = failures;
        _journal.Passed = failures.Length == 0;
        WriteJournal();
        Console.Out.Write(JsonSerializer.Serialize(_journal, JsonOptions));
        Console.Out.Flush();
        Environment.Exit(_journal.Passed ? 0 : 1);
    }

    private void Fail(Exception ex)
    {
        Program.LogTrace("fail: " + ex);
        _journal.Error = $"{ex.GetType().Name}: {ex.Message}";
        _journal.Passed = false;
        try
        {
            WriteJournal();
        }
        catch
        {
            // Nothing else we can do for the harness; the exit code still signals failure.
        }

        Environment.Exit(2);
    }

    private void WriteJournal()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_options.OutputPath)!);
        File.WriteAllText(_options.OutputPath, JsonSerializer.Serialize(_journal, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
