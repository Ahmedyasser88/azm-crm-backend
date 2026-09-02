using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Queries.GetEscalationRuleById;

public sealed record GetEscalationRuleByIdQuery(Guid Id) : IRequest<Result<EscalationRuleDto>>;
