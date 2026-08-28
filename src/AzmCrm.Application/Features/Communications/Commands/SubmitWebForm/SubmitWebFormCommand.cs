using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;

public sealed record SubmitWebFormCommand(
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string Body
) : IRequest<Result<Guid>>;
