using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Commands.Register;

public sealed record RegisterCommand(
    string Username,
    string Email,
    string MobileNumber,
    string Password,
    string ConfirmPassword
) : IRequest<Result<Guid>>;
