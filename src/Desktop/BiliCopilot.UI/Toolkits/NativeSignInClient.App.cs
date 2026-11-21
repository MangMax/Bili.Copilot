// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Authorization;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace BiliCopilot.UI.Toolkits;

internal sealed partial class NativeSignInClient
{
    private const string AppKey = "dfca71928277209b";
    private const string AppSigningKey = "b5475a8825547a4fc26c7d518eaaa02e";
    private const string AppUserAgent = "Mozilla/5.0 BiliDroid/2.0.1 os/android model/android_hd mobi_app/android_hd build/2001100 channel/master innerVer/2001100 osVer/15 network/2";

    public async Task<AppSignInResult> SignInWithPasswordAsync(string account, string password, CaptchaChallenge? challenge, CaptchaProof? proof, CancellationToken token)
    {
        var key = await RequestAsync("/x/passport-login/web/key", null, token);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(ReadString(key, "key"));
        var form = CreateDeviceForm(rsa);
        form["username"] = account;
        form["password"] = Encrypt(rsa, ReadString(key, "hash") + password);
        form["permission"] = "ALL";
        form["from_pv"] = "main.homepage.avatar-nologin.all.click";
        form["from_url"] = Uri.EscapeDataString("bilibili://pegasus/promo");
        if (challenge is not null && proof is not null)
        {
            AddProof(form, challenge, proof);
        }

        var data = await RequestAsync("/x/passport-login/oauth2/login", form, token, appRequest: true);
        if (ReadNumber(data, "status") == 2 && ParsePhoneVerification(ReadText(data, "url")) is { } verification)
        {
            throw new SignInException("B 站要求验证账号绑定的手机号。") { PhoneVerification = verification };
        }

        return ReadCredentials(data);
    }

    public async Task<string> SendCodeAsync(string dialingCode, string phone, CaptchaChallenge challenge, CaptchaProof proof, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var form = CreateAppForm(now);
        form["buvid"] = _device.Buvid;
        form["local_id"] = _device.Buvid;
        form["login_session_id"] = Md5Hex(Encoding.UTF8.GetBytes(_device.Buvid + now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)));
        form["cid"] = dialingCode;
        form["tel"] = phone;
        AddProof(form, challenge, proof);

        JsonElement data;
        try
        {
            data = await RequestAsync("/x/passport-login/sms/send", form, token, appRequest: true);
        }
        catch (SignInException ex) when (ex.BusinessCode == -105 && ex.Captcha is null)
        {
            throw new SignInException(ex.Message, ex) { Captcha = await GetSafeCenterCaptchaAsync(token), BusinessCode = ex.BusinessCode };
        }

        var url = ReadText(data, "recaptcha_url");
        var captchaKey = ReadText(data, "captcha_key");
        if (url is null && captchaKey is not null)
        {
            return captchaKey;
        }

