using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Domain.Features.Communications;

public sealed class Conversation : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public string? Subject { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    public Customer Customer { get; init; } = null!;
}
