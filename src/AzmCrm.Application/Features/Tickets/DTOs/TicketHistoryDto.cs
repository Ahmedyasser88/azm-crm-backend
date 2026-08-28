using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketHistoryDto(
    Guid Id,
    Guid TicketId,
    TicketHistoryEventType EventType,
    string Description,
    string? OldValue,
    string? NewValue,
    Guid CreatedBy,
    DateTime CreatedOn
);
