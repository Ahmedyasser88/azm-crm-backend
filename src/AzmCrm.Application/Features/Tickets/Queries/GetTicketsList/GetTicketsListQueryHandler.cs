using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketsList;

internal sealed class GetTicketsListQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketsListQuery, Result<PaginatedResult<TicketListItemDto>>>
{
    public async Task<Result<PaginatedResult<TicketListItemDto>>> Handle(
        GetTicketsListQuery request, CancellationToken ct)
    {
        var query = dbContext.Tickets.AsQueryable();

        if (request.CustomerId is not null)
            query = query.Where(t => t.CustomerId == request.CustomerId);

        if (request.Status is not null)
            query = query.Where(t => t.Status == request.Status);

        if (request.Category is not null)
            query = query.Where(t => t.Category == request.Category);

        if (request.Priority is not null)
            query = query.Where(t => t.Priority == request.Priority);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        if (request.AssignedToUserId is not null)
            query = query.Where(t => t.AssignedToUserId == request.AssignedToUserId);

        if (request.IsEscalated is not null)
            query = query.Where(t => t.IsEscalated == request.IsEscalated);

        var totalCount = await query.CountAsync(ct);

        var tickets = await query
            .OrderByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var assigneeIds = tickets.Where(t => t.AssignedToUserId is not null)
            .Select(t => t.AssignedToUserId!.Value);
        var assigneeNames = await identityQueryService.GetUsersInfoAsync(assigneeIds, ct);

        var items = tickets.Select(t => new TicketListItemDto(
            t.Id, t.CustomerId, t.Title, t.Category, t.Priority, t.Status, t.CreatedOn,
            t.AssignedToUserId,
            t.AssignedToUserId is not null && assigneeNames.TryGetValue(t.AssignedToUserId.Value, out var info)
                ? info.FullName
                : null,
            t.IsEscalated, t.EscalatedOn));

        var result = new PaginatedResult<TicketListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<TicketListItemDto>>.Success(result);
    }
}
