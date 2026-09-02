using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;

public sealed record UpdateAssignmentRuleCommand(
    Guid Id, string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder, bool IsActive) : IRequest<Result>;
