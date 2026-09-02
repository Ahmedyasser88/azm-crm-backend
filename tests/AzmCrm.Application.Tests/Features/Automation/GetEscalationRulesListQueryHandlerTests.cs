using AzmCrm.Application.Features.Automation.Queries.GetEscalationRulesList;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class GetEscalationRulesListQueryHandlerTests
{
    private static async Task<TestApplicationDbContext> SeedAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        dbContext.EscalationRules.AddRange(
            new EscalationRule { Name = "Urgent", Priority = TicketPriority.Urgent, OverdueMinutes = 0 },
            new EscalationRule { Name = "Catch-all", Priority = null, OverdueMinutes = 60 },
            new EscalationRule { Name = "Inactive", Priority = TicketPriority.Low, OverdueMinutes = 120, IsActive = false });
        await dbContext.SaveChangesAsync();
        return dbContext;
    }

    [Fact]
    public async Task List_filters_by_priority()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetEscalationRulesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetEscalationRulesListQuery(Priority: TicketPriority.Urgent), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Urgent", item.Name);
    }

    [Fact]
    public async Task List_filters_by_isActive()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetEscalationRulesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetEscalationRulesListQuery(IsActive: false), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Inactive", item.Name);
    }
}
