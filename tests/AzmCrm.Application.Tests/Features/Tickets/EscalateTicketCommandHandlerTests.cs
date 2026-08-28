using AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class EscalateTicketCommandHandlerTests
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
    public async Task Escalate_sets_IsEscalated_and_EscalatedOn_and_logs_history()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new EscalateTicketCommandHandler(dbContext);

        var result = await handler.Handle(
            new EscalateTicketCommand(ticket.Id, "SLA breach imminent"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.True(persisted.IsEscalated);
        Assert.NotNull(persisted.EscalatedOn);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.Escalated, entry.EventType);
        Assert.Contains("SLA breach imminent", entry.Description);
    }

    [Fact]
    public async Task Escalate_already_escalated_ticket_updates_EscalatedOn_and_logs_another_history_row()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new EscalateTicketCommandHandler(dbContext);

        await handler.Handle(new EscalateTicketCommand(ticket.Id, "First escalation"), CancellationToken.None);
        var firstEscalatedOn = (await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id)).EscalatedOn;

        var result = await handler.Handle(
            new EscalateTicketCommand(ticket.Id, "Still not resolved"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal(TicketHistoryEventType.Escalated, h.EventType));

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.NotNull(persisted.EscalatedOn);
        Assert.True(persisted.EscalatedOn >= firstEscalatedOn);
    }

    [Fact]
    public async Task Escalate_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new EscalateTicketCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new EscalateTicketCommand(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task Escalate_without_reason_uses_default_description()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new EscalateTicketCommandHandler(dbContext);
        await handler.Handle(new EscalateTicketCommand(ticket.Id, null), CancellationToken.None);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal("Ticket escalated.", entry.Description);
    }
}
