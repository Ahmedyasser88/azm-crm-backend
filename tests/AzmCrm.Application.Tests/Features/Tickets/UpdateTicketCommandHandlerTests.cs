using AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class UpdateTicketCommandHandlerTests
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
            Description = "Original description",
            Category = TicketCategory.Technical,
            Priority = TicketPriority.High
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return (dbContext, ticket);
    }

    [Fact]
    public async Task Update_changes_persist_and_log_history_for_changed_fields()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new UpdateTicketCommandHandler(dbContext);

        var command = new UpdateTicketCommand(
            ticket.Id, "Cannot log in at all", "Original description", TicketCategory.Technical, TicketPriority.Urgent);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal("Cannot log in at all", persisted.Title);
        Assert.Equal(TicketPriority.Urgent, persisted.Priority);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Description == "Title changed.");
        Assert.Contains(history, h => h.Description == "Priority changed.");
    }

    [Fact]
    public async Task Update_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateTicketCommandHandler(dbContext);

        var command = new UpdateTicketCommand(
            Guid.NewGuid(), "Title", null, TicketCategory.General, TicketPriority.Low);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_with_no_changes_persists_without_extra_history_rows()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var handler = new UpdateTicketCommandHandler(dbContext);

        var command = new UpdateTicketCommand(
            ticket.Id, ticket.Title, ticket.Description, ticket.Category, ticket.Priority);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Empty(history);
    }
}
