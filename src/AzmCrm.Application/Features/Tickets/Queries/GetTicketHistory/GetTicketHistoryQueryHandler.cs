using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketHistory;

internal sealed class GetTicketHistoryQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetTicketHistoryQuery, Result<PaginatedResult<TicketHistoryDto>>>
{
    public async Task<Result<PaginatedResult<TicketHistoryDto>>> Handle(
        GetTicketHistoryQuery request, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct);
        if (!ticketExists)
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var query = dbContext.TicketHistories.Where(h => h.TicketId == request.TicketId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(h => h.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(h => new TicketHistoryDto(
                h.Id, h.TicketId, h.EventType, h.Description, h.OldValue, h.NewValue,
                h.CreatedBy, h.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<TicketHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<TicketHistoryDto>>.Success(result);
    }
}
