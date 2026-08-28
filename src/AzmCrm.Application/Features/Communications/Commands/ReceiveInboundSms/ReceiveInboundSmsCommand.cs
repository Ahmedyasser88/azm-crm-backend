using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;

public sealed record ReceiveInboundSmsCommand(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
