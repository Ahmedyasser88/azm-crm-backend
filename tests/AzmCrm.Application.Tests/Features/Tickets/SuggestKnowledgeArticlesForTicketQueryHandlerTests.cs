using AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.KnowledgeBase;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class SuggestKnowledgeArticlesForTicketQueryHandlerTests
{
    private static KnowledgeArticle MakePublished(string title, DateTime? publishedOn = null) =>
        new()
        {
            Title = title,
            Content = "Content",
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = publishedOn ?? DateTime.UtcNow
        };

    private static async Task<(TestApplicationDbContext DbContext, Ticket Ticket)> SeedTicketAsync(string title)
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = title,
            Category = TicketCategory.Technical,
            Priority = TicketPriority.Low
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return (dbContext, ticket);
    }

    [Fact]
    public async Task Suggest_returns_articles_matching_ticket_Title()
    {
        // The handler matches KB fields that CONTAIN the ticket's title (same direction as
        // SearchKnowledgeArticlesQueryHandler's Contains(term)), so the ticket's title here must
        // itself appear verbatim inside the seeded article's title.
        var (dbContext, ticket) = await SeedTicketAsync("password reset");
        await using var _ = dbContext;

        dbContext.KnowledgeArticles.Add(MakePublished("password reset guide"));
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Suggest_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Suggest_with_no_matches_returns_empty_list()
    {
        var (dbContext, ticket) = await SeedTicketAsync("completely unrelated issue");
        await using var _ = dbContext;

        dbContext.KnowledgeArticles.Add(MakePublished("How do I reset my password?"));
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(ticket.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Suggest_excludes_Draft_articles_even_on_exact_Title_match()
    {
        var (dbContext, ticket) = await SeedTicketAsync("password reset");
        await using var _ = dbContext;

        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "password reset", Content = "C", Type = KnowledgeArticleType.Faq
        });
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(ticket.Id), CancellationToken.None);

        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Suggest_excludes_soft_deleted_articles()
    {
        var (dbContext, ticket) = await SeedTicketAsync("password reset");
        await using var _ = dbContext;

        var article = MakePublished("password reset");
        article.IsDeleted = true;
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(ticket.Id), CancellationToken.None);

        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Suggest_respects_MaxResults_cap()
    {
        var (dbContext, ticket) = await SeedTicketAsync("password reset");
        await using var _ = dbContext;

        for (var i = 0; i < 5; i++)
            dbContext.KnowledgeArticles.Add(MakePublished($"password reset guide {i}"));
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(
            new SuggestKnowledgeArticlesForTicketQuery(ticket.Id, MaxResults: 2), CancellationToken.None);

        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task Suggest_orders_by_PublishedOn_descending()
    {
        var (dbContext, ticket) = await SeedTicketAsync("password reset");
        await using var _ = dbContext;

        dbContext.KnowledgeArticles.Add(MakePublished("password reset older", DateTime.UtcNow.AddDays(-1)));
        dbContext.KnowledgeArticles.Add(MakePublished("password reset newer", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var handler = new SuggestKnowledgeArticlesForTicketQueryHandler(dbContext);

        var result = await handler.Handle(new SuggestKnowledgeArticlesForTicketQuery(ticket.Id), CancellationToken.None);

        Assert.Equal("password reset newer", result.Data![0].Title);
        Assert.Equal("password reset older", result.Data![1].Title);
    }
}
