// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 一次待播放的头像串联请求.
/// </summary>
/// <param name="UserId">目标用户 Id.</param>
/// <param name="AvatarUri">准备动画时卡片上显示的头像地址.</param>
/// <param name="PreparedAt">准备动画的时间.</param>
public sealed record AvatarTransitionRequest(string UserId, Uri AvatarUri, DateTimeOffset PreparedAt);

/// <summary>
/// 判断何时可以准备、启动或放弃头像串联动画.
/// </summary>
/// <remarks>
/// 这里只做纯决策，不接触 WinUI：卡片、页头与叠层页适配器共用同一套规则，
/// 动画失败或超时时导航必须照常完成。
/// </remarks>
public static class AvatarTransitionPolicy
{
    /// <summary>
    /// 准备到启动的最大允许时间。超过后目标即使出现也不再播放，
    /// 避免悬停快照长期留在界面上.
    /// </summary>
    public static TimeSpan MaxPrepareAge { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 判断点击卡片时是否值得准备动画.
    /// </summary>
    /// <param name="animationsEnabled">系统动画是否开启.</param>
    /// <param name="sourceVisible">源头像是否已完成布局并可见.</param>
    /// <param name="sourceHasImage">源头像是否已经显示了一张图片.</param>
    /// <param name="canOpenUserSpace">卡片是否提供可用的作者入口.</param>
    /// <param name="userId">作者用户 Id.</param>
    /// <param name="avatarUri">作者头像地址.</param>
    /// <returns>可以准备时为 <see langword="true"/>.</returns>
    public static bool CanPrepare(
        bool animationsEnabled,
        bool sourceVisible,
        bool sourceHasImage,
        bool canOpenUserSpace,
        string? userId,
        Uri? avatarUri)
        => animationsEnabled
            && sourceVisible
            && sourceHasImage
            && canOpenUserSpace
            && !string.IsNullOrWhiteSpace(userId)
            && avatarUri is not null;

    /// <summary>
    /// 判断请求是否指向同一个用户与同一张头像.
    /// </summary>
    /// <param name="request">准备阶段记录的请求.</param>
    /// <param name="destinationUserId">目标页当前展示的用户 Id.</param>
    /// <param name="destinationAvatarUri">目标页当前使用的头像地址.</param>
    /// <returns>用户与头像都相同时为 <see langword="true"/>.</returns>
    public static bool Matches(
        AvatarTransitionRequest? request,
        string? destinationUserId,
        Uri? destinationAvatarUri)
        => request is not null
            && string.Equals(request.UserId, destinationUserId, StringComparison.Ordinal)
            && request.AvatarUri == destinationAvatarUri;

    /// <summary>
    /// 判断请求是否仍在新旧预算内.
    /// </summary>
    /// <param name="request">准备阶段记录的请求.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>请求存在且未超过 <see cref="MaxPrepareAge"/> 时为 <see langword="true"/>.</returns>
    public static bool IsFresh(AvatarTransitionRequest? request, DateTimeOffset now)
        => request is not null
            && now >= request.PreparedAt
            && now - request.PreparedAt <= MaxPrepareAge;

    /// <summary>
    /// 判断目标页出现时是否还值得启动动画.
    /// </summary>
    /// <param name="request">准备阶段记录的请求.</param>
    /// <param name="destinationUserId">目标页当前展示的用户 Id.</param>
    /// <param name="destinationAvatarUri">目标页当前使用的头像地址.</param>
    /// <param name="age">从准备到现在的耗时.</param>
    /// <returns>目标匹配且未超时时为 <see langword="true"/>.</returns>
    public static bool CanStart(
        AvatarTransitionRequest? request,
        string? destinationUserId,
        Uri? destinationAvatarUri,
        TimeSpan age)
        => Matches(request, destinationUserId, destinationAvatarUri)
            && age >= TimeSpan.Zero
            && age <= MaxPrepareAge;

    /// <summary>
    /// 按当前时刻判断目标页出现时是否还值得启动动画.
    /// </summary>
    /// <param name="request">准备阶段记录的请求.</param>
    /// <param name="destinationUserId">目标页当前展示的用户 Id.</param>
    /// <param name="destinationAvatarUri">目标页当前使用的头像地址.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>目标匹配且未超时时为 <see langword="true"/>.</returns>
    public static bool CanStart(
        AvatarTransitionRequest? request,
        string? destinationUserId,
        Uri? destinationAvatarUri,
        DateTimeOffset now)
        => Matches(request, destinationUserId, destinationAvatarUri)
            && IsFresh(request, now);
}

/// <summary>
/// 当前唯一未完成的头像串联请求.
/// </summary>
/// <remarks>
/// 叠层页淡入抑制需要在不引用 WinUI 的导航视图模型里读取它；
/// 同一时刻最多只保留一个请求，新的准备必须先取消旧的动画.
/// </remarks>
public static class AvatarTransitionSession
{
    private static AvatarTransitionRequest? _current;

    /// <summary>
    /// 待启动的请求；没有待处理动画或请求已经过期时为 <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// 目标页可能始终没有出现。过期请求在读取时自行释放，避免它永久抑制叠层页淡入.
    /// </remarks>
    public static AvatarTransitionRequest? Current => CurrentAt(DateTimeOffset.Now);

    /// <summary>
    /// 按指定时刻读取待启动请求，过期时同时释放请求.
    /// </summary>
    /// <param name="now">当前时间.</param>
    /// <returns>仍在预算内的请求；没有请求或已经过期时为 <see langword="null"/>.</returns>
    public static AvatarTransitionRequest? CurrentAt(DateTimeOffset now)
    {
        var request = _current;
        if (request is null)
        {
            return null;
        }

        if (!AvatarTransitionPolicy.IsFresh(request, now))
        {
            _current = null;
            return null;
        }

        return request;
    }

    /// <summary>
    /// 记录新的待启动请求，替换之前的请求.
    /// </summary>
    /// <param name="userId">目标用户 Id.</param>
    /// <param name="avatarUri">目标头像地址.</param>
    /// <param name="now">准备动画的时间.</param>
    public static void Prepare(string userId, Uri avatarUri, DateTimeOffset now)
        => _current = new AvatarTransitionRequest(userId, avatarUri, now);

    /// <summary>
    /// 释放待启动请求.
    /// </summary>
    public static void Clear() => _current = null;
}
