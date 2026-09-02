using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;
using AzmCrm.Domain.Features.QuickReplies;
using Xunit;

namespace AzmCrm.Application.Tests.Features.QuickReplies;

public class GetQuickReplyTemplatesListQueryHandlerTests
{
    [Fact]
    public async Task List_returns_results_ordered_alphabetically_by_title()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.QuickReplyTemplates.AddRange(
            new QuickReplyTemplate { Title = "Zebra", Body = "B" },
            new QuickReplyTemplate { Title = "Apple", Body = "B" },
            new QuickReplyTemplate { Title = "Mango", Body = "B" });
        await dbContext.SaveChangesAsync();

        var handler = new GetQuickReplyTemplatesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetQuickReplyTemplatesListQuery(), CancellationToken.None);

        var titles = result.Data!.Items.Select(t => t.Title).ToList();
        Assert.Equal(["Apple", "Mango", "Zebra"], titles);
    }

    [Fact]
    public async Task List_filters_by_search_term_matching_title_or_body()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.QuickReplyTemplates.AddRange(
            new QuickReplyTemplate { Title = "Order Delay", Body = "Sorry for the wait" },
            new QuickReplyTemplate { Title = "Refund", Body = "Your refund is processing" });
        await dbContext.SaveChangesAsync();

        var handler = new GetQuickReplyTemplatesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetQuickReplyTemplatesListQuery(Search: "refund"), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Refund", item.Title);
    }
}
