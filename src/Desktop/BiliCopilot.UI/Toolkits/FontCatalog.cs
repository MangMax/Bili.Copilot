// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Toolkits;

/// <summary>Serializes first-use enumeration across the two font pickers.</summary>
internal static class FontCatalog
{
    private static readonly AsyncValueCache<IReadOnlyList<SystemFont>> Cache = new();

    public static Task<IReadOnlyList<SystemFont>> GetFontsAsync(IFontToolkit toolkit)
        => Cache.GetAsync(() => toolkit.GetFontsAsync());
}
