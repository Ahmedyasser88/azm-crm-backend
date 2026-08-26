using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Identity;

namespace AzmCrm.Application.Shared.Interfaces;

public interface IIdentityService
{
    Task<Result<Guid>> RegisterAsync(
        string username,
        string email,
        string mobileNumber,
        string password,
        CancellationToken ct = default);

    Task<Result<AuthenticationResponse>> LoginAsync(
        string usernameOrEmail,
        string password,
        string ipAddress,
        CancellationToken ct = default);

    Task<Result<AuthenticationResponse>> RefreshTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken ct = default);

    Task<Result> RevokeTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken ct = default);

    Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    Task<ApplicationUser> GetOrCreateExternalUserAsync(
        Guid userId,
        string username,
        string? email,
        IEnumerable<string> roles,
        CancellationToken ct = default);
}
