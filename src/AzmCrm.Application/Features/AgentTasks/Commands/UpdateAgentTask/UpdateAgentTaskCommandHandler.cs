using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;

internal sealed class UpdateAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateAgentTaskCommand, Result>
{
    public async Task<Result> Handle(UpdateAgentTaskCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.Title = request.Title;
        task.Description = request.Description;
        task.DueOn = request.DueOn;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
