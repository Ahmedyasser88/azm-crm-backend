using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicket;

internal sealed class CreateTicketCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateTicketCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var ticket = new Ticket
        {
            CustomerId = request.CustomerId,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority
        };

        dbContext.Tickets.Add(ticket);

        dbContext.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            EventType = TicketHistoryEventType.Created,
            Description = "Ticket created."
        });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(ticket.Id);
    }
}
