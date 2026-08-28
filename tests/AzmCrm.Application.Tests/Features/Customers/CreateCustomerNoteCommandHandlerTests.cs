using AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerNoteCommandHandlerTests
{
    [Fact]
    public async Task Create_note_for_existing_customer_persists_row()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateCustomerNoteCommandHandler(dbContext);
        var command = new CreateCustomerNoteCommand(customer.Id, "Called about renewal");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.CustomerNotes.SingleAsync();
        Assert.Equal(result.Data, persisted.Id);
        Assert.Equal(customer.Id, persisted.CustomerId);
        Assert.Equal("Called about renewal", persisted.Content);
    }

    [Fact]
    public async Task Create_note_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateCustomerNoteCommandHandler(dbContext);

        var command = new CreateCustomerNoteCommand(Guid.NewGuid(), "Some note");

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
