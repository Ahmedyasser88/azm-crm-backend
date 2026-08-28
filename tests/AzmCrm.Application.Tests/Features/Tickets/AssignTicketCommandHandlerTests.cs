using AzmCrm.Application.Features.Tickets.Commands.AssignTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class AssignTicketCommandHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Ticket Ticket)> SeedTicketAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return (dbContext, ticket);
    }

    [Fact]
    public async Task Assign_to_known_agent_sets_AssignedToUserId_and_logs_Assigned_history()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var agentId = Guid.NewGuid();
        var identityQueryService = new StubIdentityQueryService();
        identityQueryService.Users[agentId] = ("Agent Smith", "agent@azm.com.sa");

        var handler = new AssignTicketCommandHandler(dbContext, identityQueryService);

        var result = await handler.Handle(new AssignTicketCommand(ticket.Id, agentId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(agentId, persisted.AssignedToUserId);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.Assigned, entry.EventType);
    }

    [Fact]
    public async Task Assign_to_unknown_agent_throws_NotFoundException()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new AssignTicketCommandHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new AssignTicketCommand(ticket.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Unassign_clears_AssignedToUserId_and_logs_Unassigned_history()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var agentId = Guid.NewGuid();
        var identityQueryService = new StubIdentityQueryService();
        identityQueryService.Users[agentId] = ("Agent Smith", "agent@azm.com.sa");

        var handler = new AssignTicketCommandHandler(dbContext, identityQueryService);
        await handler.Handle(new AssignTicketCommand(ticket.Id, agentId), CancellationToken.None);

        var result = await handler.Handle(new AssignTicketCommand(ticket.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Null(persisted.AssignedToUserId);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.EventType == TicketHistoryEventType.Unassigned);
    }

    [Fact]
    public async Task Reassign_to_same_agent_persists_without_extra_history_row()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var agentId = Guid.NewGuid();
        var identityQueryService = new StubIdentityQueryService();
        identityQueryService.Users[agentId] = ("Agent Smith", "agent@azm.com.sa");

        var handler = new AssignTicketCommandHandler(dbContext, identityQueryService);
        await handler.Handle(new AssignTicketCommand(ticket.Id, agentId), CancellationToken.None);

        var result = await handler.Handle(new AssignTicketCommand(ticket.Id, agentId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Single(history);
    }

    [Fact]
    public async Task Assign_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new AssignTicketCommandHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new AssignTicketCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }
}
