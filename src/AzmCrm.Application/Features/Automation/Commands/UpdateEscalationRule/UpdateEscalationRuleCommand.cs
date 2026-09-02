using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateEscalationRule;

public sealed record UpdateEscalationRuleCommand(
    Guid Id, string Name, TicketPriority? Priority, int OverdueMinutes, bool IsActive) : IRequest<Result>;
