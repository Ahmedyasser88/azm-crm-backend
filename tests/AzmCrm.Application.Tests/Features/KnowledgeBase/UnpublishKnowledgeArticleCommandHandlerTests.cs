using AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class UnpublishKnowledgeArticleCommandHandlerTests
{
    [Fact]
    public async Task Unpublish_Published_article_sets_Draft_status_and_clears_PublishedOn_and_PublishedBy()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = DateTime.UtcNow,
            PublishedBy = Guid.NewGuid()
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new UnpublishKnowledgeArticleCommandHandler(dbContext);

        var result = await handler.Handle(new UnpublishKnowledgeArticleCommand(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(KnowledgeArticleStatus.Draft, persisted.Status);
        Assert.Null(persisted.PublishedOn);
        Assert.Null(persisted.PublishedBy);
    }

    [Fact]
    public async Task Unpublish_already_Draft_article_is_idempotent_noop()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Faq };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new UnpublishKnowledgeArticleCommandHandler(dbContext);

        var result = await handler.Handle(new UnpublishKnowledgeArticleCommand(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(KnowledgeArticleStatus.Draft, persisted.Status);
    }

    [Fact]
    public async Task Unpublish_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UnpublishKnowledgeArticleCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new UnpublishKnowledgeArticleCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
