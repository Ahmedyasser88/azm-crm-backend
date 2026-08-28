using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(Guid TicketId, TicketStatus Status) : IRequest<Result>;
