// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Carries an anonymous submission identity across deferred page loading.
/// The keyword is an in-memory navigation parameter, never diagnostic data.
/// </summary>
internal sealed record SearchSubmission(string Keyword, Guid Id)
{
    public override string ToString() => Id.ToString();
}
