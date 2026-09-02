using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Queries.GetAssignmentRuleById;

public sealed record GetAssignmentRuleByIdQuery(Guid Id) : IRequest<Result<AssignmentRuleDto>>;
