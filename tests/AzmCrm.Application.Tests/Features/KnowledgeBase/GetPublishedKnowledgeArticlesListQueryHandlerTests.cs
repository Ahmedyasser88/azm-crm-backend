using AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticlesList;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class GetPublishedKnowledgeArticlesListQueryHandlerTests
{
    private static KnowledgeArticle MakePublished(
        string title, KnowledgeArticleType type = KnowledgeArticleType.Faq, string? category = null,
        DateTime? publishedOn = null) =>
        new()
        {
            Title = title,
            Content = "Content",
            Type = type,
            Status = KnowledgeArticleStatus.Published,
            Category = category,
            PublishedOn = publishedOn ?? DateTime.UtcNow
        };

    [Fact]
    public async Task List_returns_only_Published_articles_ordered_by_PublishedOn_descending()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var older = MakePublished("Older", publishedOn: DateTime.UtcNow.AddDays(-1));
        var newer = MakePublished("Newer", publishedOn: DateTime.UtcNow);
        dbContext.KnowledgeArticles.AddRange(older, newer);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetPublishedKnowledgeArticlesListQuery(), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Newer", items[0].Title);
        Assert.Equal("Older", items[1].Title);
    }

    [Fact]
    public async Task List_excludes_Draft_articles()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "Draft", Content = "C", Type = KnowledgeArticleType.Faq
        });
        dbContext.KnowledgeArticles.Add(MakePublished("Published"));
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetPublishedKnowledgeArticlesListQuery(), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("Published", items[0].Title);
    }

    [Fact]
    public async Task List_filters_by_Type()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("Faq", KnowledgeArticleType.Faq));
        dbContext.KnowledgeArticles.Add(MakePublished("Guide", KnowledgeArticleType.Guide));
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetPublishedKnowledgeArticlesListQuery(Type: KnowledgeArticleType.Guide), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("Guide", items[0].Title);
    }

    [Fact]
    public async Task List_filters_by_Category()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakePublished("A", category: "Billing"));
        dbContext.KnowledgeArticles.Add(MakePublished("B", category: "Account"));
        await dbContext.SaveChangesAsync();

        var handler = new GetPublishedKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetPublishedKnowledgeArticlesListQuery(Category: "Account"), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("B", items[0].Title);
    }
}
