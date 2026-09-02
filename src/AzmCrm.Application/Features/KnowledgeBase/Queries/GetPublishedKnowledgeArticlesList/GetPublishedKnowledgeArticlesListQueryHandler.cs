using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticlesList;

internal sealed class GetPublishedKnowledgeArticlesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetPublishedKnowledgeArticlesListQuery, Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>
{
    public async Task<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>> Handle(
        GetPublishedKnowledgeArticlesListQuery request, CancellationToken ct)
    {
        // Status == Published is always applied, not optional — this is the customer-facing
        // list, a Draft article must never appear here regardless of other filters.
        var query = dbContext.KnowledgeArticles.Where(a => a.Status == KnowledgeArticleStatus.Published);

        if (request.Type is not null)
            query = query.Where(a => a.Type == request.Type);

        if (request.Category is not null)
            query = query.Where(a => a.Category == request.Category);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.PublishedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new KnowledgeArticlePublicListItemDto(a.Id, a.Title, a.Type, a.Category, a.PublishedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<KnowledgeArticlePublicListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>.Success(result);
    }
}
