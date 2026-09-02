using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;

internal sealed class UnpublishKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UnpublishKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UnpublishKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        if (article.Status != KnowledgeArticleStatus.Draft)
        {
            article.Status = KnowledgeArticleStatus.Draft;
            article.PublishedOn = null;
            article.PublishedBy = null;

            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
