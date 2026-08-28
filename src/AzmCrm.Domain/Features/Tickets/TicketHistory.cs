using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Tickets;

public sealed class TicketHistory : BaseEntity
{
    public required Guid TicketId { get; init; }
    public required TicketHistoryEventType EventType { get; set; }
    public required string Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public Ticket Ticket { get; init; } = null!;
}
