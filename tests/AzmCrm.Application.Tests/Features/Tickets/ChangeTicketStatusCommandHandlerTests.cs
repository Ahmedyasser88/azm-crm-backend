using AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class ChangeTicketStatusCommandHandlerTests
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
    public async Task Change_to_new_status_persists_and_logs_StatusChanged_history()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new ChangeTicketStatusCommandHandler(dbContext);

        var result = await handler.Handle(
            new ChangeTicketStatusCommand(ticket.Id, TicketStatus.InProgress), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.InProgress, persisted.Status);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.StatusChanged, entry.EventType);
    }

    [Fact]
    public async Task Change_to_same_status_persists_without_extra_history_row()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new ChangeTicketStatusCommandHandler(dbContext);

        var result = await handler.Handle(
            new ChangeTicketStatusCommand(ticket.Id, ticket.Status), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Empty(history);
    }

    [Fact]
    public async Task Change_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new ChangeTicketStatusCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new ChangeTicketStatusCommand(Guid.NewGuid(), TicketStatus.Open), CancellationToken.None));
    }
}
