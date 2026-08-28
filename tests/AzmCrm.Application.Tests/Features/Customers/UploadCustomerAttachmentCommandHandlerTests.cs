using AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class UploadCustomerAttachmentCommandHandlerTests
{
    [Fact]
    public async Task Upload_for_existing_customer_persists_metadata_and_calls_storage()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var fileStorage = new StubFileStorageService();
        var handler = new UploadCustomerAttachmentCommandHandler(dbContext, fileStorage);

        await using var content = new MemoryStream([1, 2, 3]);
        var command = new UploadCustomerAttachmentCommand(
            customer.Id, "invoice.pdf", "application/pdf", 3, content);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(fileStorage.SaveAsyncWasCalled);

        var persisted = await dbContext.CustomerAttachments.SingleAsync();
        Assert.Equal(result.Data, persisted.Id);
        Assert.Equal(customer.Id, persisted.CustomerId);
        Assert.Equal("invoice.pdf", persisted.FileName);
        Assert.Equal("application/pdf", persisted.ContentType);
        Assert.Equal(3, persisted.FileSizeBytes);
    }

    [Fact]
    public async Task Upload_for_missing_customer_throws_NotFoundException_and_does_not_call_storage()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var fileStorage = new StubFileStorageService();
        var handler = new UploadCustomerAttachmentCommandHandler(dbContext, fileStorage);

        await using var content = new MemoryStream([1, 2, 3]);
        var command = new UploadCustomerAttachmentCommand(
            Guid.NewGuid(), "invoice.pdf", "application/pdf", 3, content);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));

        Assert.False(fileStorage.SaveAsyncWasCalled);
    }
}
