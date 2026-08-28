using AzmCrm.Application.Features.Customers.Commands.UpdateCustomer;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class UpdateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Update_existing_customer_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Old Name" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateCustomerCommandHandler(dbContext);
        var command = new UpdateCustomerCommand(
            customer.Id, "New Name", "New Co", "new@acme.com", "0509876543",
            null, null, null, null, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.Customers.SingleAsync(c => c.Id == customer.Id);
        Assert.Equal("New Name", persisted.FullName);
        Assert.Equal("New Co", persisted.CompanyName);
        Assert.Equal("new@acme.com", persisted.Email);
    }

    [Fact]
    public async Task Update_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateCustomerCommandHandler(dbContext);

        var command = new UpdateCustomerCommand(
            Guid.NewGuid(), "Name", null, null, null, null, null, null, null, null, null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
