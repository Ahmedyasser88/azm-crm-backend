namespace AzmCrm.Domain.Features.Tickets;

public enum TicketHistoryEventType
{
    Created,
    Updated,
    Assigned,
    Unassigned,
    StatusChanged,
    Escalated
}
