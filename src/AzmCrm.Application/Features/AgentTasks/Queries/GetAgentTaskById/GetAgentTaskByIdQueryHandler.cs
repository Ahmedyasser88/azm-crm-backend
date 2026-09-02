using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;

internal sealed class GetAgentTaskByIdQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentTaskByIdQuery, Result<AgentTaskDto>>
{
    public async Task<Result<AgentTaskDto>> Handle(GetAgentTaskByIdQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        var dto = new AgentTaskDto(
            task.Id, task.Title, task.Description, task.DueOn, task.IsCompleted, task.CompletedOn,
            task.CustomerId, task.TicketId, task.CreatedOn, task.UpdatedOn);

        return Result<AgentTaskDto>.Success(dto);
    }
}
