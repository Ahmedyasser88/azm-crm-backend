using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketListItemDto(
    Guid Id,
    Guid CustomerId,
    string Title,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    bool IsEscalated,
    DateTime? EscalatedOn,
    Guid? SlaPolicyId,
    DateTime? ResponseDueOn,
    DateTime? ResolutionDueOn,
    DateTime? RespondedOn
);
