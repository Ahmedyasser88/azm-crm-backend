using AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class CreateTicketCommentCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_comment_for_ticket()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        var ticket = new Ticket
        {
            CustomerId = customer.Id, Title = "T", Category = TicketCategory.General, Priority = TicketPriority.Low
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommentCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateTicketCommentCommand(ticket.Id, "Escalating to billing team"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var comment = Assert.Single(dbContext.TicketComments);
        Assert.Equal(ticket.Id, comment.TicketId);
        Assert.Equal("Escalating to billing team", comment.Content);
    }

    [Fact]
    public async Task Create_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateTicketCommentCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateTicketCommentCommand(Guid.NewGuid(), "Content"), CancellationToken.None));
    }
}
