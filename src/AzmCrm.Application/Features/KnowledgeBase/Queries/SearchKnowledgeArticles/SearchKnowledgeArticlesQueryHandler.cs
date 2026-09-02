using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;

internal sealed class SearchKnowledgeArticlesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SearchKnowledgeArticlesQuery, Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>
{
    public async Task<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>> Handle(
        SearchKnowledgeArticlesQuery request, CancellationToken ct)
    {
        var term = request.Query.Trim().ToLower();

        // Matches on the parent article's own text fields, or on any of its steps' text fields
        // (KAN-6 asks for search "across all knowledge base content", which this codebase's
        // model expresses as KnowledgeArticle + child KnowledgeArticleStep rows). Draft articles
        // are excluded the same way GetPublishedKnowledgeArticlesListQueryHandler excludes them:
        // Status == Published is a fixed clause, not an optional filter.
        var query = dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Content.ToLower().Contains(term) ||
                (a.Category != null && a.Category.ToLower().Contains(term)) ||
                (a.Tags != null && a.Tags.ToLower().Contains(term)) ||
                dbContext.KnowledgeArticleSteps.Any(s =>
                    s.KnowledgeArticleId == a.Id &&
                    (s.Title.ToLower().Contains(term) || s.Description.ToLower().Contains(term))));

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
