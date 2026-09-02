using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

public sealed record CreateAssignmentRuleCommand(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder) : IRequest<Result<Guid>>;
