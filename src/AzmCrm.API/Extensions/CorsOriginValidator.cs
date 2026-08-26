namespace AzmCrm.API.Extensions;

/// <summary>
/// Origin-matching logic used by the CORS policy set up in
/// <see cref="ApplicationExtensions.AddCustomCors"/>.
/// </summary>
public static class CorsOriginValidator
{
    public static bool IsAllowed(string? origin, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        // Any localhost/127.0.0.1 origin is allowed regardless of port, to
        // support local frontend dev servers running on any port.
        if (IsLocalHost(originUri.Host))
        {
            return true;
        }

        foreach (var allowed in allowedOrigins)
        {
            if (Uri.TryCreate(allowed.TrimEnd('/'), UriKind.Absolute, out var allowedUri) &&
                string.Equals(allowedUri.Scheme, originUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(allowedUri.Host, originUri.Host, StringComparison.OrdinalIgnoreCase) &&
                allowedUri.Port == originUri.Port)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
}
