using AzmCrm.Application.Features.Communications.Commands.StartAiChat;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class StartAiChatCommandHandlerTests
{
    [Fact]
    public async Task Start_creates_customer_conversation_and_persists_inbound_and_outbound_messages()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var aiClient = new StubAiClient { Response = "Here's how to reset your password." };
        var handler = new StartAiChatCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(
            new StartAiChatCommand("Jane Doe", "jane@example.com", "How do I reset my password?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("How do I reset my password?", result.Data!.CustomerMessage.Body);
        Assert.Equal("Here's how to reset your password.", result.Data.BotReply.Body);

        var messages = await dbContext.Messages
            .Where(m => m.ConversationId == result.Data.ConversationId)
            .ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Direction == MessageDirection.Inbound);
        Assert.Contains(messages, m => m.Direction == MessageDirection.Outbound);
    }

    [Fact]
    public async Task Start_creates_Conversation_with_Chatbot_channel()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new StartAiChatCommandHandler(dbContext, new StubAiClient());

        var result = await handler.Handle(
            new StartAiChatCommand("Jane Doe", "jane@example.com", "Hello"), CancellationToken.None);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data!.ConversationId);
        Assert.Equal(CommunicationChannel.Chatbot, conversation.Channel);
    }

    [Fact]
    public async Task Start_reuses_existing_customer_by_email()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var existing = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        dbContext.Customers.Add(existing);
        await dbContext.SaveChangesAsync();

        var handler = new StartAiChatCommandHandler(dbContext, new StubAiClient());
        var result = await handler.Handle(
            new StartAiChatCommand("Jane Doe", "jane@example.com", "Hello again"), CancellationToken.None);

        Assert.Equal(1, await dbContext.Customers.CountAsync());
        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data!.ConversationId);
        Assert.Equal(existing.Id, conversation.CustomerId);
    }

    [Fact]
    public async Task Start_when_AiClient_throws_persists_fallback_bot_reply_and_still_succeeds()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var aiClient = new StubAiClient { ThrowOnCall = true };
        var handler = new StartAiChatCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(
            new StartAiChatCommand("Jane Doe", "jane@example.com", "Hello"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.BotReply.Body));

        // The customer's own message must still be persisted even though the AI call failed.
        var messages = await dbContext.Messages
            .Where(m => m.ConversationId == result.Data.ConversationId)
            .ToListAsync();
        Assert.Equal(2, messages.Count);
    }
}
