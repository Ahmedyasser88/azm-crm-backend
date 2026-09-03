using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.StartAiChat;

public sealed record StartAiChatCommand(string Name, string Email, string Body) : IRequest<Result<ChatbotReplyDto>>;
