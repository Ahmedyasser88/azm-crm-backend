using AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class DeleteKnowledgeArticleCommandHandlerTests
{
    [Fact]
    public async Task Delete_soft_deletes_article()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Faq
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteKnowledgeArticleCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(new DeleteKnowledgeArticleCommand(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.IgnoreQueryFilters().SingleAsync(a => a.Id == article.Id);
        Assert.True(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteKnowledgeArticleCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteKnowledgeArticleCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
