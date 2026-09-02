using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteEscalationRule;

public sealed record DeleteEscalationRuleCommand(Guid Id) : IRequest<Result>;
