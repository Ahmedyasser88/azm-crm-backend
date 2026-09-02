using AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class GetKnowledgeArticleByIdQueryHandlerTests
{
    [Fact]
    public async Task GetById_returns_article()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Faq };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticleByIdQueryHandler(dbContext);

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(article.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("T", result.Data!.Title);
        Assert.Empty(result.Data.Steps);
    }

    [Fact]
    public async Task GetById_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetKnowledgeArticleByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetKnowledgeArticleByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_soft_deleted_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "T",
            Content = "C",
            Type = KnowledgeArticleType.Faq,
            IsDeleted = true
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticleByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetKnowledgeArticleByIdQuery(article.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_returns_steps_ordered_by_StepNumber_ascending()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Guide };
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

        var handler = new GetKnowledgeArticleByIdQueryHandler(dbContext);

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(article.Id), CancellationToken.None);

        Assert.Equal(2, result.Data!.Steps.Count);
        Assert.Equal("First", result.Data.Steps[0].Title);
        Assert.Equal("Second", result.Data.Steps[1].Title);
    }

    [Fact]
    public async Task GetById_with_no_steps_returns_empty_Steps_list()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Faq };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticleByIdQueryHandler(dbContext);

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(article.Id), CancellationToken.None);

        Assert.NotNull(result.Data!.Steps);
        Assert.Empty(result.Data.Steps);
    }
}
