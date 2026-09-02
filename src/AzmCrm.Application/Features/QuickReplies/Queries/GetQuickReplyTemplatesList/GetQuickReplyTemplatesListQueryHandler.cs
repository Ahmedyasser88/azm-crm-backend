using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;

internal sealed class GetQuickReplyTemplatesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetQuickReplyTemplatesListQuery, Result<PaginatedResult<QuickReplyTemplateListItemDto>>>
{
    public async Task<Result<PaginatedResult<QuickReplyTemplateListItemDto>>> Handle(
        GetQuickReplyTemplatesListQuery request, CancellationToken ct)
    {
        var query = dbContext.QuickReplyTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(term) || t.Body.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            // Alphabetical by Title, not newest-first — templates are picked from a dropdown/
            // picker, not read as a chronological feed. See Story Goal, outcome 3.
            .OrderBy(t => t.Title)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new QuickReplyTemplateListItemDto(t.Id, t.Title, t.Body, t.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<QuickReplyTemplateListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<QuickReplyTemplateListItemDto>>.Success(result);
    }
}
