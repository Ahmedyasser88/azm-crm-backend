using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record AssignmentRuleListItemDto(
    Guid Id, string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, string? AssignedToUserName, int EvaluationOrder, bool IsActive);
