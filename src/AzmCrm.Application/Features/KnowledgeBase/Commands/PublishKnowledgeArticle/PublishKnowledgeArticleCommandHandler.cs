using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;

internal sealed class PublishKnowledgeArticleCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<PublishKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(PublishKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        if (article.Status != KnowledgeArticleStatus.Published)
        {
            article.Status = KnowledgeArticleStatus.Published;
            article.PublishedOn = DateTime.UtcNow;
            article.PublishedBy = currentUserService.UserId ?? Guid.Empty;

            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
