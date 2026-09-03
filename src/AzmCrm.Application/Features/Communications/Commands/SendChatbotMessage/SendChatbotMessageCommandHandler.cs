using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;

internal sealed class SendChatbotMessageCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<SendChatbotMessageCommand, Result<ChatbotReplyDto>>
{
    public async Task<Result<ChatbotReplyDto>> Handle(SendChatbotMessageCommand request, CancellationToken ct)
    {
        // A conversation id belonging to a different channel 404s indistinguishably from a
        // nonexistent id — never confirms existence of a mismatched-channel resource. Same
        // reasoning as ChatHub.GetLiveChatConversationOrThrowAsync's Channel check (KAN-3 Story 12).
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.Channel == CommunicationChannel.Chatbot, ct)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var customerMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        };
        dbContext.Messages.Add(customerMessage);

        await dbContext.SaveChangesAsync(ct);

        var replyText = await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, request.Body, ct);

        var botMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = replyText
        };
        dbContext.Messages.Add(botMessage);

        await dbContext.SaveChangesAsync(ct);

        var dto = new ChatbotReplyDto(
            conversation.Id,
            new MessageDto(customerMessage.Id, conversation.Id, customerMessage.Direction, customerMessage.Body,
                customerMessage.CreatedBy, customerMessage.CreatedOn),
            new MessageDto(botMessage.Id, conversation.Id, botMessage.Direction, botMessage.Body,
                botMessage.CreatedBy, botMessage.CreatedOn));

        return Result<ChatbotReplyDto>.Success(dto);
    }
}
