// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Authorization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using static BiliCopilot.UI.Toolkits.NativeSignInClient;

namespace BiliCopilot.UI.Accounts;

internal sealed record SavedAccount(long Uid, Guid LoginId, string? Name, BiliToken Token, Dictionary<string, string> Cookies,
    string? AvatarUrl = null, AppLoginDevice? AppDevice = null);

internal sealed record AccountFile(long? ActiveUid, SavedAccount[] Accounts);

[JsonSerializable(typeof(AccountFile))]
internal sealed partial class AccountJsonContext : JsonSerializerContext;

internal sealed class AccountStore
{
    private readonly SemaphoreSlim _writer = new(1);
    private AccountFile _state = new(null, []);
    private string _path = null!;

    public AccountFile Snapshot => Volatile.Read(ref _state);

    public async Task<bool> LoadAsync()
    {
        _path = Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path, "accounts.dat");
        if (!File.Exists(_path))
        {
            return false;
        }

        var bytes = await File.ReadAllBytesAsync(_path).ConfigureAwait(false);
        var buffer = await new DataProtectionProvider().UnprotectAsync(CryptographicBuffer.CreateFromByteArray(bytes));
        CryptographicBuffer.CopyToByteArray(buffer, out var plaintext);
        try
        {
            var state = JsonSerializer.Deserialize(plaintext, AccountJsonContext.Default.AccountFile)
                ?? throw new InvalidDataException("账户数据无法读取。");
            Volatile.Write(ref _state, state);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task SaveAsync(SavedAccount account, bool activate, CancellationToken token = default, AccountCredentials? credentials = null)
        => UpdateAsync(state => new(
            activate ? account.Uid : state.ActiveUid,
            [.. state.Accounts.Where(item => item.Uid != account.Uid), credentials is null ? account : WithCredentials(account, credentials)]), token);

    public Task SetActiveAsync(long? uid)
        => UpdateAsync(state => state.ActiveUid == uid ? state : state with { ActiveUid = uid }, default);

    public Task RemoveAsync(long uid) => UpdateAsync(state =>
    {
        var remaining = state.Accounts.Where(item => item.Uid != uid).ToArray();
        return new(state.ActiveUid == uid ? remaining.FirstOrDefault()?.Uid : state.ActiveUid, remaining);
    }, default);

    public Task SaveCredentialsAsync(long uid, Guid loginId, AccountCredentials credentials)
        => UpdateAccountAsync(uid, loginId, account => WithCredentials(account, credentials));

    public Task SaveProfileAsync(long uid, Guid loginId, string name, string? avatar)
        => UpdateAccountAsync(uid, loginId, account => account with { Name = name, AvatarUrl = avatar });

    private Task UpdateAccountAsync(long uid, Guid loginId, Func<SavedAccount, SavedAccount> update)
        => UpdateAsync(state =>
        {
            if (state.Accounts.FirstOrDefault(item => item.Uid == uid && item.LoginId == loginId) is not { } account)
            {
                return state;
            }

            var updated = update(account);
            return ReferenceEquals(account, updated)
                ? state
                : state with { Accounts = [.. state.Accounts.Select(item => item.Uid == uid ? updated : item)] };
        }, default);

    private static SavedAccount WithCredentials(SavedAccount account, AccountCredentials credentials)
    {
        var (token, cookies) = credentials.Snapshot();
        return token?.UserId == account.Uid ? account with { Token = token, Cookies = cookies } : account;
    }

    private async Task UpdateAsync(Func<AccountFile, AccountFile> update, CancellationToken token)
    {
        await _writer.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var previous = Snapshot;
            var next = update(previous);
            if (ReferenceEquals(previous, next))
            {
                return;
            }

            var plaintext = JsonSerializer.SerializeToUtf8Bytes(next, AccountJsonContext.Default.AccountFile);
            try
            {
                var buffer = await new DataProtectionProvider("LOCAL=user").ProtectAsync(CryptographicBuffer.CreateFromByteArray(plaintext));
                CryptographicBuffer.CopyToByteArray(buffer, out var encrypted);
                var temporary = _path + ".tmp";
                await File.WriteAllBytesAsync(temporary, encrypted, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, _path, overwrite: true);
                Volatile.Write(ref _state, next);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            _writer.Release();
        }
    }
}
