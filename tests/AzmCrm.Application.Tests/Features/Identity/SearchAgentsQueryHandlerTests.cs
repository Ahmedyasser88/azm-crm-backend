using AzmCrm.Application.Features.Identity.Queries.SearchAgents;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Identity;

public class SearchAgentsQueryHandlerTests
{
    private static StubIdentityQueryService SeedAgents() =>
        new()
        {
            Users =
            {
                [Guid.NewGuid()] = ("Alice Agent", "alice@example.com"),
                [Guid.NewGuid()] = ("Bob Agent", "bob@example.com"),
            }
        };

    [Fact]
    public async Task Search_with_no_term_returns_all_agents_up_to_pageSize()
    {
        var identityQueryService = SeedAgents();
        var handler = new SearchAgentsQueryHandler(identityQueryService);

        var result = await handler.Handle(new SearchAgentsQuery(PageSize: 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task Search_filters_by_name()
    {
        var identityQueryService = SeedAgents();
        var handler = new SearchAgentsQueryHandler(identityQueryService);

        var result = await handler.Handle(new SearchAgentsQuery("alice"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var agent = Assert.Single(result.Data!);
        Assert.Equal("Alice Agent", agent.FullName);
    }
}
