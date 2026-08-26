using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string IpAddress
) : IRequest<Result<AuthenticationResponse>>;
