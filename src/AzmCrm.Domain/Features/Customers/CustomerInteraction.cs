using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerInteraction : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required InteractionType Type { get; set; }
    public required string Subject { get; set; }
    public string? Description { get; set; }
    public required DateTime OccurredOn { get; set; }

    public Customer Customer { get; init; } = null!;
}
