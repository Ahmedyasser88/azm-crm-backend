using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;

public sealed record CreateAgentTaskCommand(
    string Title, string? Description, DateTime? DueOn, Guid? CustomerId, Guid? TicketId
) : IRequest<Result<Guid>>;
