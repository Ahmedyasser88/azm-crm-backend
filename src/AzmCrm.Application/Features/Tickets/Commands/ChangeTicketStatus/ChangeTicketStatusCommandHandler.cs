using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

internal sealed class ChangeTicketStatusCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ChangeTicketStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        if (ticket.Status != request.Status)
        {
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.StatusChanged,
                Description = $"Status changed from {ticket.Status} to {request.Status}.",
                OldValue = ticket.Status.ToString(),
                NewValue = request.Status.ToString()
            });

            if (ticket.RespondedOn is null && ticket.Status == TicketStatus.New)
                ticket.RespondedOn = DateTime.UtcNow;

            ticket.Status = request.Status;
        }

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
