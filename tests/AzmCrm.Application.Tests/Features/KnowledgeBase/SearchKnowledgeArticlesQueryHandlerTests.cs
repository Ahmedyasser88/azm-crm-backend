using AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class SearchKnowledgeArticlesQueryHandlerTests
{
    private static KnowledgeArticle MakePublished(
        string title, string content = "Content", string? category = null, string? tags = null) =>
        new()
        {
            Title = title,
            Content = content,
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            Category = category,
            Tags = tags,
            PublishedOn = DateTime.UtcNow
        };

    [Fact]
    public async Task Search_matches_Title()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("How do I reset my password?"));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_matches_Content()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("Title", "Go to Settings > Security > Reset Password."));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("security"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_matches_Category()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("Title", category: "Billing"));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("billing"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_matches_Tags()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("Title", tags: "password,reset"));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("reset"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_matches_step_Title_and_returns_parent_article_once()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = MakePublished("Guide", "Some content");
        article.Type = KnowledgeArticleType.Guide;
        dbContext.KnowledgeArticles.Add(article);
        dbContext.KnowledgeArticleSteps.Add(new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 1, Title = "Click Forgot Password", Description = "D1"
        });
        dbContext.KnowledgeArticleSteps.Add(new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 2, Title = "Enter your new Password", Description = "D2"
        });
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_matches_step_Description()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = MakePublished("Guide", "Some content");
        dbContext.KnowledgeArticles.Add(article);
        dbContext.KnowledgeArticleSteps.Add(new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 1, Title = "Step", Description = "Open the invoice page"
        });
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("invoice"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_excludes_Draft_articles_even_on_exact_Title_match()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "password reset", Content = "C", Type = KnowledgeArticleType.Faq
        });
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task Search_excludes_soft_deleted_articles()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = MakePublished("password reset");
        article.IsDeleted = true;
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("PASSWORD RESET"));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task Search_with_no_matches_returns_empty_result_with_correct_TotalCount()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("Unrelated title"));
        await dbContext.SaveChangesAsync();

        var handler = new SearchKnowledgeArticlesQueryHandler(dbContext);

        var result = await handler.Handle(new SearchKnowledgeArticlesQuery("password"), CancellationToken.None);

        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }
}
