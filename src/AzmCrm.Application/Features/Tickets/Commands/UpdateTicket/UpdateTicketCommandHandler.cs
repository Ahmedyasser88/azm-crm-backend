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

        var priorityChanged = ticket.Priority != request.Priority;

        if (priorityChanged)
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

        // Re-stamp SLA due dates against the new priority's active policy, mirroring
        // CreateTicketCommandHandler's own lookup so a ticket's SLA tracking always reflects
        // its current priority rather than staying pinned to whatever was active at creation.
        if (priorityChanged)
        {
            var slaPolicy = await dbContext.SlaPolicies
                .FirstOrDefaultAsync(p => p.Priority == request.Priority && p.IsActive, ct);

            if (slaPolicy is not null)
            {
                ticket.SlaPolicyId = slaPolicy.Id;
                ticket.ResponseDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResponseTimeMinutes);
                ticket.ResolutionDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResolutionTimeMinutes);
            }
            else
            {
                ticket.SlaPolicyId = null;
                ticket.ResponseDueOn = null;
                ticket.ResolutionDueOn = null;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
