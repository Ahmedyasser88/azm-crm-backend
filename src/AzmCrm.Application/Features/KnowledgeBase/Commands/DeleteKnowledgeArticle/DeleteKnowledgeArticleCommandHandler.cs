using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;

internal sealed class DeleteKnowledgeArticleCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        article.IsDeleted = true;
        article.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        article.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
