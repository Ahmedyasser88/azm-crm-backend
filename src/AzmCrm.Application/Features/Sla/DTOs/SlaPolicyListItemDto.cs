using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record SlaPolicyListItemDto(
    Guid Id, string Name, TicketPriority Priority,
    int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive);
