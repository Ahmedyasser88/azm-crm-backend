using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.AgentTasks;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class GetAgentTasksListQueryHandlerTests
{
    [Fact]
    public async Task Returns_only_tasks_owned_by_current_user()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        dbContext.AgentTasks.AddRange(
            new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "Mine" },
            new AgentTask { AssignedToUserId = Guid.NewGuid(), Title = "Not mine" });
        await dbContext.SaveChangesAsync();

        var handler = new GetAgentTasksListQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetAgentTasksListQuery(), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Mine", item.Title);
    }

    [Fact]
    public async Task Filters_by_isCompleted()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();

        dbContext.AgentTasks.AddRange(
            new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "Done", IsCompleted = true },
            new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "Not done", IsCompleted = false });
        await dbContext.SaveChangesAsync();

        var handler = new GetAgentTasksListQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetAgentTasksListQuery(IsCompleted: true), CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Done", item.Title);
    }

    [Fact]
    public async Task Orders_incomplete_first_then_soonest_due_first()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var userId = currentUser.UserId!.Value;
        var now = DateTime.UtcNow;

        var completed = new AgentTask { AssignedToUserId = userId, Title = "Completed", IsCompleted = true };
        var dueTomorrow = new AgentTask { AssignedToUserId = userId, Title = "Due tomorrow", DueOn = now.AddDays(1) };
        var dueInAnHour = new AgentTask { AssignedToUserId = userId, Title = "Due in an hour", DueOn = now.AddHours(1) };
        var noDueDate = new AgentTask { AssignedToUserId = userId, Title = "No due date" };

        dbContext.AgentTasks.AddRange(completed, dueTomorrow, dueInAnHour, noDueDate);
        await dbContext.SaveChangesAsync();

        var handler = new GetAgentTasksListQueryHandler(dbContext, currentUser);

        var result = await handler.Handle(new GetAgentTasksListQuery(PageSize: 100), CancellationToken.None);

        var titles = result.Data!.Items.Select(t => t.Title).ToList();
        Assert.Equal(["Due in an hour", "Due tomorrow", "No due date", "Completed"], titles);
    }
}
