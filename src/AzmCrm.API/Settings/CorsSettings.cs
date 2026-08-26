namespace AzmCrm.API.Settings;

/// <summary>
/// Cross-origin allow-list, bound from the "Cors" section of appsettings.json.
/// Any "localhost" or "127.0.0.1" origin (on any port) is always allowed in
/// addition to whatever is listed here — see <see cref="Extensions.CorsOriginValidator"/>.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
