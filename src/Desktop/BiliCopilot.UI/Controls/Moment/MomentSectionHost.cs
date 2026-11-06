// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 缓存动态分区控件，保留卡片与滚动位置.
/// </summary>
public sealed partial class MomentSectionHost : Grid
{
    private readonly Dictionary<IMomentSectionDetailViewModel, FrameworkElement> _sections = new();
    private readonly UISettings _uiSettings = new();

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
        if (section is not null && animate && _uiSettings.AnimationsEnabled)
        {
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(200);
            animation.InsertKeyFrame(0, 0.65f);
            animation.InsertKeyFrame(1, 1);
            visual.StartAnimation(nameof(visual.Opacity), animation);

            var incoming = ElementCompositionPreview.GetElementVisual(_sections[section]);
            var slide = visual.Compositor.CreateVector3KeyFrameAnimation();
            slide.Duration = TimeSpan.FromMilliseconds(280);
            var direction = section is VideoMomentSectionDetailViewModel ? -1 : 1;
            slide.InsertKeyFrame(0, new Vector3(direction * 40, 0, 0));
            slide.InsertKeyFrame(1, Vector3.Zero,
                visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0), new Vector2(0, 1)));
            incoming.StartAnimation("Translation", slide);
        }
    }
}