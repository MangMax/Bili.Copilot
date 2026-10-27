// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockCategorySettingViewModelTests
{
    [Fact]
    public void Behavior_SetToPersistedValue_DoesNotWrite()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Sponsor, SponsorBlockBehavior.Skip, writes);

        setting.Behavior = SponsorBlockBehavior.Skip;

        Assert.Empty(writes);
    }

    [Fact]
    public void Behavior_SetToDifferentValue_WritesOnce()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Interaction, SponsorBlockBehavior.ShowButton, writes);

        setting.Behavior = SponsorBlockBehavior.Mute;

        var write = Assert.Single(writes);
        Assert.Equal(SponsorBlockCategory.Interaction, write.Category);
        Assert.Equal(SponsorBlockBehavior.Mute, write.Behavior);
    }

    [Fact]
    public void Behavior_SetToSameNewValueTwice_WritesOnlyOnFirstChange()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Outro, SponsorBlockBehavior.Skip, writes);

        setting.Behavior = SponsorBlockBehavior.Mute;
        setting.Behavior = SponsorBlockBehavior.Mute;

        Assert.Single(writes);
    }

    [Fact]
    public void Behavior_ChangeThenRevert_WritesBothTimes()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Intro, SponsorBlockBehavior.Skip, writes);

        setting.Behavior = SponsorBlockBehavior.Mute;
        setting.Behavior = SponsorBlockBehavior.Skip;

        Assert.Equal(2, writes.Count);
        Assert.Equal(SponsorBlockBehavior.Mute, writes[0].Behavior);
        Assert.Equal(SponsorBlockBehavior.Skip, writes[1].Behavior);
    }

    [Fact]
    public void ApplySelection_NullOrForeignValue_IsIgnored()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Preview, SponsorBlockBehavior.None, writes);

        setting.ApplySelection(null);
        setting.ApplySelection("not a behavior");
        setting.ApplySelection(42);

        Assert.Empty(writes);
        Assert.Equal(SponsorBlockBehavior.None, setting.Behavior);
    }

    [Fact]
    public void ApplySelection_SameValue_DoesNotWrite()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.PoiHighlight, SponsorBlockBehavior.ShowButton, writes);

        setting.ApplySelection(SponsorBlockBehavior.ShowButton);

        Assert.Empty(writes);
    }

    [Fact]
    public void ApplySelection_KnownValue_UpdatesAndWritesOnce()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Filler, SponsorBlockBehavior.None, writes);

        setting.ApplySelection(SponsorBlockBehavior.Skip);

        var write = Assert.Single(writes);
        Assert.Equal(SponsorBlockCategory.Filler, write.Category);
        Assert.Equal(SponsorBlockBehavior.Skip, write.Behavior);
        Assert.Equal(SponsorBlockBehavior.Skip, setting.Behavior);
    }

    [Fact]
    public void ApplySelection_DuringBindingRefresh_IsIgnored()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Padding, SponsorBlockBehavior.None, writes);

        setting.BeginBindingRefresh();
        try
        {
            // 模拟下拉框换绑到本类别时带出上一行的残留值.
            setting.ApplySelection(SponsorBlockBehavior.Skip);
        }
        finally
        {
            setting.EndBindingRefresh();
        }

        Assert.Empty(writes);
        Assert.Equal(SponsorBlockBehavior.None, setting.Behavior);
    }

    [Fact]
    public void ApplySelection_AfterBindingRefresh_IsApplied()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.Padding, SponsorBlockBehavior.None, writes);

        setting.BeginBindingRefresh();
        setting.EndBindingRefresh();
        setting.ApplySelection(SponsorBlockBehavior.Skip);

        var write = Assert.Single(writes);
        Assert.Equal(SponsorBlockBehavior.Skip, write.Behavior);
    }

    [Fact]
    public void EndBindingRefresh_WithoutBegin_DoesNotGoNegative()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();
        var setting = CreateSetting(SponsorBlockCategory.MusicOfftopic, SponsorBlockBehavior.None, writes);

        setting.EndBindingRefresh();
        setting.ApplySelection(SponsorBlockBehavior.Mute);

        Assert.Single(writes);
    }

    [Fact]
    public void Behavior_ChangeRaisesPropertyChangedOnce()
    {
        var setting = CreateSetting(SponsorBlockCategory.SelfPromo, SponsorBlockBehavior.Skip, []);
        var changes = new List<string?>();
        setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        setting.Behavior = SponsorBlockBehavior.Mute;
        setting.Behavior = SponsorBlockBehavior.Mute;

        Assert.Equal([nameof(SponsorBlockCategorySettingViewModel.Behavior)], changes);
    }

    [Fact]
    public void DefaultArgsConstructor_KeepsDocumentedDefaults()
    {
        var writes = new List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)>();

        foreach (var descriptor in SponsorBlockCategoryCatalog.All)
        {
            var setting = CreateSetting(descriptor.Category, descriptor.DefaultBehavior, writes);

            // 首次打开设置页时，下拉框把已保存的值回绑到数据项，不应该产生写入.
            setting.ApplySelection(setting.Behavior);
            Assert.Equal(descriptor.DefaultBehavior, setting.Behavior);
        }

        Assert.Empty(writes);
    }

    [Fact]
    public void Constructor_WithCustomColor_ReportsCustomColor()
    {
        var setting = CreateColorSetting(SponsorBlockCategory.Interaction, 0xFF3366CC);

        Assert.Equal(0xFF3366CCu, setting.CustomColor);
        Assert.True(setting.HasCustomColor);
        Assert.False(setting.IsUsingDefaultColor);
    }

    [Fact]
    public void Constructor_WithoutCustomColor_UsesDefault()
    {
        var setting = CreateColorSetting(SponsorBlockCategory.Interaction);

        Assert.Null(setting.CustomColor);
        Assert.False(setting.HasCustomColor);
        Assert.True(setting.IsUsingDefaultColor);
    }

    [Fact]
    public void SetCustomColor_StoresValueRaisesPropertiesAndWritesOnce()
    {
        var writes = new List<(SponsorBlockCategory Category, uint Color)>();
        var setting = CreateColorSetting(SponsorBlockCategory.Interaction, colorWrites: writes);
        var changes = new List<string?>();
        setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        setting.SetCustomColor(0xFF112233);

        var write = Assert.Single(writes);
        Assert.Equal(SponsorBlockCategory.Interaction, write.Category);
        Assert.Equal(0xFF112233u, write.Color);
        Assert.Equal(0xFF112233u, setting.CustomColor);
        Assert.True(setting.HasCustomColor);
        Assert.Equal(
            [
                nameof(SponsorBlockCategorySettingViewModel.CustomColor),
                nameof(SponsorBlockCategorySettingViewModel.HasCustomColor),
                nameof(SponsorBlockCategorySettingViewModel.IsUsingDefaultColor),
            ],
            changes);
    }

    [Fact]
    public void SetCustomColor_SameValue_DoesNotWriteOrRaise()
    {
        var writes = new List<(SponsorBlockCategory Category, uint Color)>();
        var setting = CreateColorSetting(SponsorBlockCategory.Interaction, 0xFF112233, writes);
        var changes = new List<string?>();
        setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        setting.SetCustomColor(0xFF112233);

        Assert.Empty(writes);
        Assert.Empty(changes);
    }

    [Fact]
    public void SetCustomColor_ChangeThenRevert_WritesBothTimes()
    {
        var writes = new List<(SponsorBlockCategory Category, uint Color)>();
        var setting = CreateColorSetting(SponsorBlockCategory.Interaction, 0xFF112233, writes);

        setting.SetCustomColor(0xFF445566);
        setting.SetCustomColor(0xFF112233);

        Assert.Equal(2, writes.Count);
        Assert.Equal(0xFF445566u, writes[0].Color);
        Assert.Equal(0xFF112233u, writes[1].Color);
    }

    [Fact]
    public void ResetColor_FromCustom_InvokesResetOnceAndRaisesProperties()
    {
        var resets = new List<SponsorBlockCategory>();
        var setting = CreateColorSetting(SponsorBlockCategory.Outro, 0xFF3366CC, resetWrites: resets);
        var changes = new List<string?>();
        setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        setting.ResetColor();

        Assert.Equal([SponsorBlockCategory.Outro], resets);
        Assert.Null(setting.CustomColor);
        Assert.False(setting.HasCustomColor);
        Assert.True(setting.IsUsingDefaultColor);
        Assert.Equal(
            [
                nameof(SponsorBlockCategorySettingViewModel.CustomColor),
                nameof(SponsorBlockCategorySettingViewModel.HasCustomColor),
                nameof(SponsorBlockCategorySettingViewModel.IsUsingDefaultColor),
            ],
            changes);
    }

    [Fact]
    public void ResetColor_WhenDefault_IsNoOp()
    {
        var resets = new List<SponsorBlockCategory>();
        var setting = CreateColorSetting(SponsorBlockCategory.Outro, resetWrites: resets);
        var changes = new List<string?>();
        setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        setting.ResetColor();

        Assert.Empty(resets);
        Assert.Empty(changes);
    }

    [Fact]
    public void ColorAutomationIds_FollowNamingConvention()
    {
        var setting = CreateColorSetting(SponsorBlockCategory.PoiHighlight);

        Assert.Equal("SponsorBlockColor_PoiHighlight", setting.ColorAutomationId);
        Assert.Equal("SponsorBlockColorReset_PoiHighlight", setting.ColorResetAutomationId);
    }

    private static SponsorBlockCategorySettingViewModel CreateColorSetting(
        SponsorBlockCategory category,
        uint? customColor = null,
        List<(SponsorBlockCategory Category, uint Color)>? colorWrites = null,
        List<SponsorBlockCategory>? resetWrites = null)
        => new(
            category,
            SponsorBlockBehavior.None,
            category.ToString(),
            (_, _) => { },
            customColor,
            colorWrites is null ? null : (changedCategory, color) => colorWrites.Add((changedCategory, color)),
            resetWrites is null ? null : changedCategory => resetWrites.Add(changedCategory));

    private static SponsorBlockCategorySettingViewModel CreateSetting(
        SponsorBlockCategory category,
        SponsorBlockBehavior behavior,
        List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)> writes)
        => new(category, behavior, category.ToString(), (changedCategory, changedBehavior) => writes.Add((changedCategory, changedBehavior)));
}
