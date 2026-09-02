using AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Sla;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class GetSlaBreachNotificationsListQueryHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Guid TicketId, Guid UserId)> SeedAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var ticketId = Guid.NewGuid();
        var otherTicketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        dbContext.SlaBreachNotifications.AddRange(
            new SlaBreachNotification
            {
                TicketId = ticketId, BreachType = SlaBreachType.ResponseOverdue,
                NotifiedUserId = userId, Message = "M1"
            },
            new SlaBreachNotification
            {
                TicketId = otherTicketId, BreachType = SlaBreachType.ResolutionOverdue,
                NotifiedUserId = null, Message = "M2"
            });
        await dbContext.SaveChangesAsync();

        return (dbContext, ticketId, userId);
    }

    [Fact]
    public async Task List_filters_by_ticketId()
    {
        var (dbContext, ticketId, _) = await SeedAsync();
        await using var _db = dbContext;

        var handler = new GetSlaBreachNotificationsListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(
            new GetSlaBreachNotificationsListQuery(TicketId: ticketId), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ticketId, item.TicketId);
    }

    [Fact]
    public async Task List_filters_by_notifiedUserId()
    {
        var (dbContext, _, userId) = await SeedAsync();
        await using var _db = dbContext;

        var handler = new GetSlaBreachNotificationsListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(
            new GetSlaBreachNotificationsListQuery(NotifiedUserId: userId), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(userId, item.NotifiedUserId);
    }

    [Fact]
    public async Task List_filters_by_breachType()
    {
        var (dbContext, _, _) = await SeedAsync();
        await using var _db = dbContext;

        var handler = new GetSlaBreachNotificationsListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(
            new GetSlaBreachNotificationsListQuery(BreachType: SlaBreachType.ResolutionOverdue), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(SlaBreachType.ResolutionOverdue, item.BreachType);
    }

    [Fact]
    public async Task List_orders_newest_first()
    {
        var (dbContext, _, _) = await SeedAsync();
        await using var _db = dbContext;

        var handler = new GetSlaBreachNotificationsListQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(new GetSlaBreachNotificationsListQuery(), CancellationToken.None);

        Assert.Equal(2, result.Data!.TotalCount);
    }
}
