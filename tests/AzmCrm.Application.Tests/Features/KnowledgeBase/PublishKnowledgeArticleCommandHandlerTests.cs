using AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class PublishKnowledgeArticleCommandHandlerTests
{
    [Fact]
    public async Task Publish_Draft_article_sets_Published_status_and_stamps_PublishedOn_and_PublishedBy()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Faq };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var currentUser = new StubCurrentUserService();
        var handler = new PublishKnowledgeArticleCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new PublishKnowledgeArticleCommand(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(KnowledgeArticleStatus.Published, persisted.Status);
        Assert.NotNull(persisted.PublishedOn);
        Assert.Equal(currentUser.UserId, persisted.PublishedBy);
    }

    [Fact]
    public async Task Publish_already_Published_article_is_idempotent_noop()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var publishedOn = DateTime.UtcNow.AddDays(-1);
        var publishedBy = Guid.NewGuid();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = publishedOn,
            PublishedBy = publishedBy
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new PublishKnowledgeArticleCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(new PublishKnowledgeArticleCommand(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(publishedOn, persisted.PublishedOn);
        Assert.Equal(publishedBy, persisted.PublishedBy);
    }

    [Fact]
    public async Task Publish_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new PublishKnowledgeArticleCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new PublishKnowledgeArticleCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
