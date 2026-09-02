using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;

public sealed record SetAgentTaskCompletionCommand(Guid Id, bool IsCompleted) : IRequest<Result>;
