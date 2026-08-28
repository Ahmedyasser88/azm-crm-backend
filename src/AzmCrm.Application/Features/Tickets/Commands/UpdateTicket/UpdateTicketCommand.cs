using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;

public sealed record UpdateTicketCommand(
    Guid Id,
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority
) : IRequest<Result>;
