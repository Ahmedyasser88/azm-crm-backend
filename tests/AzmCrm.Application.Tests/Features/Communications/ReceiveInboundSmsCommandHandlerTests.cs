using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class ReceiveInboundSmsCommandHandlerTests
{
    [Fact]
    public async Task Receive_with_new_sender_phone_creates_customer_and_conversation()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new ReceiveInboundSmsCommandHandler(dbContext);

        var result = await handler.Handle(
            new ReceiveInboundSmsCommand("+966512345678", "Hello", null), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(CommunicationChannel.Sms, conversation.Channel);

        var customer = await dbContext.Customers.SingleAsync(c => c.Id == conversation.CustomerId);
        Assert.Equal("+966512345678", customer.PhoneNumber);
    }

    [Fact]
    public async Task Receive_with_existing_open_conversation_appends_to_it()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "+966512345678", PhoneNumber = "+966512345678" };
        dbContext.Customers.Add(customer);
        var conversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Sms };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var handler = new ReceiveInboundSmsCommandHandler(dbContext);
        var result = await handler.Handle(
            new ReceiveInboundSmsCommand("+966512345678", "Follow-up", null), CancellationToken.None);

        Assert.Equal(conversation.Id, result.Data);
        Assert.Equal(1, await dbContext.Conversations.CountAsync());
    }

    [Fact]
    public async Task Receive_with_closed_existing_conversation_creates_new_conversation()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "+966512345678", PhoneNumber = "+966512345678" };
        dbContext.Customers.Add(customer);
        var closed = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.Sms,
            Status = ConversationStatus.Closed
        };
        dbContext.Conversations.Add(closed);
        await dbContext.SaveChangesAsync();

        var handler = new ReceiveInboundSmsCommandHandler(dbContext);
        var result = await handler.Handle(
            new ReceiveInboundSmsCommand("+966512345678", "New issue", null), CancellationToken.None);

        Assert.NotEqual(closed.Id, result.Data);
        Assert.Equal(2, await dbContext.Conversations.CountAsync());
    }

    [Fact]
    public async Task Receive_with_duplicate_ExternalMessageId_is_idempotent()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new ReceiveInboundSmsCommandHandler(dbContext);

        var command = new ReceiveInboundSmsCommand("+966512345678", "Hello", "SM123");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.Data, second.Data);
        Assert.Equal(1, await dbContext.Messages.CountAsync());
    }
}
