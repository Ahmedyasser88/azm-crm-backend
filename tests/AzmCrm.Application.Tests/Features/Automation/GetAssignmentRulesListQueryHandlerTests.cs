using AzmCrm.Application.Features.Automation.Queries.GetAssignmentRulesList;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class GetAssignmentRulesListQueryHandlerTests
{
    private static async Task<TestApplicationDbContext> SeedAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        dbContext.AssignmentRules.AddRange(
            new AssignmentRule { Name = "Second", Category = TicketCategory.Billing, AssignedToUserId = Guid.NewGuid(), EvaluationOrder = 2 },
            new AssignmentRule { Name = "First", Priority = TicketPriority.Urgent, AssignedToUserId = Guid.NewGuid(), EvaluationOrder = 1 },
            new AssignmentRule { Name = "Inactive", AssignedToUserId = Guid.NewGuid(), EvaluationOrder = 3, IsActive = false });
        await dbContext.SaveChangesAsync();
        return dbContext;
    }

    [Fact]
    public async Task List_returns_rules_ordered_by_EvaluationOrder()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetAssignmentRulesListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(new GetAssignmentRulesListQuery(), CancellationToken.None);

        Assert.Equal(["First", "Second", "Inactive"], result.Data!.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task List_filters_by_category()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetAssignmentRulesListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(
            new GetAssignmentRulesListQuery(Category: TicketCategory.Billing), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Second", item.Name);
    }

    [Fact]
    public async Task List_filters_by_priority()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetAssignmentRulesListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(
            new GetAssignmentRulesListQuery(Priority: TicketPriority.Urgent), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("First", item.Name);
    }

    [Fact]
    public async Task List_filters_by_isActive()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetAssignmentRulesListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(new GetAssignmentRulesListQuery(IsActive: false), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Inactive", item.Name);
    }
}
