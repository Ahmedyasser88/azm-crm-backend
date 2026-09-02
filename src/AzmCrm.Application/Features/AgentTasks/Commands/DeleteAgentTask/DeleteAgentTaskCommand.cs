using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;

public sealed record DeleteAgentTaskCommand(Guid Id) : IRequest<Result>;
