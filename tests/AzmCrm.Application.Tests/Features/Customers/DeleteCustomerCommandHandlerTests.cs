using AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerById;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class DeleteCustomerCommandHandlerTests
{
    [Fact]
    public async Task Delete_sets_IsDeleted_and_DeletedBy_DeletedOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "To Delete" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var currentUser = new StubCurrentUserService();
        var handler = new DeleteCustomerCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new DeleteCustomerCommand(customer.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The Customer query filter (!IsDeleted) means a direct EF query no longer returns
        // this row; use IgnoreQueryFilters to inspect the soft-deleted row's fields directly.
        var persisted = await dbContext.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customer.Id);
        Assert.True(persisted.IsDeleted);
        Assert.Equal(currentUser.UserId, persisted.DeletedBy);
        Assert.NotNull(persisted.DeletedOn);
    }

    [Fact]
    public async Task Delete_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteCustomerCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteCustomerCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Deleted_customer_is_excluded_from_GetById()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "To Delete" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var deleteHandler = new DeleteCustomerCommandHandler(dbContext, new StubCurrentUserService());
        await deleteHandler.Handle(new DeleteCustomerCommand(customer.Id), CancellationToken.None);

        var getByIdHandler = new GetCustomerByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => getByIdHandler.Handle(new GetCustomerByIdQuery(customer.Id), CancellationToken.None));
    }
}
