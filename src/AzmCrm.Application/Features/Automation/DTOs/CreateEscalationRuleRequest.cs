using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record CreateEscalationRuleRequest(string Name, TicketPriority? Priority, int OverdueMinutes);
