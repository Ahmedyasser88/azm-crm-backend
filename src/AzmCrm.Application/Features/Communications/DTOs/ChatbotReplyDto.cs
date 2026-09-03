namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record ChatbotReplyDto(Guid ConversationId, MessageDto CustomerMessage, MessageDto BotReply);
