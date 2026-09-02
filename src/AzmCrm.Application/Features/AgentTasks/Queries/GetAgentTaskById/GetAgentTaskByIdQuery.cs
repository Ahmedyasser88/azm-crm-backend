using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;

public sealed record GetAgentTaskByIdQuery(Guid Id) : IRequest<Result<AgentTaskDto>>;
