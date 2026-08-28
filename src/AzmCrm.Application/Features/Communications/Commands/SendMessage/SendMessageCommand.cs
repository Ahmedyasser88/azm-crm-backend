using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SendMessage;

public sealed record SendMessageCommand(Guid ConversationId, string Body) : IRequest<Result<MessageDto>>;
