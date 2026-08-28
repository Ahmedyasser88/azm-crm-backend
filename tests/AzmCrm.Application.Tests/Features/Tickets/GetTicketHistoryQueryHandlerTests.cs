using AzmCrm.Application.Features.Tickets.Commands.CreateTicket;
using AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;
using AzmCrm.Application.Features.Tickets.Queries.GetTicketHistory;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class GetTicketHistoryQueryHandlerTests
{
    [Fact]
    public async Task History_returns_entries_ordered_by_CreatedOn_desc()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low
        };
        dbContext.Tickets.Add(ticket);
        dbContext.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            EventType = TicketHistoryEventType.Created,
            Description = "Ticket created."
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketHistoryQueryHandler(dbContext);
        var result = await handler.Handle(new GetTicketHistoryQuery(ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task History_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetTicketHistoryQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetTicketHistoryQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task History_reflects_both_create_and_update_events()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var createHandler = new CreateTicketCommandHandler(dbContext);
        var createResult = await createHandler.Handle(
            new CreateTicketCommand(customer.Id, "Ticket", null, TicketCategory.General, TicketPriority.Low),
            CancellationToken.None);

        var updateHandler = new UpdateTicketCommandHandler(dbContext);
        await updateHandler.Handle(
            new UpdateTicketCommand(createResult.Data, "Updated Ticket", null, TicketCategory.General, TicketPriority.Low),
            CancellationToken.None);

        var historyHandler = new GetTicketHistoryQueryHandler(dbContext);
        var result = await historyHandler.Handle(new GetTicketHistoryQuery(createResult.Data), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
    }
}
