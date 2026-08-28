using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class ReceiveInboundEmailCommandHandlerTests
{
    [Fact]
    public async Task Receive_with_new_sender_email_creates_customer_and_conversation()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new ReceiveInboundEmailCommandHandler(dbContext);

        var result = await handler.Handle(
            new ReceiveInboundEmailCommand("jane@example.com", "Jane Doe", "Help", "I need help", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(CommunicationChannel.Email, conversation.Channel);

        var customer = await dbContext.Customers.SingleAsync(c => c.Id == conversation.CustomerId);
        Assert.Equal("jane@example.com", customer.Email);
    }

    [Fact]
    public async Task Receive_with_existing_open_conversation_appends_to_it_instead_of_creating_new()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(customer);
        var conversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Email };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var handler = new ReceiveInboundEmailCommandHandler(dbContext);
        var result = await handler.Handle(
            new ReceiveInboundEmailCommand("jane@example.com", "Jane Doe", "Help", "Follow-up", null),
            CancellationToken.None);

        Assert.Equal(conversation.Id, result.Data);
        Assert.Equal(1, await dbContext.Conversations.CountAsync());
        Assert.Equal(1, await dbContext.Messages.CountAsync(m => m.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task Receive_with_closed_existing_conversation_creates_new_conversation()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(customer);
        var closedConversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.Email,
            Status = ConversationStatus.Closed
        };
        dbContext.Conversations.Add(closedConversation);
        await dbContext.SaveChangesAsync();

        var handler = new ReceiveInboundEmailCommandHandler(dbContext);
        var result = await handler.Handle(
            new ReceiveInboundEmailCommand("jane@example.com", "Jane Doe", "Help", "New issue", null),
            CancellationToken.None);

        Assert.NotEqual(closedConversation.Id, result.Data);
        Assert.Equal(2, await dbContext.Conversations.CountAsync());
    }

    [Fact]
    public async Task Receive_with_duplicate_ExternalMessageId_is_idempotent()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new ReceiveInboundEmailCommandHandler(dbContext);

        var command = new ReceiveInboundEmailCommand(
            "jane@example.com", "Jane Doe", "Help", "I need help", "provider-message-1");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.Data, second.Data);
        Assert.Equal(1, await dbContext.Messages.CountAsync());
    }
}
