// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Text.Json;

namespace BiliCopilot.UI.Toolkits;

internal sealed partial class NativeSignInClient : IDisposable
{
    private static readonly Uri Passport = new("https://passport.bilibili.com");
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly AppLoginDevice _device;

    public NativeSignInClient(AppLoginDevice? device = null, HttpMessageHandler? handler = null)
    {
        _device = device ?? CreateLoginDevice();
        _http = new HttpClient(handler ?? new HttpClientHandler { CookieContainer = _cookies, AllowAutoRedirect = false })
        {
            BaseAddress = Passport,
            Timeout = TimeSpan.FromSeconds(20),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/142.0.0.0 Safari/537.36");
    }

    public async Task<CaptchaChallenge> GetCaptchaAsync(CancellationToken cancellationToken)
    {
        var data = await RequestAsync("/x/passport-login/captcha?source=main_web", null, cancellationToken);
        if (ReadString(data, "type") != "geetest")
        {
            throw new SignInException("当前验证方式请通过网页登录完成。");
        }

        var geetest = data.GetProperty("geetest");
        return new(ReadString(data, "token"), ReadString(geetest, "gt"), ReadString(geetest, "challenge"));
    }

    public async Task<IReadOnlyList<PhoneRegion>> GetRegionsAsync(CancellationToken cancellationToken)
    {
        var data = await RequestAsync("/x/passport-login/web/country", null, cancellationToken);
        return data.GetProperty("list").EnumerateArray()
            .Select(item => new PhoneRegion(ReadString(item, "country_code"), ReadString(item, "cname")))
            .ToArray();
    }

    public void Dispose() => _http.Dispose();

    private async Task<JsonElement> RequestAsync(string path, Dictionary<string, string>? parameters, CancellationToken cancellationToken,
        bool appRequest = false, Uri? referer = null, bool emptyPost = false)
    {
        using var request = new HttpRequestMessage(parameters is null && !emptyPost ? HttpMethod.Get : HttpMethod.Post, path);
        if (parameters is not null)
        {
            if (appRequest)
            {
                SignAppForm(parameters);
            }

            request.Content = new FormUrlEncodedContent(parameters);
        }
        else if (emptyPost)
        {
            request.Content = new FormUrlEncodedContent([]);
        }

        if (appRequest)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", AppUserAgent);
            request.Headers.Add("app-key", "android_hd");
            request.Headers.Add("buvid", _device.Buvid);
            request.Headers.Add("bili-http-engine", "cronet");
            request.Headers.Add("env", "prod");
            if (request.Content?.Headers.ContentType is { } contentType)
            {
                contentType.CharSet = "utf-8";
            }
        }
        else
        {
            request.Headers.Add("Origin", Passport.GetLeftPart(UriPartial.Authority));
            referer ??= new Uri(Passport, "/login");
        }

        request.Headers.Referrer = referer;
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var code = root.GetProperty("code").GetInt32();
        var data = ReadProperty(root, "data");
        if (code != 0)
        {
            if (code == -105 && (ParseCaptchaUrl(ReadText(data, "recaptcha_url")) ?? ParseCaptchaUrl(ReadText(data, "url"))) is { } challenge)
            {
                throw new SignInException("请完成 B 站安全验证。") { Captcha = challenge, BusinessCode = code };
            }

            throw new SignInException(ReadText(root, "message"))
            {
                BusinessCode = code,
            };
        }

        return data.Clone();
    }

    private static string ReadString(JsonElement element, string property)
        => ReadText(element, property) ?? throw new SignInException("登录响应不完整，请重试。");

    internal sealed record CaptchaChallenge(string Token, string SiteKey, string Challenge);

    internal sealed record CaptchaProof(string Challenge, string Validate, string Seccode);

    internal sealed record PhoneRegion(string DialingCode, string Name)
    {
        public override string ToString() => $"{Name} (+{DialingCode})";

        public bool TryNormalizePhone(string input, out string phone)
        {
            phone = string.Concat(input.Where(character => !char.IsWhiteSpace(character) && character is not ('-' or '(' or ')')));
            var prefix = "+" + DialingCode;
            if (phone.StartsWith(prefix, StringComparison.Ordinal))
            {
                phone = phone[prefix.Length..];
            }

            if (phone.Length is < 4 or > 15 || !phone.All(char.IsAsciiDigit))
            {
                return false;
            }

            return DialingCode != "86" || (phone.Length == 11 && phone[0] == '1' && phone[1] is >= '3' and <= '9');
        }
    }

    internal sealed class SignInException : Exception
    {
        public int? BusinessCode { get; init; }

        public CaptchaChallenge? Captcha { get; init; }

        public PhoneVerification? PhoneVerification { get; init; }

        public SignInException()
            : this(null)
        {
        }

        public SignInException(string? message)
            : this(message, null)
        {
        }

        public SignInException(string? message, Exception? innerException)
            : base(message ?? "登录失败，请稍后重试。", innerException)
        {
        }
    }
}
