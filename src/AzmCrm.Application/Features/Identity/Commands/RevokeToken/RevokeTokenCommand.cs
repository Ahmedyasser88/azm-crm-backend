using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Commands.RevokeToken;

public sealed record RevokeTokenCommand(
    string RefreshToken,
    string IpAddress
) : IRequest<Result>;
