using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record EscalationRuleListItemDto(
    Guid Id, string Name, TicketPriority? Priority, int OverdueMinutes, bool IsActive);
