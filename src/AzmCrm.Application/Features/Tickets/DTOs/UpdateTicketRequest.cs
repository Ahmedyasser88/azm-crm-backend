using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record UpdateTicketRequest(
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority
);
