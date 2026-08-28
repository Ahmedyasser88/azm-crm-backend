using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.AssignTicket;

public sealed record AssignTicketCommand(Guid TicketId, Guid? AssignedToUserId) : IRequest<Result>;
