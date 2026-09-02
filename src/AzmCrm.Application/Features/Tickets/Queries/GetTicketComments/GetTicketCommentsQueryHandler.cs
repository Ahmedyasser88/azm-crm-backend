using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;

internal sealed class GetTicketCommentsQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketCommentsQuery, Result<PaginatedResult<TicketCommentDto>>>
{
    public async Task<Result<PaginatedResult<TicketCommentDto>>> Handle(
        GetTicketCommentsQuery request, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct);
        if (!ticketExists)
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var query = dbContext.TicketComments.Where(c => c.TicketId == request.TicketId);

        var totalCount = await query.CountAsync(ct);

        var comments = await query
            .OrderBy(c => c.CreatedOn) // oldest first — collaboration thread reading order, see Story Goal
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var authorIds = comments.Select(c => c.CreatedBy).Distinct();
        var authorNames = await identityQueryService.GetUsersInfoAsync(authorIds, ct);

        var items = comments.Select(c => new TicketCommentDto(
            c.Id, c.TicketId, c.Content, c.CreatedBy,
            authorNames.TryGetValue(c.CreatedBy, out var info) ? info.FullName : null,
            c.CreatedOn));

        var result = new PaginatedResult<TicketCommentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<TicketCommentDto>>.Success(result);
    }
}
