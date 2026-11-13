// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.WinUI.Animations;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls;

public sealed partial class OverlayFrame : LayoutUserControlBase
{
    public OverlayFrame() => InitializeComponent();

    public List<object> GetBackStack()
        => [.. RootGrid.Children];

    public void ClearBackStack()
    {
        RootGrid.Children.Clear();
    }

    public void GoBack()
    {
        if (RootGrid.Children.Count > 0)
        {
            RootGrid.Children.RemoveAt(RootGrid.Children.Count - 1);
        }

        // 如果还有元素，则将最后一个元素设置为可见
        if (RootGrid.Children.Count > 0)
        {
            foreach (var child in RootGrid.Children)
            {
                child.Visibility = Visibility.Collapsed;
            }

            RootGrid.Children[^1].Visibility = Visibility.Visible;
        }
    }

    public void NavigateTo(Type pageType, object? parameter = null, bool animateIn = true)
    {
        if (pageType is null)
        {
            return;
        }

        // 同一导航只创建一对过渡，新建与复用的页面都重新应用，清空后也能恢复.
        var showSet = new ImplicitAnimationSet
        {
            new OpacityAnimation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                From = 0,
                To = 1,
            },
        };
        var hideSet = new ImplicitAnimationSet
        {
            new OpacityAnimation
            {
                Duration = TimeSpan.FromMilliseconds(100),
                From = 1,
                To = 0,
            },
        };
        UIElement? targetElement = null;

        // 查找是否已存在该类型的控件
        foreach (var item in RootGrid.Children)
        {
            if (item.GetType() == pageType)
            {
                targetElement = item;
                // 如果实现了 IMainFramePage，则调用 SetParameter
                if (item is IParameterPage mainFramePage)
                {
                    mainFramePage.SetParameter(parameter);
                }
            }
        }

        if (targetElement == null)
        {
            // 未找到，创建新实例
            var element = NavigationPageFactory.Create(pageType);
            targetElement = element;
            // 如果实现了 IMainFramePage，则调用 SetParameter
            if (element is IParameterPage mainFramePage)
            {
                mainFramePage.SetParameter(parameter);
            }

            RootGrid.Children.Add(element);
        }
        else if (RootGrid.Children.Count > 1)
        {
            // 将 targetElement 移动到最后
            RootGrid.Children.Move(Convert.ToUInt32(RootGrid.Children.IndexOf(targetElement)), Convert.ToUInt32(RootGrid.Children.Count - 1));
        }

        // 头像串联导航必须抑制叠层页自身的淡入，避免两套过渡同时播放；
        // 之后以默认参数返回同一页面时再恢复淡入。
        Implicit.SetShowAnimations(targetElement, animateIn ? showSet : null);
        Implicit.SetHideAnimations(targetElement, hideSet);

        // 设置可见性
        foreach (var child in RootGrid.Children)
        {
            child.Visibility = child == targetElement ? Visibility.Visible : Visibility.Collapsed;
            if (child.Visibility == Visibility.Collapsed && child is ICancelPageViewModel cancelPageViewModel)
            {
                cancelPageViewModel.CancelLoading();
            }
        }
    }

    public object? GetCurrentContent()
    {
        if (RootGrid.Children.Count > 0)
        {
            return RootGrid.Children[^1];
        }

        return default;
    }
}
