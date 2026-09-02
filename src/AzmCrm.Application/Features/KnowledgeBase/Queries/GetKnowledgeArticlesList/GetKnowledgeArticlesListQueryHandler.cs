using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticlesList;

internal sealed class GetKnowledgeArticlesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetKnowledgeArticlesListQuery, Result<PaginatedResult<KnowledgeArticleListItemDto>>>
{
    public async Task<Result<PaginatedResult<KnowledgeArticleListItemDto>>> Handle(
        GetKnowledgeArticlesListQuery request, CancellationToken ct)
    {
        var query = dbContext.KnowledgeArticles.AsQueryable();

        if (request.Type is not null)
            query = query.Where(a => a.Type == request.Type);

        if (request.Status is not null)
            query = query.Where(a => a.Status == request.Status);

        if (request.Category is not null)
            query = query.Where(a => a.Category == request.Category);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            // Newest-first — a management list of freshly authored/edited content, unlike SLA's
            // small fixed-size priority-keyed list. See Story Goal, outcome 3.
            .OrderByDescending(a => a.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new KnowledgeArticleListItemDto(a.Id, a.Title, a.Type, a.Status, a.Category, a.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<KnowledgeArticleListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<KnowledgeArticleListItemDto>>.Success(result);
    }
}
