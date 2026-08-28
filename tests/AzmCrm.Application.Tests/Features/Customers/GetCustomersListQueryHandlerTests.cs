using AzmCrm.Application.Features.Customers.Queries.GetCustomersList;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class GetCustomersListQueryHandlerTests
{
    [Fact]
    public async Task List_returns_paginated_results_ordered_by_CreatedOn_desc()
    {
        await using var dbContext = TestApplicationDbContext.Create();

        var older = new Customer { FullName = "Older", CreatedOn = DateTime.UtcNow.AddDays(-1) };
        var newer = new Customer { FullName = "Newer", CreatedOn = DateTime.UtcNow };
        dbContext.Customers.AddRange(older, newer);
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomersListQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomersListQuery(1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("Newer", result.Data.Items.First().FullName);
    }

    [Fact]
    public async Task List_filters_by_search_term_case_insensitively()
    {
        await using var dbContext = TestApplicationDbContext.Create();

        dbContext.Customers.AddRange(
            new Customer { FullName = "Jane Doe", Email = "jane@acme.com" },
            new Customer { FullName = "John Smith", Email = "john@example.com" });
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomersListQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomersListQuery(1, 20, "JANE"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Jane Doe", result.Data.Items.Single().FullName);
    }

    [Fact]
    public async Task List_with_blank_search_returns_all()
    {
        await using var dbContext = TestApplicationDbContext.Create();

        dbContext.Customers.AddRange(
            new Customer { FullName = "Jane Doe" },
            new Customer { FullName = "John Smith" });
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomersListQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomersListQuery(1, 20, "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
    }
}
