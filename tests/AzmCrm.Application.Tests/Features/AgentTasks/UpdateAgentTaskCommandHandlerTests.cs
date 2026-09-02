using AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.AgentTasks;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class UpdateAgentTaskCommandHandlerTests
{
    [Fact]
    public async Task Update_modifies_title_description_and_dueOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var task = new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "Old" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAgentTaskCommandHandler(dbContext, currentUser);
        var dueOn = DateTime.UtcNow.AddDays(2);

        var result = await handler.Handle(
            new UpdateAgentTaskCommand(task.Id, "New", "Description", dueOn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", task.Title);
        Assert.Equal("Description", task.Description);
        Assert.Equal(dueOn, task.DueOn);
    }

    [Fact]
    public async Task Update_missing_task_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateAgentTaskCommand(Guid.NewGuid(), "Title", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Update_task_owned_by_another_user_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var task = new AgentTask { AssignedToUserId = Guid.NewGuid(), Title = "Old" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateAgentTaskCommand(task.Id, "New", null, null), CancellationToken.None));
    }
}
