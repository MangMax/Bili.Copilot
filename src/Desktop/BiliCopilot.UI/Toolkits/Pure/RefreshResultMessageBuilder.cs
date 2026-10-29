// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 刷新结果提示文案的组装工具.
/// </summary>
/// <remarks>
/// 提示文案同时承载「本次新增了多少条」与「撤销刷新」入口。
/// 增量刷新没有取到新视频时仍然显示提示（否则用户会失去撤销入口），
/// 只是改用不含「新增」字样的普通刷新文案。
/// </remarks>
public static class RefreshResultMessageBuilder
{
    /// <summary>
    /// 组装刷新结果提示文案.
    /// </summary>
    /// <param name="isIncremental">本次刷新是否为增量刷新.</param>
    /// <param name="addedCount">本次刷新新增的条目数，仅在增量刷新时有意义.</param>
    /// <param name="addedTemplate">带 <c>{0}</c> 占位符的增量刷新文案模板.</param>
    /// <param name="reloadedTemplate">普通（清空重载）刷新文案.</param>
    /// <returns>可直接展示的提示文案.</returns>
    public static string Build(
        bool isIncremental,
        int addedCount,
        string addedTemplate,
        string reloadedTemplate)
    {
        ArgumentNullException.ThrowIfNull(addedTemplate);
        ArgumentNullException.ThrowIfNull(reloadedTemplate);
        ArgumentOutOfRangeException.ThrowIfNegative(addedCount);

        if (!isIncremental || addedCount == 0)
        {
            return reloadedTemplate;
        }

        return string.Format(addedTemplate, addedCount);
    }
}
