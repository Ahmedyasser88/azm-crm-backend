using AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class GetCustomerAttachmentContentQueryHandlerTests
{
    [Fact]
    public async Task Download_returns_content_for_matching_customer_and_attachment()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var fileStorage = new StubFileStorageService();
        var storageKey = await fileStorage.SaveAsync(new MemoryStream([1, 2, 3]), "invoice.pdf");

        var attachment = new CustomerAttachment
        {
            CustomerId = customer.Id, FileName = "invoice.pdf", ContentType = "application/pdf",
            FileSizeBytes = 3, StorageKey = storageKey
        };
        dbContext.CustomerAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerAttachmentContentQueryHandler(dbContext, fileStorage);
        var result = await handler.Handle(
            new GetCustomerAttachmentContentQuery(customer.Id, attachment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("invoice.pdf", result.Data!.FileName);
        Assert.Equal("application/pdf", result.Data.ContentType);
    }

    [Fact]
    public async Task Download_throws_NotFoundException_when_attachment_belongs_to_different_customer()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        var otherCustomer = new Customer { FullName = "John Smith" };
        dbContext.Customers.AddRange(customer, otherCustomer);
        await dbContext.SaveChangesAsync();

        var fileStorage = new StubFileStorageService();
        var storageKey = await fileStorage.SaveAsync(new MemoryStream([1, 2, 3]), "invoice.pdf");

        var attachment = new CustomerAttachment
        {
            CustomerId = customer.Id, FileName = "invoice.pdf", ContentType = "application/pdf",
            FileSizeBytes = 3, StorageKey = storageKey
        };
        dbContext.CustomerAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerAttachmentContentQueryHandler(dbContext, fileStorage);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetCustomerAttachmentContentQuery(otherCustomer.Id, attachment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Download_throws_NotFoundException_when_attachment_missing()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerAttachmentContentQueryHandler(dbContext, new StubFileStorageService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetCustomerAttachmentContentQuery(customer.Id, Guid.NewGuid()), CancellationToken.None));
    }
}
