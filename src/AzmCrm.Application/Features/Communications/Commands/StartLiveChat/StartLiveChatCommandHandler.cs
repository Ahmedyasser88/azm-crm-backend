using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

internal sealed class StartLiveChatCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<StartLiveChatCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartLiveChatCommand request, CancellationToken ct)
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
            Channel = CommunicationChannel.LiveChat
        };
        dbContext.Conversations.Add(conversation);

        dbContext.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
