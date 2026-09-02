using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;

internal sealed class SetAgentTaskCompletionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<SetAgentTaskCompletionCommand, Result>
{
    public async Task<Result> Handle(SetAgentTaskCompletionCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.IsCompleted = request.IsCompleted;
        task.CompletedOn = request.IsCompleted ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
