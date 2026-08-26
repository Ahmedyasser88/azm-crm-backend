using AzmCrm.Application.Shared.Interfaces;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AzmCrm.Infrastructure.Identity;

internal sealed class CurrentUserService : ICurrentUserService
{
    // Eagerly capture the ClaimsPrincipal and token at construction time so that
    // background tasks that outlive the HTTP request never touch the disposed
    // HttpContext / IFeatureCollection.
    private readonly ClaimsPrincipal? _user;
    private readonly string? _accessToken;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;
        _user = httpContext?.User;

        var authHeader = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _accessToken = authHeader["Bearer ".Length..];
        }
    }

    public Guid? UserId
    {
        get
        {
            var userIdString = _user?.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? _user?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.TryParse(userIdString, out var userId))
                return userId;

            return null;
        }
    }

    public string? Username => _user?.FindFirstValue(ClaimTypes.GivenName)
        ?? _user?.FindFirstValue(JwtRegisteredClaimNames.GivenName)
        ?? _user?.FindFirstValue(ClaimTypes.Name)
        ?? _user?.FindFirstValue(JwtRegisteredClaimNames.Name);

    public string? Email => _user?.FindFirstValue(ClaimTypes.Email)
        ?? _user?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public string? MobileNumber => _user?.FindFirstValue("mobile_number");

    public IEnumerable<string> Roles => _user?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value) ?? [];

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated ?? false;

    public string? UserType => _user?.FindFirstValue(ClaimTypes.Role);

    public string? AccessToken => _accessToken;
}
