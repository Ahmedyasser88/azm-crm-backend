using AzmCrm.Domain.Features.Identity;
using System.Security.Claims;

namespace AzmCrm.Application.Shared.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
