using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Automation;

public sealed class EscalationRule : BaseEntity
{
    public required string Name { get; set; }
    public TicketPriority? Priority { get; set; }
    public required int OverdueMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
