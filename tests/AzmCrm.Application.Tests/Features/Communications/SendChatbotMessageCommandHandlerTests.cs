using AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class SendChatbotMessageCommandHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Conversation Conversation)> SeedChatbotConversationAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(customer);

        var conversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Chatbot };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        return (dbContext, conversation);
    }

    [Fact]
    public async Task Send_persists_inbound_and_outbound_messages()
    {
        var (dbContext, conversation) = await SeedChatbotConversationAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { Response = "Try clearing your browser cache." };
        var handler = new SendChatbotMessageCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(
            new SendChatbotMessageCommand(conversation.Id, "It's still not working"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var messages = await dbContext.Messages.Where(m => m.ConversationId == conversation.Id).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Direction == MessageDirection.Inbound && m.Body == "It's still not working");
        Assert.Contains(messages, m => m.Direction == MessageDirection.Outbound && m.Body == "Try clearing your browser cache.");
    }

    [Fact]
    public async Task Send_for_missing_conversation_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SendChatbotMessageCommandHandler(dbContext, new StubAiClient());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SendChatbotMessageCommand(Guid.NewGuid(), "Hello"), CancellationToken.None));
    }

    [Fact]
    public async Task Send_for_conversation_with_different_channel_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        var conversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.LiveChat };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var handler = new SendChatbotMessageCommandHandler(dbContext, new StubAiClient());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SendChatbotMessageCommand(conversation.Id, "Hello"), CancellationToken.None));
    }

    [Fact]
    public async Task Send_when_AiClient_throws_persists_fallback_bot_reply_and_still_succeeds()
    {
        var (dbContext, conversation) = await SeedChatbotConversationAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { ThrowOnCall = true };
        var handler = new SendChatbotMessageCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(
            new SendChatbotMessageCommand(conversation.Id, "Hello"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.BotReply.Body));

        var messages = await dbContext.Messages.Where(m => m.ConversationId == conversation.Id).ToListAsync();
        Assert.Equal(2, messages.Count);
    }
}
