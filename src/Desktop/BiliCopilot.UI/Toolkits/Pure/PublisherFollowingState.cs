// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Retains a card's last successful relationship while consuming shared account state.</summary>
public sealed class PublisherFollowingState
{
    private readonly UserRelationBatcher _relations;
    private readonly string _userId;
    private string? _account;
    private long _loadVersion;

    /// <summary>Records a feed observation once, when the card is created, rather than on every remount.</summary>
    public PublisherFollowingState(UserRelationBatcher relations, string? account, string userId, bool? feedFollowing)
    {
        _relations = relations;
        _account = account;
        _userId = userId;
        relations.SetAccount(account);
        if (account is not null && userId != account && feedFollowing.HasValue)
            relations.SetKnownFollowing(account, userId, feedFollowing.Value);
        Current = account is null || userId == account ? null : relations.GetKnownFollowing(account, userId);
    }

    /// <summary>Last successful state for this card's current account; null is unknown.</summary>
    public bool? Current { get; private set; }

    /// <summary>Refreshes expired state without losing a same-account success on eviction or network failure.</summary>
    public async Task<bool?> LoadAsync(string? account, CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        if (_account != account) { Current = null; _account = account; }
        _relations.SetAccount(account);
        if (account is null || account == _userId) return Current = null;
        Current = _relations.GetKnownFollowing(account, _userId) ?? Current;
        var following = await _relations.QueryAsync(account, _userId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (version == _loadVersion && _account == account) Current = following ?? Current;
        return Current;
    }
}
