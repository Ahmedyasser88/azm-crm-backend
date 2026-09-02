using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record CreateAssignmentRuleRequest(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder);
