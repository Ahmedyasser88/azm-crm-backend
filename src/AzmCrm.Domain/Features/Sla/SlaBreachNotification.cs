using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Sla;

public sealed class SlaBreachNotification : BaseEntity
{
    public required Guid TicketId { get; init; }
    public required SlaBreachType BreachType { get; init; }
    public Guid? NotifiedUserId { get; init; }
    public required string Message { get; init; }
    public bool EmailSent { get; set; }
}
