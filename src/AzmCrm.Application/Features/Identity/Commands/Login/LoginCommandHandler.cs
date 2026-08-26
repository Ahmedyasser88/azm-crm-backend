using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Identity.Commands.Login;

internal sealed class LoginCommandHandler(
    IIdentityService identityService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<AuthenticationResponse>>
{
    public async Task<Result<AuthenticationResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        logger.LogInformation("Login attempt for: {UsernameOrEmail}", request.UsernameOrEmail);

        var result = await identityService.LoginAsync(
            request.UsernameOrEmail,
            request.Password,
            request.IpAddress,
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("User {UsernameOrEmail} logged in successfully", request.UsernameOrEmail);
        }
        else
        {
            logger.LogWarning("Login failed for {UsernameOrEmail}", request.UsernameOrEmail);
        }

        return result;
    }
}
