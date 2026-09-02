using AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Dashboard;

public class GetDashboardSummaryQueryHandlerTests
{
    [Fact]
    public async Task Counts_tickets_by_status_for_current_user_only()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);

        dbContext.Tickets.AddRange(
            new Ticket { CustomerId = customer.Id, Title = "T1", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.Open, AssignedToUserId = currentUser.UserId },
            new Ticket { CustomerId = customer.Id, Title = "T2", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.Open, AssignedToUserId = currentUser.UserId },
            new Ticket { CustomerId = customer.Id, Title = "T3", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.Resolved, AssignedToUserId = currentUser.UserId },
            new Ticket { CustomerId = customer.Id, Title = "T4 (other user)", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.Open, AssignedToUserId = Guid.NewGuid() });
        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardSummaryQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalAssigned);
        Assert.Equal(2, result.Data.Open);
        Assert.Equal(1, result.Data.Resolved);
        Assert.Equal(0, result.Data.New);
    }

    [Fact]
    public async Task EscalatedCount_counts_escalated_tickets_regardless_of_status()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);

        dbContext.Tickets.AddRange(
            new Ticket { CustomerId = customer.Id, Title = "T1", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.InProgress, AssignedToUserId = currentUser.UserId, IsEscalated = true },
            new Ticket { CustomerId = customer.Id, Title = "T2", Category = TicketCategory.General, Priority = TicketPriority.Low, Status = TicketStatus.Open, AssignedToUserId = currentUser.UserId, IsEscalated = false });
        await dbContext.SaveChangesAsync();

        var handler = new GetDashboardSummaryQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(1, result.Data!.EscalatedCount);
    }

    [Fact]
    public async Task Returns_all_zero_summary_when_no_tickets_assigned()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        var handler = new GetDashboardSummaryQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.TotalAssigned);
        Assert.Equal(0, result.Data.EscalatedCount);
    }
}
