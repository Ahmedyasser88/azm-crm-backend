using AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class GetCustomerInteractionsQueryHandlerTests
{
    [Fact]
    public async Task List_returns_interactions_ordered_by_OccurredOn_desc()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        dbContext.CustomerInteractions.AddRange(
            new CustomerInteraction
            {
                CustomerId = customer.Id, Type = InteractionType.Call, Subject = "Older",
                OccurredOn = DateTime.UtcNow.AddDays(-1)
            },
            new CustomerInteraction
            {
                CustomerId = customer.Id, Type = InteractionType.Email, Subject = "Newer",
                OccurredOn = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerInteractionsQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomerInteractionsQuery(customer.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("Newer", result.Data.Items.First().Subject);
    }

    [Fact]
    public async Task List_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetCustomerInteractionsQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetCustomerInteractionsQuery(Guid.NewGuid()), CancellationToken.None));
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
            dbContext.CustomerInteractions.Add(new CustomerInteraction
            {
                CustomerId = customer.Id,
                Type = InteractionType.Other,
                Subject = $"Interaction {i}",
                OccurredOn = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerInteractionsQueryHandler(dbContext);
        var result = await handler.Handle(
            new GetCustomerInteractionsQuery(customer.Id, PageNumber: 1, PageSize: 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }
}
