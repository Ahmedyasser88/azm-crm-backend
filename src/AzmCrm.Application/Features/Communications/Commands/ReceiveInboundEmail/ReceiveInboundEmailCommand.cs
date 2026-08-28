using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

public sealed record ReceiveInboundEmailCommand(
    string FromEmail,
    string? FromName,
    string? Subject,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
