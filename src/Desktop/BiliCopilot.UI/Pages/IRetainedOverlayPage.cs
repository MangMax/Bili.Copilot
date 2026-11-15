// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Pages;

/// <summary>
/// 关闭后仍保留在叠层里的页面.
/// </summary>
/// <remarks>
/// 重建代价高、且能安全重置状态的页面（如带串联动画的用户空间、番剧详情）才实现该接口；
/// 其他页面返回后立即释放，下次进入重新构造.
/// </remarks>
public interface IRetainedOverlayPage
{
    /// <summary>
    /// 关闭后是否保留页面实例；默认保留.
    /// </summary>
    bool RetainOnClose => true;
}
