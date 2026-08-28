using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerNote : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required string Content { get; set; }

    public Customer Customer { get; init; } = null!;
}
