using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;

internal sealed class ReceiveInboundSmsCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ReceiveInboundSmsCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReceiveInboundSmsCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ExternalMessageId))
        {
            var existing = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.ExternalMessageId == request.ExternalMessageId, ct);
            if (existing is not null)
                return Result<Guid>.Success(existing.ConversationId);
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == request.FromPhoneNumber, ct);

        if (customer is null)
        {
            customer = new Customer
            {
                FullName = request.FromPhoneNumber,
                PhoneNumber = request.FromPhoneNumber
            };
            dbContext.Customers.Add(customer);
        }

        var conversation = await dbContext.Conversations
            .Where(c => c.CustomerId == customer.Id
                        && c.Channel == CommunicationChannel.Sms
                        && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerId = customer.Id,
                Channel = CommunicationChannel.Sms
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
