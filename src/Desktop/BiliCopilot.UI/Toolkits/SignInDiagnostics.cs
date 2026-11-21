// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BiliCopilot.UI.Toolkits;

internal static class SignInDiagnostics
{
    public static void LogFailure(ILogger logger, string stage, Exception exception)
    {
        int? httpStatus = null;
        int? businessCode = null;
        var failureType = exception.GetType().Name;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } status })
            {
                httpStatus ??= (int)status;
            }

            businessCode ??= current is NativeSignInClient.SignInException { BusinessCode: { } code }
                ? code
                : ReadBusinessCode(current.Message);
        }

        logger.LogWarning(
            "登录失败，阶段 {SignInStage}，异常 {FailureType}，HResult {HResult}，HTTP {HttpStatus}，业务码 {BusinessCode}",
            stage, failureType, exception.HResult, httpStatus, businessCode);
    }

    private static int? ReadBusinessCode(string message)
    {
        if (message.Length > 16_384 || !message.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(message);
            return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number
                && code.TryGetInt32(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
