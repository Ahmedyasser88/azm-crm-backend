using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Automation;

public sealed class AssignmentRule : BaseEntity
{
    public required string Name { get; set; }
    public TicketCategory? Category { get; set; }
    public TicketPriority? Priority { get; set; }
    public required Guid AssignedToUserId { get; set; }
    public int EvaluationOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
