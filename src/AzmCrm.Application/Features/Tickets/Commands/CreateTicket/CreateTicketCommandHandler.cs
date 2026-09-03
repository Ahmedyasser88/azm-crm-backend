using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicket;

internal sealed class CreateTicketCommandHandler(IApplicationDbContext dbContext, IIncomingTicketCategorizer categorizer)
    : IRequestHandler<CreateTicketCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        // When the caller omits Category, classify it from Title/Description instead of
        // requiring one — see Story 27 (KAN-7). CategorizeAsync never throws; it falls back to
        // TicketCategory.General on any AI failure, so ticket creation is never blocked by it.
        var category = request.Category
            ?? await categorizer.CategorizeAsync(request.Title, request.Description, ct);

        var ticket = new Ticket
        {
            CustomerId = request.CustomerId,
            Title = request.Title,
            Description = request.Description,
            Category = category,
            Priority = request.Priority
        };

        var slaPolicy = await dbContext.SlaPolicies
            .FirstOrDefaultAsync(p => p.Priority == request.Priority && p.IsActive, ct);

        if (slaPolicy is not null)
        {
            ticket.SlaPolicyId = slaPolicy.Id;
            ticket.ResponseDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResponseTimeMinutes);
            ticket.ResolutionDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResolutionTimeMinutes);
        }

        var assignmentRule = await dbContext.AssignmentRules
            .Where(r => r.IsActive)
            .Where(r => r.Category == null || r.Category == category)
            .Where(r => r.Priority == null || r.Priority == request.Priority)
            .OrderBy(r => r.EvaluationOrder)
            .FirstOrDefaultAsync(ct);

        if (assignmentRule is not null)
            ticket.AssignedToUserId = assignmentRule.AssignedToUserId;

        dbContext.Tickets.Add(ticket);

        dbContext.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            EventType = TicketHistoryEventType.Created,
            Description = "Ticket created."
        });

        if (assignmentRule is not null)
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.Assigned,
                Description = $"Ticket auto-assigned by rule '{assignmentRule.Name}'.",
                OldValue = null,
                NewValue = assignmentRule.AssignedToUserId.ToString()
            });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(ticket.Id);
    }
}
