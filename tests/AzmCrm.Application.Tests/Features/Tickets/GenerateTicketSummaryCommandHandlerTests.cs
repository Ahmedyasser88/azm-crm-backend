using AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class GenerateTicketSummaryCommandHandlerTests
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
    public async Task Generate_persists_summary_and_stamps_GeneratedOn()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { Response = "Customer cannot log in; investigating 401 errors." };
        var handler = new GenerateTicketSummaryCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(new GenerateTicketSummaryCommand(ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Customer cannot log in; investigating 401 errors.", result.Data!.Summary);

        var updated = await dbContext.Tickets.FindAsync(ticket.Id);
        Assert.Equal("Customer cannot log in; investigating 401 errors.", updated!.AiSummary);
        Assert.NotNull(updated.AiSummaryGeneratedOn);
    }

    [Fact]
    public async Task Generate_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GenerateTicketSummaryCommandHandler(dbContext, new StubAiClient());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GenerateTicketSummaryCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Generate_when_AiClient_throws_returns_Failure_and_leaves_ticket_AiSummary_null()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { ThrowOnCall = true };
        var handler = new GenerateTicketSummaryCommandHandler(dbContext, aiClient);

        var result = await handler.Handle(new GenerateTicketSummaryCommand(ticket.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var updated = await dbContext.Tickets.FindAsync(ticket.Id);
        Assert.Null(updated!.AiSummary);
        Assert.Null(updated.AiSummaryGeneratedOn);
    }

    [Fact]
    public async Task Generate_includes_recent_TicketComments_in_prompt()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        dbContext.TicketComments.Add(new TicketComment { TicketId = ticket.Id, Content = "Reset password sent." });
        await dbContext.SaveChangesAsync();

        var aiClient = new StubAiClient();
        var handler = new GenerateTicketSummaryCommandHandler(dbContext, aiClient);

        await handler.Handle(new GenerateTicketSummaryCommand(ticket.Id), CancellationToken.None);

        Assert.Single(aiClient.Calls);
        Assert.Contains("Reset password sent.", aiClient.Calls[0].UserPrompt);
    }

    [Fact]
    public async Task Generate_overwrites_previous_summary_on_second_call()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var aiClient = new StubAiClient { Response = "First summary." };
        var handler = new GenerateTicketSummaryCommandHandler(dbContext, aiClient);
        await handler.Handle(new GenerateTicketSummaryCommand(ticket.Id), CancellationToken.None);

        aiClient.Response = "Second summary.";
        var result = await handler.Handle(new GenerateTicketSummaryCommand(ticket.Id), CancellationToken.None);

        Assert.Equal("Second summary.", result.Data!.Summary);
        var updated = await dbContext.Tickets.FindAsync(ticket.Id);
        Assert.Equal("Second summary.", updated!.AiSummary);
    }
}
