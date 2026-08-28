using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.AssignTicket;

internal sealed class AssignTicketCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<AssignTicketCommand, Result>
{
    public async Task<Result> Handle(AssignTicketCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var previousAssignee = ticket.AssignedToUserId;

        if (request.AssignedToUserId is null)
        {
            if (previousAssignee is not null)
                dbContext.TicketHistories.Add(new TicketHistory
                {
                    TicketId = ticket.Id,
                    EventType = TicketHistoryEventType.Unassigned,
                    Description = "Ticket unassigned.",
                    OldValue = previousAssignee.ToString(),
                    NewValue = null
                });

            ticket.AssignedToUserId = null;
        }
        else
        {
            var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId.Value, ct);
            if (fullName is null)
                throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

            if (previousAssignee != request.AssignedToUserId)
                dbContext.TicketHistories.Add(new TicketHistory
                {
                    TicketId = ticket.Id,
                    EventType = TicketHistoryEventType.Assigned,
                    Description = $"Ticket assigned to {fullName}.",
                    OldValue = previousAssignee?.ToString(),
                    NewValue = request.AssignedToUserId.ToString()
                });

            ticket.AssignedToUserId = request.AssignedToUserId;
        }

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
