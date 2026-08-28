using AzmCrm.Application.Features.Communications.Commands.SendMessage;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class SendMessageCommandHandlerTests
{
    private sealed class ThrowingChannelMessageSender : IChannelMessageSender
    {
        public required CommunicationChannel Channel { get; init; }

        public Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated provider failure");
    }

    private sealed class RecordingChannelMessageSender : IChannelMessageSender
    {
        public required CommunicationChannel Channel { get; init; }
        public bool WasCalled { get; private set; }

        public Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private static async Task<(TestApplicationDbContext DbContext, Conversation Conversation)> SeedConversationAsync(
        CommunicationChannel channel = CommunicationChannel.Email)
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(customer);

        var conversation = new Conversation { CustomerId = customer.Id, Channel = channel };
        dbContext.Conversations.Add(conversation);

        await dbContext.SaveChangesAsync();

        return (dbContext, conversation);
    }

    [Fact]
    public async Task Send_persists_outbound_message_and_returns_success_when_no_sender_registered()
    {
        var (dbContext, conversation) = await SeedConversationAsync();
        await using var _ = dbContext;

        var handler = new SendMessageCommandHandler(
            dbContext, [], NullLogger<SendMessageCommandHandler>.Instance);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Thanks for reaching out"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var message = await dbContext.Messages.SingleAsync(m => m.Id == result.Data!.Id);
        Assert.Equal(MessageDirection.Outbound, message.Direction);
    }

    [Fact]
    public async Task Send_for_missing_conversation_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SendMessageCommandHandler(
            dbContext, [], NullLogger<SendMessageCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new SendMessageCommand(Guid.NewGuid(), "Hi"), CancellationToken.None));
    }

    [Fact]
    public async Task Send_returns_success_even_when_registered_sender_throws()
    {
        var (dbContext, conversation) = await SeedConversationAsync();
        await using var _ = dbContext;

        var senders = new IChannelMessageSender[] { new ThrowingChannelMessageSender { Channel = conversation.Channel } };
        var handler = new SendMessageCommandHandler(
            dbContext, senders, NullLogger<SendMessageCommandHandler>.Instance);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Hello"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(await dbContext.Messages.AnyAsync(m => m.Id == result.Data!.Id));
    }

    [Fact]
    public async Task Send_invokes_matching_sender_only()
    {
        var (dbContext, conversation) = await SeedConversationAsync(CommunicationChannel.Email);
        await using var _ = dbContext;

        var matching = new RecordingChannelMessageSender { Channel = CommunicationChannel.Email };
        var other = new RecordingChannelMessageSender { Channel = CommunicationChannel.Sms };
        var handler = new SendMessageCommandHandler(
            dbContext, [matching, other], NullLogger<SendMessageCommandHandler>.Instance);

        await handler.Handle(new SendMessageCommand(conversation.Id, "Hello"), CancellationToken.None);

        Assert.True(matching.WasCalled);
        Assert.False(other.WasCalled);
    }
}
