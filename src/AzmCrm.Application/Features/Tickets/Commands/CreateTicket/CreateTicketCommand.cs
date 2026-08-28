using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicket;

public sealed record CreateTicketCommand(
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority
) : IRequest<Result<Guid>>;
