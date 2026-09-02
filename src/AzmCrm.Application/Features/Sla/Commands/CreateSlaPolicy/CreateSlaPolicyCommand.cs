using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;

public sealed record CreateSlaPolicyCommand(
    string Name, TicketPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes)
    : IRequest<Result<Guid>>;
