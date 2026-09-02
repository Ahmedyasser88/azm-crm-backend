using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;

internal sealed class DeleteAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteAgentTaskCommand, Result>
{
    public async Task<Result> Handle(DeleteAgentTaskCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.IsDeleted = true;
        task.DeletedBy = userId;
        task.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
