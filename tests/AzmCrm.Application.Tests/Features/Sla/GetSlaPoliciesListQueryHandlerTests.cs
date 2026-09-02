using AzmCrm.Application.Features.Sla.Queries.GetSlaPoliciesList;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class GetSlaPoliciesListQueryHandlerTests
{
    private static async Task<TestApplicationDbContext> SeedAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        dbContext.SlaPolicies.AddRange(
            new SlaPolicy { Name = "Urgent", Priority = TicketPriority.Urgent, ResponseTimeMinutes = 15, ResolutionTimeMinutes = 60 },
            new SlaPolicy { Name = "Low", Priority = TicketPriority.Low, ResponseTimeMinutes = 120, ResolutionTimeMinutes = 1440 },
            new SlaPolicy { Name = "Inactive High", Priority = TicketPriority.High, ResponseTimeMinutes = 30, ResolutionTimeMinutes = 240, IsActive = false });
        await dbContext.SaveChangesAsync();
        return dbContext;
    }

    [Fact]
    public async Task List_returns_all_policies_ordered_by_priority()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetSlaPoliciesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetSlaPoliciesListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalCount);
    }

    [Fact]
    public async Task List_filters_by_priority()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetSlaPoliciesListQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetSlaPoliciesListQuery(Priority: TicketPriority.Low), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Low", item.Name);
    }

    [Fact]
    public async Task List_filters_by_isActive()
    {
        await using var dbContext = await SeedAsync();
        var handler = new GetSlaPoliciesListQueryHandler(dbContext);

        var result = await handler.Handle(new GetSlaPoliciesListQuery(IsActive: false), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Inactive High", item.Name);
    }
}
