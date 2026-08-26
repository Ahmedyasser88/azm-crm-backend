using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Identity.Commands.RevokeToken;

internal sealed class RevokeTokenCommandHandler(
    IIdentityService identityService,
    ILogger<RevokeTokenCommandHandler> logger)
    : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        logger.LogInformation("Attempting to revoke token");

        var result = await identityService.RevokeTokenAsync(
            request.RefreshToken,
            request.IpAddress,
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("Token revoked successfully");
        }
        else
        {
            logger.LogWarning("Token revocation failed");
        }

        return result;
    }
}
