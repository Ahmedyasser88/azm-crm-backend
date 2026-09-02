using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Queries.GetEscalationRuleById;

internal sealed class GetEscalationRuleByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetEscalationRuleByIdQuery, Result<EscalationRuleDto>>
{
    public async Task<Result<EscalationRuleDto>> Handle(GetEscalationRuleByIdQuery request, CancellationToken ct)
    {
        var rule = await dbContext.EscalationRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Escalation rule '{request.Id}' was not found.");

        var dto = new EscalationRuleDto(
            rule.Id, rule.Name, rule.Priority, rule.OverdueMinutes, rule.IsActive, rule.CreatedOn, rule.UpdatedOn);

        return Result<EscalationRuleDto>.Success(dto);
    }
}
