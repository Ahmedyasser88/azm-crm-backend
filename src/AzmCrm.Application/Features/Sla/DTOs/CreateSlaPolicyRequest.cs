using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record CreateSlaPolicyRequest(
    string Name, TicketPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes);
