using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;

public sealed record DeleteAssignmentRuleCommand(Guid Id) : IRequest<Result>;
