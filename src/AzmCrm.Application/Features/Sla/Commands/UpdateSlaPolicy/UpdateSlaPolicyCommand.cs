using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;

public sealed record UpdateSlaPolicyCommand(
    Guid Id, string Name, TicketPriority Priority,
    int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive) : IRequest<Result>;
