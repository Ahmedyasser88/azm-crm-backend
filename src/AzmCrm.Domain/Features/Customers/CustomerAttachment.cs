using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerAttachment : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string StorageKey { get; init; }

    public Customer Customer { get; init; } = null!;
}
