using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

internal sealed class ReceiveInboundEmailCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ReceiveInboundEmailCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReceiveInboundEmailCommand request, CancellationToken ct)
    {
        // Idempotency: a webhook provider may retry the same delivery. If this exact provider
        // message id was already recorded, return the conversation it was already filed under
        // instead of creating a duplicate Message.
        if (!string.IsNullOrWhiteSpace(request.ExternalMessageId))
        {
            var existing = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.ExternalMessageId == request.ExternalMessageId, ct);
            if (existing is not null)
                return Result<Guid>.Success(existing.ConversationId);
        }

        var normalizedEmail = request.FromEmail.Trim().ToLower();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail, ct);

        if (customer is null)
        {
            customer = new Customer
            {
                FullName = request.FromName ?? request.FromEmail,
                Email = request.FromEmail
            };
            dbContext.Customers.Add(customer);
        }

        // Unlike a web-form submission (always a new Conversation), a running email thread
        // should land in the customer's existing open Email conversation, if one exists.
        var conversation = await dbContext.Conversations
            .Where(c => c.CustomerId == customer.Id
                        && c.Channel == CommunicationChannel.Email
                        && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerId = customer.Id,
                Channel = CommunicationChannel.Email,
                Subject = request.Subject
            };
            dbContext.Conversations.Add(conversation);
        }

        dbContext.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body,
            ExternalMessageId = request.ExternalMessageId
        });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
