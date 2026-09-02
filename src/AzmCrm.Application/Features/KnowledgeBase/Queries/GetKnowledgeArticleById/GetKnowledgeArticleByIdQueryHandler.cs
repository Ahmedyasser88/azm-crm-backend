using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;

internal sealed class GetKnowledgeArticleByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetKnowledgeArticleByIdQuery, Result<KnowledgeArticleDto>>
{
    public async Task<Result<KnowledgeArticleDto>> Handle(
        GetKnowledgeArticleByIdQuery request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        var steps = await dbContext.KnowledgeArticleSteps
            .Where(s => s.KnowledgeArticleId == article.Id)
            .OrderBy(s => s.StepNumber)
            .Select(s => new KnowledgeArticleStepDto(s.Id, s.StepNumber, s.Title, s.Description))
            .ToListAsync(ct);

        var dto = new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.Type, article.Status,
            article.Category, article.Tags, article.PublishedOn, article.PublishedBy,
            article.CreatedOn, article.UpdatedOn, steps);

        return Result<KnowledgeArticleDto>.Success(dto);
    }
}
