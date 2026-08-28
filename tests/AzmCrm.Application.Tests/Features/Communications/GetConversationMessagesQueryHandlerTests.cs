using AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class GetConversationMessagesQueryHandlerTests
{
    [Fact]
    public async Task Messages_return_ordered_oldest_first()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        var conversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Email };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        // Insert out of chronological order to prove the handler sorts, not just returns insertion order.
        var second = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = "Second",
            CreatedOn = DateTime.UtcNow.AddMinutes(2)
        };
        var first = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = "First",
            CreatedOn = DateTime.UtcNow.AddMinutes(1)
        };
        dbContext.Messages.AddRange(second, first);
        await dbContext.SaveChangesAsync();

        var handler = new GetConversationMessagesQueryHandler(dbContext);
        var result = await handler.Handle(
            new GetConversationMessagesQuery(conversation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(first.Id, items[0].Id);
        Assert.Equal(second.Id, items[1].Id);
    }

    [Fact]
    public async Task Messages_for_missing_conversation_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetConversationMessagesQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetConversationMessagesQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
