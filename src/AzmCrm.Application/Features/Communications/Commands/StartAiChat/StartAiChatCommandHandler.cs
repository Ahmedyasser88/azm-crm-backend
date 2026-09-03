using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.StartAiChat;

internal sealed class StartAiChatCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<StartAiChatCommand, Result<ChatbotReplyDto>>
{
    public async Task<Result<ChatbotReplyDto>> Handle(StartAiChatCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail, ct);

        if (customer is null)
        {
            customer = new Customer { FullName = request.Name, Email = request.Email };
            dbContext.Customers.Add(customer);
        }

        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.Chatbot
        };
        dbContext.Conversations.Add(conversation);

        var customerMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        };
        dbContext.Messages.Add(customerMessage);

        // Customer message and conversation are saved before the AI call — an AI-provider
        // failure below must never lose the customer's own message. See ChatbotReplyGenerator.
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
