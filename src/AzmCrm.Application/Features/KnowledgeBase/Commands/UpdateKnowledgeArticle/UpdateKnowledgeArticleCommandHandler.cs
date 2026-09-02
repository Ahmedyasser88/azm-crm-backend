using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;

internal sealed class UpdateKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        article.Title = request.Title;
        article.Content = request.Content;
        article.Type = request.Type;
        article.Category = request.Category;
        article.Tags = request.Tags;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
