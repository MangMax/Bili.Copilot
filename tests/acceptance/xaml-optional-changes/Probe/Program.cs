// Copyright (c) Bili Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BiliCopilot.Acceptance.XamlOptionalChanges;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Settings;
using XamlSettings = Microsoft.UI.Xaml.Settings.XamlOptionalChanges;

namespace BiliCopilot.Acceptance.XamlOptionalChanges;

/// <summary>
/// 零/全部/指定 XAML 可选开关下的运行时探针。开关必须在 Application.Start 之前启用，
/// 所以这里用 DISABLE_XAML_GENERATED_MAIN 自己接管入口。
/// </summary>
internal static class Program
{
    private static string? _tracePath;

    internal static ProbeOptions? PendingOptions { get; set; }

    internal static ProbeJournal? PendingJournal { get; set; }

    internal static void LogTrace(string message)
    {
        try
        {
            if (_tracePath is null)
            {
                return;
            }

            File.AppendAllText(
                _tracePath,
                $"{DateTimeOffset.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Tracing must never take the probe down.
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        ProbeOptions options;
        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.Exit(2);
            return;
        }

        PendingOptions = options;
        _tracePath = options.OutputPath + ".log";
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Program.LogTrace("AppDomain.UnhandledException: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Program.LogTrace("UnobservedTaskException: " + e.Exception);
        Program.LogTrace("main: options parsed");
        var journal = new ProbeJournal
        {
            Variant = options.Variant,
            Switches = options.Switches.ToArray(),
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        PendingJournal = journal;
        journal.StatesBeforeStart = CaptureStates();
        foreach (var name in options.Switches)
        {
            try
            {
                XamlSettings.EnableChange(ToChangeId(name));
            }
            catch (Exception ex)
            {
                journal.EnableErrors.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        journal.StatesAfterEnable = CaptureStates();
        Application.Start(_ => new ProbeApp());
    }

    internal static Dictionary<string, bool> CaptureStates()
    {
        var states = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["IsLocked"] = XamlSettings.IsLocked(),
        };
        foreach (var name in ProbeSwitches.All)
        {
            states[name] = XamlSettings.IsChangeEnabled(ToChangeId(name));
        }

        return states;
    }

    private static XamlChangeId ToChangeId(string name) => name switch
    {
        ProbeSwitches.DefaultStyleOptimizations => XamlChangeId.DefaultStyleOptimizations,
        ProbeSwitches.DeferContextFlyoutInit => XamlChangeId.DeferContextFlyoutInit,
        ProbeSwitches.IconNoGridOptimization => XamlChangeId.IconNoGridOptimization,
        ProbeSwitches.OptimizeApplyStyles => XamlChangeId.OptimizeApplyStyles,
        _ => throw new ArgumentException($"Unknown XAML optional change '{name}'."),
    };

    internal static string Format(bool value) => value ? "true" : "false";
}

public sealed class ProbeOptions
{
    public string Variant { get; private init; } = "custom";

    public IReadOnlyList<string> Switches { get; private init; } = Array.Empty<string>();

    public IReadOnlyList<ProbeExpectation> Expectations { get; private init; } = Array.Empty<ProbeExpectation>();

    public string OutputPath { get; private init; } = "probe.json";

    public string? ScreenshotDirectory { get; private init; }

    public bool CaptureMenu { get; private init; } = true;

    public static ProbeOptions Parse(string[] args)
    {
        var variant = "custom";
        var switches = "none";
        var output = "probe.json";
        string? screenshotDirectory = null;
        var captureMenu = true;
        var expectations = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {args[i]}.");
                }

                return args[++i];
            }

            switch (args[i])
            {
                case "--variant":
                    variant = Next();
                    break;
                case "--switches":
                    switches = Next();
                    break;
                case "--expect":
                    expectations.Add(Next());
                    break;
                case "--out":
                    output = Next();
                    break;
                case "--screenshot-dir":
                    screenshotDirectory = Next();
                    break;
                case "--no-menu":
                    captureMenu = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        return new ProbeOptions
        {
            Variant = variant,
            Switches = ProbeSwitches.Parse(switches),
            Expectations = ProbeExpectations.Parse(expectations),
            OutputPath = Path.GetFullPath(output),
            ScreenshotDirectory = screenshotDirectory is null ? null : Path.GetFullPath(screenshotDirectory),
            CaptureMenu = captureMenu,
        };
    }
}

public sealed class ProbeJournal
{
    public string Variant { get; set; } = string.Empty;

    public string[] Switches { get; set; } = Array.Empty<string>();

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset FinishedAtUtc { get; set; }

    public List<string> EnableErrors { get; set; } = new();

    public Dictionary<string, bool> StatesBeforeStart { get; set; } = new();

    public Dictionary<string, bool> StatesAfterEnable { get; set; } = new();

    public Dictionary<string, bool> StatesInsideStart { get; set; } = new();

    public string[] Screenshots { get; set; } = Array.Empty<string>();

    public int PopupWindowCount { get; set; }

    public Dictionary<string, string> Observed { get; set; } = new();

    public List<ProbeExpectationResult> Expectations { get; set; } = new();

    public string[] FailureReasons { get; set; } = Array.Empty<string>();

    public bool Passed { get; set; }

    public double BootMs { get; set; }

    public string WindowSize { get; set; } = string.Empty;

    public uint Dpi { get; set; }

    public string? Error { get; set; }
}
