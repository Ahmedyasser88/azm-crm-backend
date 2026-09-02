using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;

internal sealed class GetAgentTasksListQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentTasksListQuery, Result<PaginatedResult<AgentTaskDto>>>
{
    public async Task<Result<PaginatedResult<AgentTaskDto>>> Handle(
        GetAgentTasksListQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var query = dbContext.AgentTasks.Where(t => t.AssignedToUserId == userId);

        if (request.IsCompleted is not null)
            query = query.Where(t => t.IsCompleted == request.IsCompleted);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            // Incomplete tasks first, soonest due first, so this list can be rendered directly
            // as a reminders panel — a deliberate deviation from the CreatedOn-desc convention
            // used by every other list query in this codebase (see Edge Cases).
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueOn ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new AgentTaskDto(
                t.Id, t.Title, t.Description, t.DueOn, t.IsCompleted, t.CompletedOn,
                t.CustomerId, t.TicketId, t.CreatedOn, t.UpdatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<AgentTaskDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<AgentTaskDto>>.Success(result);
    }
}
