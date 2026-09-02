using AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;
using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.AgentTasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class DeleteAgentTaskCommandHandlerTests
{
    [Fact]
    public async Task Delete_sets_IsDeleted_and_DeletedBy_DeletedOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var task = new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "T" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteAgentTaskCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new DeleteAgentTaskCommand(task.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.AgentTasks.IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
        Assert.True(persisted.IsDeleted);
        Assert.Equal(currentUser.UserId, persisted.DeletedBy);
        Assert.NotNull(persisted.DeletedOn);
    }

    [Fact]
    public async Task Delete_missing_task_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteAgentTaskCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteAgentTaskCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Deleted_task_is_excluded_from_GetById()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var currentUser = new StubCurrentUserService();
        var task = new AgentTask { AssignedToUserId = currentUser.UserId!.Value, Title = "T" };
        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var deleteHandler = new DeleteAgentTaskCommandHandler(dbContext, currentUser);
        await deleteHandler.Handle(new DeleteAgentTaskCommand(task.Id), CancellationToken.None);

        var getByIdHandler = new GetAgentTaskByIdQueryHandler(dbContext, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => getByIdHandler.Handle(new GetAgentTaskByIdQuery(task.Id), CancellationToken.None));
    }
}
