using AzmCrm.Application.Features.Customers.Queries.GetCustomerNotes;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class GetCustomerNotesQueryHandlerTests
{
    [Fact]
    public async Task List_returns_notes_ordered_by_CreatedOn_desc()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        dbContext.CustomerNotes.AddRange(
            new CustomerNote { CustomerId = customer.Id, Content = "Older", CreatedOn = DateTime.UtcNow.AddDays(-1) },
            new CustomerNote { CustomerId = customer.Id, Content = "Newer", CreatedOn = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerNotesQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomerNotesQuery(customer.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("Newer", result.Data.Items.First().Content);
    }

    [Fact]
    public async Task List_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetCustomerNotesQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetCustomerNotesQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task List_is_paginated()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            dbContext.CustomerNotes.Add(new CustomerNote
            {
                CustomerId = customer.Id,
                Content = $"Note {i}",
                CreatedOn = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerNotesQueryHandler(dbContext);
        var result = await handler.Handle(
            new GetCustomerNotesQuery(customer.Id, PageNumber: 1, PageSize: 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }
}