        throw new SignInException("请完成 B 站安全验证。") { Captcha = ParseCaptchaUrl(url) ?? await GetSafeCenterCaptchaAsync(token) };
    }

    public async Task<AppSignInResult> SignInWithSmsAsync(string dialingCode, string phone, string code, string captchaKey, CancellationToken token)
    {
        var key = await RequestAsync("/x/passport-login/web/key", null, token);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(ReadString(key, "key"));
        var form = CreateDeviceForm(rsa);
        form["cid"] = dialingCode;
        form["tel"] = phone;
        form["code"] = code;
        form["captcha_key"] = captchaKey;
        form["from_pv"] = "main.my-information.my-login.0.click";
        form["from_url"] = Uri.EscapeDataString("bilibili://user_center/mine");
        return ReadCredentials(await RequestAsync("/x/passport-login/login/sms", form, token, appRequest: true));
    }

    public async Task<string?> GetVerificationPhoneAsync(PhoneVerification verification, CancellationToken token)
    {
        var data = await RequestAsync("/x/safecenter/user/info?tmp_code=" + Uri.EscapeDataString(verification.Token),
            null, token, appRequest: true);
        return ReadText(ReadProperty(data, "account_info"), "hide_tel");
    }

    public async Task<CaptchaChallenge> GetSafeCenterCaptchaAsync(CancellationToken token)
    {
        var data = await RequestAsync("/x/safecenter/captcha/pre", null, token, appRequest: true, emptyPost: true);
        return new(ReadString(data, "recaptcha_token"), ReadString(data, "gee_gt"), ReadString(data, "gee_challenge"));
    }

    public async Task<string> SendVerificationCodeAsync(PhoneVerification verification, CaptchaChallenge challenge, CaptchaProof proof, CancellationToken token)
    {
        Dictionary<string, string> form = new()
        {
            ["tmp_code"] = verification.Token,
            ["disable_rcmd"] = "0",
            ["sms_type"] = "loginTelCheck",
        };
        AddProof(form, challenge, proof);
        var data = await RequestAsync("/x/safecenter/common/sms/send", form, token, appRequest: true, referer: verification.Url);
        return ReadString(data, "captcha_key");
    }

    public async Task<string> VerifyPhoneAsync(PhoneVerification verification, string captchaKey, string code, CancellationToken token)
    {
        var data = await RequestAsync("/x/safecenter/login/tel/verify", new()
        {
            ["tmp_code"] = verification.Token,
            ["request_id"] = verification.RequestId,
            ["source"] = verification.Source,
            ["type"] = "loginTelCheck",
            ["captcha_key"] = captchaKey,
            ["code"] = code,
        }, token, appRequest: true, referer: verification.Url);
        return ReadString(data, "code");
    }

    public async Task<AppSignInResult> ExchangePhoneVerificationAsync(string code, CancellationToken token)
    {
        Dictionary<string, string> form = new()
        {
            ["build"] = "2001100",
            ["buvid"] = _device.Buvid,
            ["code"] = code,
            ["disable_rcmd"] = "0",
            ["grant_type"] = "authorization_code",
            ["local_id"] = _device.Buvid,
            ["mobi_app"] = "android_hd",
            ["platform"] = "android",
            ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        };
        return ReadCredentials(await RequestAsync("/x/passport-login/oauth2/access_token", form, token, appRequest: true));
    }

    public async Task<AppSignInResult> RefreshAsync(BiliToken current, Dictionary<string, string> cookies, CancellationToken token)
    {
        var form = CreateAppForm(DateTimeOffset.UtcNow);
        foreach (var name in new[] { "SESSDATA", "bili_jct", "DedeUserID", "DedeUserID__ckMd5", "sid" })
        {
            if (cookies.TryGetValue(name, out var value))
            {
                form[name] = value;
            }
        }

        form["buvid"] = _device.Buvid;
        form["local_id"] = _device.Buvid;
        form["access_key"] = form["access_token"] = current.AccessToken!;
        form["refresh_token"] = current.RefreshToken!;
        var data = await RequestAsync("/x/passport-login/oauth2/refresh_token", form, token, appRequest: true);
        return ReadCredentials(data, cookies, current.UserId);
    }

    private AppSignInResult ReadCredentials(JsonElement data, Dictionary<string, string>? priorCookies = null, long previousUid = 0)
    {
        if (ReadNumber(data, "status") is not 0)
        {
            throw new SignInException("B 站要求额外验证，请通过 B 站安全中心完成后重试。");
        }

        var cookies = priorCookies is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new(priorCookies, StringComparer.Ordinal);
        foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://www.bilibili.com/")))
        {
            cookies[cookie.Name] = cookie.Value;
        }

        var entries = ReadProperty(ReadProperty(data, "cookie_info"), "cookies");
        if (entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (ReadText(entry, "name") is { } name && ReadText(entry, "value") is { } value)
                {
                    cookies[name] = value;
                }
            }
        }

        var payload = ReadProperty(data, "token_info");
        if (payload.ValueKind != JsonValueKind.Object)
        {
            payload = data;
        }

        var uid = ReadNumber(payload, "mid");
        if (cookies.TryGetValue("DedeUserID", out var cookieUid))
        {
            if (!long.TryParse(cookieUid, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedUid)
                || (uid != 0 && uid != parsedUid))
            {
                throw new SignInException("B 站返回的登录凭据属于不同账号，请重试。");
            }

            uid = parsedUid;
        }

        var access = ReadText(payload, "access_token");
        var refresh = ReadText(payload, "refresh_token");
        var lifetime = ReadNumber(payload, "expires_in");
        if (uid <= 0 || (previousUid > 0 && uid != previousUid) || lifetime <= 0 || lifetime > int.MaxValue
            || access is null || refresh is null || !cookies.TryGetValue("SESSDATA", out var session) || string.IsNullOrWhiteSpace(session))
        {
            throw new SignInException("B 站返回的登录凭据不完整，请重试。");
        }

        return new(new BiliToken
        {
            UserId = uid,
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresIn = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + lifetime,
        }, cookies, _device);
    }

    private static Dictionary<string, string> CreateAppForm(DateTimeOffset now) => new()
    {
        ["build"] = "2001100",
        ["channel"] = "master",
        ["c_locale"] = "zh_CN",
        ["s_locale"] = "zh_CN",
        ["disable_rcmd"] = "0",
        ["mobi_app"] = "android_hd",
        ["platform"] = "android",
        ["statistics"] = """{"appId":5,"platform":3,"version":"2.0.1","abtest":""}""",
        ["ts"] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
    };

    private Dictionary<string, string> CreateDeviceForm(RSA rsa)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var form = CreateAppForm(DateTimeOffset.UtcNow);
        form["device"] = "phone";
        form["device_name"] = "vivo";
        form["device_platform"] = "Android14vivo";
        form["bili_local_id"] = form["device_id"] = _device.DeviceId;
        form["buvid"] = form["local_id"] = _device.Buvid;
        form["dt"] = Uri.EscapeDataString(Encrypt(rsa, RandomNumberGenerator.GetString(alphabet, 16)));
        return form;
    }

    private static void AddProof(Dictionary<string, string> form, CaptchaChallenge challenge, CaptchaProof proof)
    {
        form["recaptcha_token"] = challenge.Token;
        form["gee_challenge"] = proof.Challenge;
        form["gee_validate"] = proof.Validate;
        form["gee_seccode"] = proof.Seccode;
    }

    private static void SignAppForm(Dictionary<string, string> form)
    {
        form["appkey"] = AppKey;
        var canonical = string.Join("&", form.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{EncodeSignatureValue(pair.Key)}={EncodeSignatureValue(pair.Value)}"));
        form["sign"] = Md5Hex(Encoding.UTF8.GetBytes(canonical + AppSigningKey));
    }

    // Passport's signing format uses form encoding with spaces written as %20.
    private static string EncodeSignatureValue(string value)
        => Uri.EscapeDataString(value).Replace("%2A", "*", StringComparison.Ordinal).Replace("~", "%7E", StringComparison.Ordinal);

    private static string Encrypt(RSA rsa, string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        try
        {
            return Convert.ToBase64String(rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    // MD5 is part of the Passport request format and is also used to format random identifiers.
    private static string Md5Hex(byte[] value) => Convert.ToHexStringLower(MD5.HashData(value));

    private static AppLoginDevice CreateLoginDevice()
    {
        var digest = Md5Hex(RandomNumberGenerator.GetBytes(16));
        var bytes = RandomNumberGenerator.GetBytes(31);
        var now = DateTime.Now;
        int[] parts = [now.Year / 100, now.Year % 100, now.Month, now.Day, now.Hour, now.Minute, now.Second];
        for (var i = 0; i < parts.Length; i++)
        {
            bytes[16 + i] = (byte)((parts[i] / 10 << 4) | parts[i] % 10);
        }

        var checksum = (bytes.Sum(value => (int)value) & 0xff).ToString("x2", CultureInfo.InvariantCulture);
        return new($"XY{digest[2]}{digest[12]}{digest[22]}{digest}", Md5Hex(bytes) + checksum);
    }

    private static CaptchaChallenge? ParseCaptchaUrl(string? url)
    {
        var fields = ReadPassportQuery(url, out _);
        return fields.TryGetValue("recaptcha_token", out var token) && fields.TryGetValue("gee_gt", out var gt)
            && fields.TryGetValue("gee_challenge", out var challenge) ? new(token, gt, challenge) : null;
    }

    private static PhoneVerification? ParsePhoneVerification(string? url)
    {
        var fields = ReadPassportQuery(url, out var uri);
        return uri is not null && fields.TryGetValue("tmp_token", out var token) && fields.TryGetValue("request_id", out var requestId)
            ? new(token, requestId, fields.GetValueOrDefault("source", "risk"), uri) : null;
    }

    private static Dictionary<string, string> ReadPassportQuery(string? url, out Uri? uri)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal);
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0
            || (uri.Host != "bilibili.com" && !uri.Host.EndsWith(".bilibili.com", StringComparison.Ordinal)))
        {
            uri = null;
            return parameters;
        }

        foreach (var entry in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = entry.Split('=', 2);
            if (pair.Length == 2 && WebUtility.UrlDecode(pair[1]) is { Length: > 0 } value)
            {
                parameters[WebUtility.UrlDecode(pair[0])] = value;
            }
        }

        return parameters;
    }

    private static JsonElement ReadProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? ReadText(JsonElement element, string name)
        => ReadProperty(element, name) is { ValueKind: JsonValueKind.String } value
            && value.GetString() is { Length: > 0 } text ? text : null;

    private static long ReadNumber(JsonElement element, string name)
    {
        var value = ReadProperty(element, name);
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) => number,
            _ => 0,
        };
    }

    internal sealed record AppLoginDevice(string Buvid, string DeviceId);

    internal sealed record AppSignInResult(BiliToken Token, Dictionary<string, string> Cookies, AppLoginDevice Device);

    internal sealed record PhoneVerification(string Token, string RequestId, string Source, Uri Url);
}
