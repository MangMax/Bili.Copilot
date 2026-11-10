// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.Live;

/// <summary>One rule per line preserves selected phrases, including spaces and punctuation.</summary>
public static class LiveChatBlockInput
{
    /// <summary>Reads nonempty, distinct rules.</summary>
    public static string[] Lines(string? text)
        => (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Reads positive user identifiers.</summary>
    public static long[] UserIds(string? text)
        => (text ?? string.Empty).Split([',', '，', ';', '；', '\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0).Distinct().ToArray();

    /// <summary>Adds an exact phrase without replacing existing rules.</summary>
    public static string Add(string? text, string value)
        => string.Join(Environment.NewLine, Lines(text).Concat(Lines(value)).Distinct(StringComparer.OrdinalIgnoreCase));
}
