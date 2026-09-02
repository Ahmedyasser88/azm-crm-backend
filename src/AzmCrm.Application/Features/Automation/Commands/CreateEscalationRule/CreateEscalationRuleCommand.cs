using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

public sealed record CreateEscalationRuleCommand(string Name, TicketPriority? Priority, int OverdueMinutes)
    : IRequest<Result<Guid>>;
