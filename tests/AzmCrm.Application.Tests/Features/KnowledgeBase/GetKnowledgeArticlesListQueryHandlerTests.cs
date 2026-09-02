using AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticlesList;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class GetKnowledgeArticlesListQueryHandlerTests
{
    private static KnowledgeArticle MakeArticle(
        string title, KnowledgeArticleType type, KnowledgeArticleStatus status = KnowledgeArticleStatus.Draft,
        string? category = null) =>
        new() { Title = title, Content = "Content", Type = type, Status = status, Category = category };

    [Fact]
    public async Task List_returns_all_articles_ordered_by_CreatedOn_descending()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var first = MakeArticle("First", KnowledgeArticleType.Faq);
        first.CreatedOn = DateTime.UtcNow.AddMinutes(-10);
        var second = MakeArticle("Second", KnowledgeArticleType.Faq);
        second.CreatedOn = DateTime.UtcNow;
        dbContext.KnowledgeArticles.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetKnowledgeArticlesListQuery(), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Second", items[0].Title);
        Assert.Equal("First", items[1].Title);
    }

    [Fact]
    public async Task List_filters_by_Type()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakeArticle("Faq one", KnowledgeArticleType.Faq));
        dbContext.KnowledgeArticles.Add(MakeArticle("Guide one", KnowledgeArticleType.Guide));
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetKnowledgeArticlesListQuery(Type: KnowledgeArticleType.Guide), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("Guide one", items[0].Title);
    }

    [Fact]
    public async Task List_filters_by_Status()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakeArticle("Draft one", KnowledgeArticleType.Faq));
        dbContext.KnowledgeArticles.Add(
            MakeArticle("Published one", KnowledgeArticleType.Faq, KnowledgeArticleStatus.Published));
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetKnowledgeArticlesListQuery(Status: KnowledgeArticleStatus.Published), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("Published one", items[0].Title);
    }

    [Fact]
    public async Task List_filters_by_Category()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(MakeArticle("A", KnowledgeArticleType.Faq, category: "Billing"));
        dbContext.KnowledgeArticles.Add(MakeArticle("B", KnowledgeArticleType.Faq, category: "Account"));
        await dbContext.SaveChangesAsync();

        var handler = new GetKnowledgeArticlesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetKnowledgeArticlesListQuery(Category: "Account"), CancellationToken.None);

        var items = result.Data!.Items.ToList();
        Assert.Single(items);
        Assert.Equal("B", items[0].Title);
    }
}
