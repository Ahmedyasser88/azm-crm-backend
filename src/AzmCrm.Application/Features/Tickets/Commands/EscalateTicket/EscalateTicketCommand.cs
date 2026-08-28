using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

public sealed record EscalateTicketCommand(Guid TicketId, string? Reason) : IRequest<Result>;
