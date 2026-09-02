using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Tickets;

public sealed class TicketComment : BaseEntity
{
    public required Guid TicketId { get; init; }
    public required string Content { get; set; }

    public Ticket Ticket { get; init; } = null!;
}
