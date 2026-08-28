using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;

internal sealed class UpdateTicketCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateTicketCommand, Result>
{
    public async Task<Result> Handle(UpdateTicketCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Ticket '{request.Id}' was not found.");

        if (ticket.Title != request.Title)
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.Updated,
                Description = "Title changed.",
                OldValue = ticket.Title,
                NewValue = request.Title
            });

        if (ticket.Category != request.Category)
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.Updated,
                Description = "Category changed.",
                OldValue = ticket.Category.ToString(),
                NewValue = request.Category.ToString()
            });

        if (ticket.Priority != request.Priority)
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.Updated,
                Description = "Priority changed.",
                OldValue = ticket.Priority.ToString(),
                NewValue = request.Priority.ToString()
            });

        ticket.Title = request.Title;
        ticket.Description = request.Description;
        ticket.Category = request.Category;
        ticket.Priority = request.Priority;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
