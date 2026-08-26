namespace AzmCrm.Application.Features.Identity.DTOs;

public sealed record AuthenticationResponse(
    Guid UserId,
    string Username,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresOn,
    DateTime RefreshTokenExpiresOn
);
