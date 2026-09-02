using AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class GetPublishedKnowledgeArticleByIdQueryHandlerTests
{
    [Fact]
    public async Task GetPublishedById_returns_Published_article()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = DateTime.UtcNow
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticleByIdQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetPublishedKnowledgeArticleByIdQuery(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("T", result.Data!.Title);
    }

    [Fact]
    public async Task GetPublishedById_Draft_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Faq };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticleByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetPublishedKnowledgeArticleByIdQuery(article.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetPublishedById_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetPublishedKnowledgeArticleByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetPublishedKnowledgeArticleByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetPublishedById_returns_steps_ordered_by_StepNumber_ascending()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Guide,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = DateTime.UtcNow
        };
        dbContext.KnowledgeArticles.Add(article);
        dbContext.KnowledgeArticleSteps.Add(new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 2, Title = "Second", Description = "D2"
        });
        dbContext.KnowledgeArticleSteps.Add(new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 1, Title = "First", Description = "D1"
        });
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticleByIdQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetPublishedKnowledgeArticleByIdQuery(article.Id), CancellationToken.None);

        Assert.Equal(2, result.Data!.Steps.Count);
        Assert.Equal("First", result.Data.Steps[0].Title);
        Assert.Equal("Second", result.Data.Steps[1].Title);
    }
}
