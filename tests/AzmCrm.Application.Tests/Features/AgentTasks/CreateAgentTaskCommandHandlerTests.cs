using AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class CreateAgentTaskCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_task_owned_by_current_user()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var handler = new CreateAgentTaskCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(
            new CreateAgentTaskCommand("Call back customer", "Follow up", DateTime.UtcNow.AddDays(1), null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var task = Assert.Single(dbContext.AgentTasks);
        Assert.Equal(currentUser.UserId, task.AssignedToUserId);
        Assert.Equal("Call back customer", task.Title);
    }

    [Fact]
    public async Task Create_with_unknown_customerId_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateAgentTaskCommand("Title", null, null, Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task Create_with_unknown_ticketId_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateAgentTaskCommand("Title", null, null, null, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Create_without_optional_links_succeeds()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(
            new CreateAgentTaskCommand("Title", null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_with_known_customer_and_ticket_succeeds()
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

        var handler = new CreateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(
            new CreateAgentTaskCommand("Title", null, null, customer.Id, ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
