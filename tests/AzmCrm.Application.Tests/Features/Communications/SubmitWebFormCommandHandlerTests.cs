using AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class SubmitWebFormCommandHandlerTests
{
    [Fact]
    public async Task Submit_with_new_email_creates_customer_conversation_and_inbound_message()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SubmitWebFormCommandHandler(dbContext);

        var result = await handler.Handle(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, "Billing", "I have a billing question"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(CommunicationChannel.WebForm, conversation.Channel);

        var customer = await dbContext.Customers.SingleAsync(c => c.Id == conversation.CustomerId);
        Assert.Equal("jane@example.com", customer.Email);

        var message = await dbContext.Messages.SingleAsync(m => m.ConversationId == conversation.Id);
        Assert.Equal(MessageDirection.Inbound, message.Direction);
    }

    [Fact]
    public async Task Submit_with_existing_email_reuses_customer_case_insensitively()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var existing = new Customer { FullName = "Jane Doe", Email = "Jane@Example.com" };
        dbContext.Customers.Add(existing);
        await dbContext.SaveChangesAsync();

        var handler = new SubmitWebFormCommandHandler(dbContext);

        var result = await handler.Handle(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, null, "Follow-up question"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await dbContext.Customers.CountAsync());

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(existing.Id, conversation.CustomerId);
    }

    [Fact]
    public async Task Submit_always_creates_a_new_conversation_even_for_repeat_submitter()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SubmitWebFormCommandHandler(dbContext);

        await handler.Handle(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, null, "First message"),
            CancellationToken.None);
        await handler.Handle(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, null, "Second message"),
            CancellationToken.None);

        Assert.Equal(1, await dbContext.Customers.CountAsync());
        Assert.Equal(2, await dbContext.Conversations.CountAsync());
    }
}
