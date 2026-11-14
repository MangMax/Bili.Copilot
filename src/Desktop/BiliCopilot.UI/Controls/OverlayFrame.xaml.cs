// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.WinUI.Animations;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls;

public sealed partial class OverlayFrame : LayoutUserControlBase
{
    // 返回时把页面留在视觉树里，只把它从可见栈里摘掉：
    // 叠层页重建要几百毫秒，重新挂载也要几十毫秒，留着它头像串联动画才不会先停住再飞.
    private const int MaxCachedPages = 2;

    // 可见的返回栈；被返回的页面可能仍留在 RootGrid.Children 里等待复用，不算栈的一部分.
    private readonly List<UIElement> _backStack = new();

    public OverlayFrame() => InitializeComponent();

    public List<object> GetBackStack()
        => [.. _backStack];

    public void ClearBackStack()
    {
        _backStack.Clear();
        RootGrid.Children.Clear();
    }

    public void GoBack()
    {
        if (_backStack.Count > 0)
        {
            var removed = _backStack[^1];
            _backStack.RemoveAt(_backStack.Count - 1);
            removed.Visibility = Visibility.Collapsed;

            // 只有实现参数接口的页面能复用，其余页面照旧真正移除.
            if (removed is not IParameterPage)
            {
                RootGrid.Children.Remove(removed);
            }
            else
            {
                var index = RootGrid.Children.IndexOf(removed);
                if (index >= 0 && RootGrid.Children.Count > 1)
                {
                    // 挪到最后，缓存按最近的返回顺序保留.
                    RootGrid.Children.Move(Convert.ToUInt32(index), Convert.ToUInt32(RootGrid.Children.Count - 1));
                }

                TrimCachedPages();
            }
        }

        // 如果还有元素，则将最后一个元素设置为可见
        if (_backStack.Count > 0)
        {
            foreach (var child in _backStack)
            {
                child.Visibility = Visibility.Collapsed;
            }

            _backStack[^1].Visibility = Visibility.Visible;
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

        // 抑制淡入时仍要挂一个零时长显示动画：上一次隐藏动画会把不透明度停在 0，
        // 只把显示动画设为 null 的话，复用的页面会整页透明（元素在、画面是空的）.
        var instantSet = new ImplicitAnimationSet
        {
            new OpacityAnimation
            {
                Duration = TimeSpan.FromMilliseconds(1),
                From = 0,
                To = 1,
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
        else
        {
            if (RootGrid.Children.Count > 1)
            {
                // 将 targetElement 移动到最后
                RootGrid.Children.Move(Convert.ToUInt32(RootGrid.Children.IndexOf(targetElement)), Convert.ToUInt32(RootGrid.Children.Count - 1));
            }
        }

        // 维护逻辑返回栈：目标页可能来自栈中，也可能是返回时保留下来的页面.
        _backStack.Remove(targetElement);
        _backStack.Add(targetElement);

        // 头像串联导航必须抑制叠层页自身的淡入，避免两套过渡同时播放；
        // 之后以默认参数返回同一页面时再恢复淡入。
        Implicit.SetShowAnimations(targetElement, animateIn ? showSet : instantSet);
        Implicit.SetHideAnimations(targetElement, hideSet);

        // 设置可见性
        foreach (var child in _backStack)
        {
            child.Visibility = child == targetElement ? Visibility.Visible : Visibility.Collapsed;
            if (child != targetElement && child is ICancelPageViewModel cancelPageViewModel)
            {
                cancelPageViewModel.CancelLoading();
            }
        }
    }

    /// <summary>
    /// 限制保留的缓存页面数量，超出时从最旧的开始移除.
    /// </summary>
    private void TrimCachedPages()
    {
        var cached = 0;
        for (var i = RootGrid.Children.Count - 1; i >= 0; i--)
        {
            var child = RootGrid.Children[i];
            if (_backStack.Contains(child) || child is not IParameterPage)
            {
                continue;
            }

            cached++;
            if (cached > MaxCachedPages)
            {
                RootGrid.Children.RemoveAt(i);
            }
        }
    }

    public object? GetCurrentContent()
    {
        if (_backStack.Count > 0)
        {
            return _backStack[^1];
        }

        return default;
    }
}
