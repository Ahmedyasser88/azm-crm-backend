using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Domain.Features.Tickets;

public sealed class Ticket : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required TicketCategory Category { get; set; }
    public required TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public Guid? AssignedToUserId { get; set; }
    public bool IsEscalated { get; set; }
    public DateTime? EscalatedOn { get; set; }

    public Customer Customer { get; init; } = null!;
}
