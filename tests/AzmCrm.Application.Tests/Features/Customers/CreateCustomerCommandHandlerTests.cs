using AzmCrm.Application.Features.Customers.Commands.CreateCustomer;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_customer_and_returns_new_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateCustomerCommandHandler(dbContext);

        var command = new CreateCustomerCommand(
            "Jane Doe", "Acme Inc", "jane@acme.com", "0501234567",
            "123 Main St", null, "Riyadh", "Riyadh Province", "12345", "SA");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);

        var persisted = await dbContext.Customers.SingleAsync();
        Assert.Equal(result.Data, persisted.Id);
        Assert.Equal("Jane Doe", persisted.FullName);
        Assert.Equal("Acme Inc", persisted.CompanyName);
        Assert.Equal("jane@acme.com", persisted.Email);
        Assert.Equal("0501234567", persisted.PhoneNumber);
    }
}
