using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Identity.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IIdentityService identityService,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<AuthenticationResponse>>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken ct)
    {
        logger.LogInformation("Attempting to refresh token");

        var result = await identityService.RefreshTokenAsync(
            request.RefreshToken,
            request.IpAddress,
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("Token refreshed successfully");
        }
        else
        {
            logger.LogWarning("Token refresh failed");
        }

        return result;
    }
}
