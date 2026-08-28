using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketDto(
    Guid Id,
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    DateTime? UpdatedOn,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    bool IsEscalated,
    DateTime? EscalatedOn
);
