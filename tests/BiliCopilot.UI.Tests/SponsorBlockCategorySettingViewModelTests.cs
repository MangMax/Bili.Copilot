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

    private static SponsorBlockCategorySettingViewModel CreateSetting(
        SponsorBlockCategory category,
        SponsorBlockBehavior behavior,
        List<(SponsorBlockCategory Category, SponsorBlockBehavior Behavior)> writes)
        => new(category, behavior, category.ToString(), (changedCategory, changedBehavior) => writes.Add((changedCategory, changedBehavior)));
}
