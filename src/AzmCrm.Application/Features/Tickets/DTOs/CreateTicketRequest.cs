using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record CreateTicketRequest(
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory? Category,
    TicketPriority Priority
);
