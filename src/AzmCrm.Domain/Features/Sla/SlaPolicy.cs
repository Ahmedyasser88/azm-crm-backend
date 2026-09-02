using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Sla;

public sealed class SlaPolicy : BaseEntity
{
    public required string Name { get; set; }
    public required TicketPriority Priority { get; set; }
    public required int ResponseTimeMinutes { get; set; }
    public required int ResolutionTimeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
