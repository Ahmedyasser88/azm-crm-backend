using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;

public sealed record ReceiveInboundWhatsAppMessageCommand(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
