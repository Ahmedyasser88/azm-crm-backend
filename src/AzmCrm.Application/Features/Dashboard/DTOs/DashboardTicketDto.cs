using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record DashboardTicketDto(
    Guid Id,
    string Title,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    bool IsEscalated,
    DateTime? EscalatedOn,
    CustomerSummaryDto? Customer
);
