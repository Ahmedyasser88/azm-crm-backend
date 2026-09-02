using AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Dashboard;

public class GetMyTicketsQueryHandlerTests
{
    [Fact]
    public async Task Returns_only_tickets_assigned_to_current_user()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);

        var myTicket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "My ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            AssignedToUserId = currentUser.UserId
        };
        var otherTicket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Not my ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            AssignedToUserId = Guid.NewGuid()
        };
        dbContext.Tickets.AddRange(myTicket, otherTicket);
        await dbContext.SaveChangesAsync();

        var handler = new GetMyTicketsQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetMyTicketsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(myTicket.Id, item.Id);
    }

    [Fact]
    public async Task Filters_by_status()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);

        var openTicket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Open ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open,
            AssignedToUserId = currentUser.UserId
        };
        var resolvedTicket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Resolved ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            Status = TicketStatus.Resolved,
            AssignedToUserId = currentUser.UserId
        };
        dbContext.Tickets.AddRange(openTicket, resolvedTicket);
        await dbContext.SaveChangesAsync();

        var handler = new GetMyTicketsQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetMyTicketsQuery(Status: TicketStatus.Open), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(openTicket.Id, item.Id);
    }

    [Fact]
    public async Task Embeds_customer_summary_for_each_ticket()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe", CompanyName = "Acme", Email = "jane@acme.com" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "My ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            AssignedToUserId = currentUser.UserId
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var handler = new GetMyTicketsQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetMyTicketsQuery(), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.NotNull(item.Customer);
        Assert.Equal(customer.Id, item.Customer!.Id);
        Assert.Equal("Jane Doe", item.Customer.FullName);
        Assert.Equal("Acme", item.Customer.CompanyName);
    }

    [Fact]
    public async Task Customer_is_null_when_customer_was_soft_deleted()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "My ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            AssignedToUserId = currentUser.UserId
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        customer.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var handler = new GetMyTicketsQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetMyTicketsQuery(), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ticket.Id, item.Id);
        Assert.Null(item.Customer);
    }

    [Fact]
    public async Task Returns_empty_page_when_no_tickets_assigned()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var handler = new GetMyTicketsQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetMyTicketsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }
}
