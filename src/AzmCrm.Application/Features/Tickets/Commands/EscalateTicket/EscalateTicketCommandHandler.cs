using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

internal sealed class EscalateTicketCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<EscalateTicketCommand, Result>
{
    public async Task<Result> Handle(EscalateTicketCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        ticket.IsEscalated = true;
        ticket.EscalatedOn = DateTime.UtcNow;

        dbContext.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            EventType = TicketHistoryEventType.Escalated,
            Description = string.IsNullOrWhiteSpace(request.Reason)
                ? "Ticket escalated."
                : $"Ticket escalated: {request.Reason}"
        });

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
