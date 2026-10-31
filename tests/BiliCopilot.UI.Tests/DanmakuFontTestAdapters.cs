// Copyright (c) Bili Copilot. All rights reserved.

namespace Richasy.WinUIKernel.Share.Toolkits;

public sealed class SystemFont
{
    public string LocalName { get; set; } = string.Empty;

    public string EngName { get; set; } = string.Empty;
}

public interface IFontToolkit
{
    Task<IReadOnlyList<SystemFont>> GetFontsAsync();
}
