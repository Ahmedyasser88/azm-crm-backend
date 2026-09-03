using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

internal sealed class SuggestKnowledgeArticlesForTicketQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SuggestKnowledgeArticlesForTicketQuery, Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>>
{
    public async Task<Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>> Handle(
        SuggestKnowledgeArticlesForTicketQuery request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        // Same multi-field Contains match SearchKnowledgeArticlesQueryHandler uses (KAN-6 Story
        // 24), keyed on the ticket's own Title instead of a user-supplied search string, and
        // capped to MaxResults instead of paginated — see Story 28 (KAN-7) for the reasoning.
        var term = ticket.Title.Trim().ToLower();

        var matches = await dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Content.ToLower().Contains(term) ||
                (a.Category != null && a.Category.ToLower().Contains(term)) ||
                (a.Tags != null && a.Tags.ToLower().Contains(term)) ||
                dbContext.KnowledgeArticleSteps.Any(s =>
                    s.KnowledgeArticleId == a.Id &&
                    (s.Title.ToLower().Contains(term) || s.Description.ToLower().Contains(term))))
            .OrderByDescending(a => a.PublishedOn)
            .Take(request.MaxResults)
            .Select(a => new KnowledgeArticlePublicListItemDto(a.Id, a.Title, a.Type, a.Category, a.PublishedOn))
            .ToListAsync(ct);

        return Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>.Success(matches);
    }
}
