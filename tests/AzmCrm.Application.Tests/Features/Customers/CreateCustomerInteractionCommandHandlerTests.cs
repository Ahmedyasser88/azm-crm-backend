using AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerInteractionCommandHandlerTests
{
    [Fact]
    public async Task Create_interaction_for_existing_customer_persists_row()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateCustomerInteractionCommandHandler(dbContext);
        var command = new CreateCustomerInteractionCommand(
            customer.Id, InteractionType.Call, "Follow-up", "Discussed renewal", DateTime.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.CustomerInteractions.SingleAsync();
        Assert.Equal(result.Data, persisted.Id);
        Assert.Equal(customer.Id, persisted.CustomerId);
        Assert.Equal(InteractionType.Call, persisted.Type);
        Assert.Equal("Follow-up", persisted.Subject);
    }

    [Fact]
    public async Task Create_interaction_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateCustomerInteractionCommandHandler(dbContext);

        var command = new CreateCustomerInteractionCommand(
            Guid.NewGuid(), InteractionType.Call, "Follow-up", null, DateTime.UtcNow);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
