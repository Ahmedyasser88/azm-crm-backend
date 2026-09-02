using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;

public sealed record GetAgentTasksListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsCompleted = null
) : IRequest<Result<PaginatedResult<AgentTaskDto>>>;
