using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;

public sealed record UpdateAgentTaskCommand(
    Guid Id, string Title, string? Description, DateTime? DueOn
) : IRequest<Result>;
