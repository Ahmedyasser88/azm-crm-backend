using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Identity.Commands.Register;

internal sealed class RegisterCommandHandler(
    IIdentityService identityService,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterCommand request, CancellationToken ct)
    {
        logger.LogInformation("Attempting to register user: {Username}", request.Username);

        var result = await identityService.RegisterAsync(
            request.Username,
            request.Email,
            request.MobileNumber,
            request.Password,
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("User {Username} registered successfully with ID: {UserId}",
                request.Username, result.Data);
        }
        else
        {
            logger.LogWarning("Registration failed for {Username}: {Errors}",
                request.Username, string.Join(", ", result.Errors));
        }

        return result;
    }
}
