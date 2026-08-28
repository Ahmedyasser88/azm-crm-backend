using AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class GetCustomerAttachmentsQueryHandlerTests
{
    [Fact]
    public async Task List_returns_attachments_ordered_by_CreatedOn_desc()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        dbContext.CustomerAttachments.AddRange(
            new CustomerAttachment
            {
                CustomerId = customer.Id, FileName = "older.pdf", ContentType = "application/pdf",
                FileSizeBytes = 10, StorageKey = "key-older", CreatedOn = DateTime.UtcNow.AddDays(-1)
            },
            new CustomerAttachment
            {
                CustomerId = customer.Id, FileName = "newer.pdf", ContentType = "application/pdf",
                FileSizeBytes = 20, StorageKey = "key-newer", CreatedOn = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var handler = new GetCustomerAttachmentsQueryHandler(dbContext);
        var result = await handler.Handle(new GetCustomerAttachmentsQuery(customer.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("newer.pdf", result.Data.Items.First().FileName);
    }

    [Fact]
    public async Task List_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetCustomerAttachmentsQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetCustomerAttachmentsQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
