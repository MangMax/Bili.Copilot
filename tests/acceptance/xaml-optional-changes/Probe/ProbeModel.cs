// Copyright (c) Bili Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliCopilot.Acceptance.XamlOptionalChanges;

/// <summary>
/// 可 A/B 的 XAML 可选开关清单。名称必须与
/// <c>Microsoft.UI.Xaml.Settings.XamlChangeId</c> 的成员名一致，
/// 也就是 MSBuild 属性 <c>EnabledXamlOptionalChanges</c> 接受的名称。
/// </summary>
public static class ProbeSwitches
{
    public const string DefaultStyleOptimizations = "DefaultStyleOptimizations";

    public const string DeferContextFlyoutInit = "DeferContextFlyoutInit";

    public const string IconNoGridOptimization = "IconNoGridOptimization";

    public const string OptimizeApplyStyles = "OptimizeApplyStyles";

    private static readonly string[] Canonical =
    {
        DefaultStyleOptimizations,
        DeferContextFlyoutInit,
        IconNoGridOptimization,
        OptimizeApplyStyles,
    };

    /// <summary>
    /// 四个开关的规范顺序。
    /// </summary>
    public static IReadOnlyList<string> All => Canonical;

    /// <summary>
    /// 解析探针的 <c>--switches</c> 参数：<c>none</c>、<c>all</c> 或逗号分隔的开关名。
    /// 名称大小写不敏感，但返回规范大小写。
    /// </summary>
    /// <exception cref="ArgumentException">名称未知或重复。</exception>
    public static IReadOnlyList<string> Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec) || spec.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        if (spec.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Canonical;
        }

        var result = new List<string>();
        foreach (var raw in spec.Split(','))
        {
            var name = raw.Trim();
            if (name.Length == 0)
            {
                throw new ArgumentException($"Switch list '{spec}' contains an empty entry.");
            }

            var canonical = Canonical.FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
            {
                throw new ArgumentException(
                    $"Unknown XAML optional change '{name}'. Valid names: {string.Join(", ", Canonical)}.");
            }

            if (result.Contains(canonical, StringComparer.Ordinal))
            {
                throw new ArgumentException($"XAML optional change '{canonical}' is listed more than once.");
            }

            result.Add(canonical);
        }

        return result;
    }
}

/// <summary>
/// 一条由测试装置提供给探针的期望，例如 <c>fontIconShape=TextBlock</c>。
/// 探针把观察到的值与期望逐条比较，任何一条不符就以非零退出码结束。
/// </summary>
public sealed record ProbeExpectation(string Name, string Expected);

/// <summary>
/// 一条期望的比较结果，写入探针 JSON 供后续审计。
/// </summary>
public sealed record ProbeExpectationResult(string Name, string Expected, string Actual, bool Passed);

/// <summary>
/// 解析并判定探针期望。这里不依赖 WinUI，方便在普通单元测试里覆盖退出码语义。
/// </summary>
public static class ProbeExpectations
{
    public const string MissingObservation = "<missing>";

    public static IReadOnlyList<ProbeExpectation> Parse(IEnumerable<string> values)
    {
        var result = new List<ProbeExpectation>();
        foreach (var value in values)
        {
            var text = value?.Trim() ?? string.Empty;
            var separator = text.IndexOf('=');
            if (separator <= 0 || separator == text.Length - 1)
            {
                throw new ArgumentException(
                    $"Expectation '{value}' must use the form name=value with a non-empty name and value.");
            }

            var name = text[..separator].Trim();
            var expected = text[(separator + 1)..].Trim();
            if (name.Length == 0 || expected.Length == 0)
            {
                throw new ArgumentException(
                    $"Expectation '{value}' must use the form name=value with a non-empty name and value.");
            }

            if (result.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Expectation '{name}' is specified more than once.");
            }

            result.Add(new ProbeExpectation(name, expected));
        }

        return result;
    }

    /// <summary>
    /// 逐条比较观察值与期望。名称与值均大小写不敏感，忽略首尾空白；
    /// 缺少观察值时按失败处理并标记为 <c>&lt;missing&gt;</c>，避免漏测被当作通过。
    /// </summary>
    public static IReadOnlyList<ProbeExpectationResult> Evaluate(
        IEnumerable<ProbeExpectation> expectations,
        IReadOnlyDictionary<string, string> observed)
    {
        var results = new List<ProbeExpectationResult>();
        foreach (var expectation in expectations)
        {
            if (!observed.TryGetValue(expectation.Name, out var actual))
            {
                var match = observed.FirstOrDefault(
                    x => x.Key.Equals(expectation.Name, StringComparison.OrdinalIgnoreCase));
                actual = match.Key is null ? null : match.Value;
            }

            if (actual is null)
            {
                results.Add(new ProbeExpectationResult(
                    expectation.Name,
                    expectation.Expected,
                    MissingObservation,
                    false));
                continue;
            }

            var passed = actual.Trim().Equals(expectation.Expected.Trim(), StringComparison.OrdinalIgnoreCase);
            results.Add(new ProbeExpectationResult(expectation.Name, expectation.Expected, actual, passed));
        }

        return results;
    }
}
