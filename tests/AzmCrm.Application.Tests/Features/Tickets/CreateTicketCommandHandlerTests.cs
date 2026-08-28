using AzmCrm.Application.Features.Tickets.Commands.CreateTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_ticket_with_New_status_and_logs_Created_history()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Cannot log in", "User gets 401 on login", TicketCategory.Technical, TicketPriority.High);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(customer.Id, ticket.CustomerId);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal(TicketCategory.Technical, ticket.Category);
        Assert.Equal(TicketPriority.High, ticket.Priority);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.Created, entry.EventType);
    }

    [Fact]
    public async Task Create_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            Guid.NewGuid(), "Cannot log in", null, TicketCategory.Technical, TicketPriority.High);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
