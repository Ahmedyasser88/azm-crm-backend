using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record UpdateAssignmentRuleRequest(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder, bool IsActive);
