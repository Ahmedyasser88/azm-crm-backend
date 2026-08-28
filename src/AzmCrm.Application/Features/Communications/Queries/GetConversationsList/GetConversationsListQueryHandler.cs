using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationsList;

internal sealed class GetConversationsListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationsListQuery, Result<PaginatedResult<ConversationListItemDto>>>
{
    public async Task<Result<PaginatedResult<ConversationListItemDto>>> Handle(
        GetConversationsListQuery request, CancellationToken ct)
    {
        var query = dbContext.Conversations.AsQueryable();

        if (request.CustomerId is not null)
            query = query.Where(c => c.CustomerId == request.CustomerId);

        if (request.Channel is not null)
            query = query.Where(c => c.Channel == request.Channel);

        if (request.Status is not null)
            query = query.Where(c => c.Status == request.Status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ConversationListItemDto(
                c.Id, c.CustomerId, c.Channel, c.Subject, c.Status, c.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<ConversationListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<ConversationListItemDto>>.Success(result);
    }
}
