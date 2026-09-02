using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;

internal sealed class GetPublishedKnowledgeArticleByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetPublishedKnowledgeArticleByIdQuery, Result<KnowledgeArticlePublicDto>>
{
    public async Task<Result<KnowledgeArticlePublicDto>> Handle(
        GetPublishedKnowledgeArticleByIdQuery request, CancellationToken ct)
    {
        // The Status == Published condition is part of the lookup predicate itself (not a
        // post-fetch check) so a Draft article's id 404s exactly like a nonexistent id —
        // a customer must never learn a draft exists by probing ids.
        var article = await dbContext.KnowledgeArticles
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.Status == KnowledgeArticleStatus.Published, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        var steps = await dbContext.KnowledgeArticleSteps
            .Where(s => s.KnowledgeArticleId == article.Id)
            .OrderBy(s => s.StepNumber)
            .Select(s => new KnowledgeArticleStepDto(s.Id, s.StepNumber, s.Title, s.Description))
            .ToListAsync(ct);

        var dto = new KnowledgeArticlePublicDto(
            article.Id, article.Title, article.Content, article.Type,
            article.Category, article.Tags, article.PublishedOn, steps);

        return Result<KnowledgeArticlePublicDto>.Success(dto);
    }
}
