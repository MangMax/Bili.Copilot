// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 字幕语言偏好的排序规则：中文非 AI > 中文 AI > 其他语言非 AI > 列表首个。
/// </summary>
public class SubtitlePreferenceTests
{
    [Fact]
    public void SelectPreferredIndex_ChineseNonAIWinsOverChineseAIAndEnglish()
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            ("English", true),
            ("中文（自动生成）", true),
            ("中文（简体）", false),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(2, index);
    }

    [Fact]
    public void SelectPreferredIndex_ChineseAIWinsOverEnglishNonAI()
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            ("English", false),
            ("中文（自动生成）", true),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectPreferredIndex_NoChineseFallsBackToFirstNonAI()
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            ("English", true),
            ("日本語", false),
            ("한국어", true),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectPreferredIndex_NoChineseAndNoNonAIFallsBackToFirst()
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            ("English", true),
            ("日本語", true),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(0, index);
    }

    [Fact]
    public void SelectPreferredIndex_EmptyListReturnsMinusOne()
    {
        var index = SubtitlePreference.SelectPreferredIndex([]);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void SelectPreferredIndex_NullOrBlankLanguageNamesDoNotCrash()
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            (null!, false),
            ("   ", false),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(0, index);
    }

    [Theory]
    [InlineData("中文（简体）")]
    [InlineData("简体中文")]
    [InlineData("简体")]
    [InlineData("繁体中文")]
    [InlineData("汉语")]
    [InlineData("Chinese (auto-generated)")]
    [InlineData("CHINESE")]
    [InlineData("zh-CN")]
    [InlineData("zh-Hans")]
    public void SelectPreferredIndex_RecognizesChineseLanguageNames(string chineseName)
    {
        var metas = new (string LanguageName, bool IsAI)[]
        {
            ("English", false),
            (chineseName, true),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas);

        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectPreferredIndex_GenericOverload_UsesProvidedPropertyAccessors()
    {
        var metas = new SubtitleMetaStub[]
        {
            new("English", true),
            new("中文（简体）", false),
        };

        var index = SubtitlePreference.SelectPreferredIndex(metas, p => p.LanguageName, p => p.IsAI);

        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectPreferredIndex_GenericOverload_EmptyListReturnsMinusOne()
    {
        var index = SubtitlePreference.SelectPreferredIndex<SubtitleMetaStub>([], p => p.LanguageName, p => p.IsAI);

        Assert.Equal(-1, index);
    }

    private sealed record SubtitleMetaStub(string? LanguageName, bool IsAI);
}
