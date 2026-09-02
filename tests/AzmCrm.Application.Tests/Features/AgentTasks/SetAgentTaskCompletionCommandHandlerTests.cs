using AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.AgentTasks;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class SetAgentTaskCompletionCommandHandlerTests
{
    [Fact]
    public async Task Completing_sets_IsCompleted_and_CompletedOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var task = new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "T" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new SetAgentTaskCompletionCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new SetAgentTaskCompletionCommand(task.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(task.IsCompleted);
        Assert.NotNull(task.CompletedOn);
    }

    [Fact]
    public async Task Uncompleting_clears_CompletedOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var task = new AgentTask
        {
            AssignedToUserId = currentUser.UserId!.Value, Title = "T",
            IsCompleted = true, CompletedOn = DateTime.UtcNow
        };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new SetAgentTaskCompletionCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new SetAgentTaskCompletionCommand(task.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedOn);
    }

    [Fact]
    public async Task SetCompletion_task_owned_by_another_user_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var task = new AgentTask { AssignedToUserId = Guid.NewGuid(), Title = "T" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new SetAgentTaskCompletionCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SetAgentTaskCompletionCommand(task.Id, true), CancellationToken.None));
    }
}
