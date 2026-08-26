using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Commands.Login;

public sealed record LoginCommand(
    string UsernameOrEmail,
    string Password,
    string IpAddress
) : IRequest<Result<AuthenticationResponse>>;
