using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;

internal sealed class CreateKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateKnowledgeArticleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = new KnowledgeArticle
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            Category = request.Category,
            Tags = request.Tags
        };

        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(article.Id);
    }
}
