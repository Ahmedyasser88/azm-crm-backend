using AzmCrm.Application.Features.Tickets.Queries.GetTicketsList;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class GetTicketsListQueryHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Customer Customer)> SeedCustomerAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return (dbContext, customer);
    }

    [Fact]
    public async Task List_returns_paginated_results_ordered_by_CreatedOn_desc()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        for (var i = 0; i < 3; i++)
        {
            dbContext.Tickets.Add(new Ticket
            {
                CustomerId = customer.Id,
                Title = $"Ticket {i}",
                Category = TicketCategory.General,
                Priority = TicketPriority.Low
            });
        }
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, new StubIdentityQueryService());
        var result = await handler.Handle(new GetTicketsListQuery(PageNumber: 1, PageSize: 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }

    [Fact]
    public async Task List_filters_by_status_category_priority_and_customerId()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        var otherCustomer = new Customer { FullName = "Other Co" };
        dbContext.Customers.Add(otherCustomer);

        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Matching ticket",
            Category = TicketCategory.Billing,
            Priority = TicketPriority.Urgent,
            Status = TicketStatus.Open
        });
        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = otherCustomer.Id,
            Title = "Non-matching ticket",
            Category = TicketCategory.Technical,
            Priority = TicketPriority.Low,
            Status = TicketStatus.New
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, new StubIdentityQueryService());
        var result = await handler.Handle(
            new GetTicketsListQuery(
                CustomerId: customer.Id, Status: TicketStatus.Open,
                Category: TicketCategory.Billing, Priority: TicketPriority.Urgent),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Matching ticket", item.Title);
    }

    [Fact]
    public async Task List_filters_by_search_term_case_insensitively()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Cannot Login",
            Category = TicketCategory.Technical,
            Priority = TicketPriority.Low
        });
        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Billing question",
            Category = TicketCategory.Billing,
            Priority = TicketPriority.Low
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, new StubIdentityQueryService());
        var result = await handler.Handle(new GetTicketsListQuery(Search: "login"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Cannot Login", item.Title);
    }

    [Fact]
    public async Task List_with_blank_search_returns_all()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Ticket A",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, new StubIdentityQueryService());
        var result = await handler.Handle(new GetTicketsListQuery(Search: "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task List_filters_by_assignedToUserId()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        var agentId = Guid.NewGuid();
        var identityQueryService = new StubIdentityQueryService();
        identityQueryService.Users[agentId] = ("Agent Smith", "agent@azm.com.sa");

        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Assigned ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            AssignedToUserId = agentId
        });
        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Unassigned ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, identityQueryService);
        var result = await handler.Handle(new GetTicketsListQuery(AssignedToUserId: agentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Assigned ticket", item.Title);
        Assert.Equal("Agent Smith", item.AssignedToUserName);
    }

    [Fact]
    public async Task List_filters_by_isEscalated()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Escalated ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low,
            IsEscalated = true,
            EscalatedOn = DateTime.UtcNow
        });
        dbContext.Tickets.Add(new Ticket
        {
            CustomerId = customer.Id,
            Title = "Non-escalated ticket",
            Category = TicketCategory.General,
            Priority = TicketPriority.Low
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketsListQueryHandler(dbContext, new StubIdentityQueryService());
        var result = await handler.Handle(new GetTicketsListQuery(IsEscalated: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Escalated ticket", item.Title);
        Assert.True(item.IsEscalated);
    }
}
