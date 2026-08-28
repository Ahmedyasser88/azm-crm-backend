using AzmCrm.Application.Features.Communications.Commands.StartLiveChat;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class StartLiveChatCommandHandlerTests
{
    [Fact]
    public async Task Start_with_new_email_creates_customer_conversation_and_inbound_message()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new StartLiveChatCommandHandler(dbContext);

        var result = await handler.Handle(
            new StartLiveChatCommand("Jane Doe", "jane@example.com", "Hi, are you open?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(CommunicationChannel.LiveChat, conversation.Channel);

        var customer = await dbContext.Customers.SingleAsync(c => c.Id == conversation.CustomerId);
        Assert.Equal("jane@example.com", customer.Email);

        var message = await dbContext.Messages.SingleAsync(m => m.ConversationId == conversation.Id);
        Assert.Equal(MessageDirection.Inbound, message.Direction);
    }

    [Fact]
    public async Task Start_with_existing_email_reuses_customer()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var existing = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(existing);
        await dbContext.SaveChangesAsync();

        var handler = new StartLiveChatCommandHandler(dbContext);
        var result = await handler.Handle(
            new StartLiveChatCommand("Jane Doe", "jane@example.com", "Hello again"), CancellationToken.None);

        Assert.Equal(1, await dbContext.Customers.CountAsync());
        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(existing.Id, conversation.CustomerId);
    }
}
