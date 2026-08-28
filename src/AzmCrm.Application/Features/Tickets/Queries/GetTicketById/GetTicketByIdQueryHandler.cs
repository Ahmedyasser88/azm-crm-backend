using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketById;

internal sealed class GetTicketByIdQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketByIdQuery, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(GetTicketByIdQuery request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Ticket '{request.Id}' was not found.");

        string? assignedToUserName = null;
        if (ticket.AssignedToUserId is not null)
        {
            var (fullName, _) = await identityQueryService.GetUserInfoAsync(ticket.AssignedToUserId.Value, ct);
            assignedToUserName = fullName;
        }

        var dto = new TicketDto(
            ticket.Id, ticket.CustomerId, ticket.Title, ticket.Description, ticket.Category,
            ticket.Priority, ticket.Status, ticket.CreatedOn, ticket.UpdatedOn,
            ticket.AssignedToUserId, assignedToUserName,
            ticket.IsEscalated, ticket.EscalatedOn);

        return Result<TicketDto>.Success(dto);
    }
}
