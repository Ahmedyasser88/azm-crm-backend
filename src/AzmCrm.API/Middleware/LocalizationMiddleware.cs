using AzmCrm.Application.Localization;

namespace AzmCrm.API.Middleware;

internal sealed class LocalizationMiddleware(
    RequestDelegate next,
    ILogger<LocalizationMiddleware> logger)
{
    private const string AcceptLanguageHeader = "Accept-Language";

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
    {
        var language = GetLanguageFromRequest(context);

        if (!string.IsNullOrEmpty(language))
        {
            localizationService.SetLanguage(language);
            logger.LogDebug("Language set to: {Language}", language);
        }

        await next(context);
    }

    private static string? GetLanguageFromRequest(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(AcceptLanguageHeader, out var headerValue))
        {
            var language = headerValue.ToString().Split(',').FirstOrDefault()?.Split(';').FirstOrDefault()?.Trim();
            return language;
        }

        return context.Request.Query["lang"].ToString();
    }
}
