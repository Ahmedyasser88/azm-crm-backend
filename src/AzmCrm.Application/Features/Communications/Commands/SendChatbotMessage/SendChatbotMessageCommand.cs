using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;

public sealed record SendChatbotMessageCommand(Guid ConversationId, string Body) : IRequest<Result<ChatbotReplyDto>>;
