// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Owned native tray resources and their live Shell availability.
/// </summary>
internal interface ITrayIconSession : IDisposable
{
    /// <summary>
    /// Raised on the UI thread after Shell recreation has completed, including failure.
    /// </summary>
    event EventHandler? HealthChanged;

    bool IsCreated { get; }
}
