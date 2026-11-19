// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 缓存动态分区控件，保留卡片与滚动位置.
/// </summary>
public sealed partial class MomentSectionHost : Grid
{
    private readonly Dictionary<IMomentSectionDetailViewModel, FrameworkElement> _sections = new();

    public static readonly DependencyProperty SelectedSectionProperty = DependencyProperty.Register(
        nameof(SelectedSection), typeof(IMomentSectionDetailViewModel), typeof(MomentSectionHost),
        new PropertyMetadata(null, OnSelectedSectionChanged));

    public IMomentSectionDetailViewModel? SelectedSection
    {
        get => (IMomentSectionDetailViewModel?)GetValue(SelectedSectionProperty);
        set => SetValue(SelectedSectionProperty, value);
    }

    private static void OnSelectedSectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((MomentSectionHost)sender).ShowSection(args.NewValue as IMomentSectionDetailViewModel);

    private void ShowSection(IMomentSectionDetailViewModel? section)
    {
        var animate = IsLoaded && _sections.Count > 0;
        foreach (var entry in _sections)
        {
            var isSelected = entry.Key == section;
            // 子元素的隐式隐藏动画可能继续绘制已折叠分区，先清除整体可见度以免叠影。
            entry.Value.Opacity = isSelected ? 1 : 0;
            entry.Value.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            var sectionVisual = ElementCompositionPreview.GetElementVisual(entry.Value);
            sectionVisual.StopAnimation("Translation");
            sectionVisual.Properties.InsertVector3("Translation", Vector3.Zero);
        }

        if (section is not null && !_sections.ContainsKey(section))
        {
            FrameworkElement control = section switch
            {
                VideoMomentSectionDetailViewModel video => new VideoMomentSectionDetailControl { ViewModel = video },
                ComprehensiveMomentSectionDetailViewModel comprehensive => new ComprehensiveMomentSectionDetailControl { ViewModel = comprehensive },
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
            ElementCompositionPreview.SetIsTranslationEnabled(control, true);
            _sections.Add(section, control);
            Children.Add(control);
        }

        var visual = ElementCompositionPreview.GetElementVisual(this);
        visual.Clip ??= visual.Compositor.CreateInsetClip();
        visual.StopAnimation(nameof(visual.Opacity));
        visual.Opacity = 1;

        // 与其它标签栏共用同一套标签过渡：方向由 MomentHeader 的标签栏发布.
        // 外框只淡入，滑动的始终是刚显示的分区，和原来的分区切换手感一致.
        if (section is not null && animate)
        {
            PageTransition.SetContent(this, _sections[section]);
            PageTransition.Play(this, PageTransitionKind.Tab);
        }
    }
}