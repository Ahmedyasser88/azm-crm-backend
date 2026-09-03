using AzmCrm.Application.Features.Tickets.Queries.SuggestTicketReply;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class SuggestTicketReplyQueryHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Ticket Ticket)> SeedTicketAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Cannot log in",
            Description = "User gets 401 on login",
            Category = TicketCategory.Technical,
            Priority = TicketPriority.High
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return (dbContext, ticket);
    }

    [Fact]
    public async Task Suggest_returns_AI_generated_reply()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { Response = "Hi, thanks for reaching out — let's get this fixed." };
        var handler = new SuggestTicketReplyQueryHandler(dbContext, aiClient);

        var result = await handler.Handle(new SuggestTicketReplyQuery(ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hi, thanks for reaching out — let's get this fixed.", result.Data!.SuggestedReply);
    }

    [Fact]
    public async Task Suggest_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SuggestTicketReplyQueryHandler(dbContext, new StubAiClient());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new SuggestTicketReplyQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Suggest_when_AiClient_throws_returns_Failure()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { ThrowOnCall = true };
        var handler = new SuggestTicketReplyQueryHandler(dbContext, aiClient);

        var result = await handler.Handle(new SuggestTicketReplyQuery(ticket.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Suggest_includes_ticket_Title_and_Description_in_prompt()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient();
        var handler = new SuggestTicketReplyQueryHandler(dbContext, aiClient);

        await handler.Handle(new SuggestTicketReplyQuery(ticket.Id), CancellationToken.None);

        Assert.Single(aiClient.Calls);
        Assert.Contains("Cannot log in", aiClient.Calls[0].UserPrompt);
        Assert.Contains("User gets 401 on login", aiClient.Calls[0].UserPrompt);
    }
}
