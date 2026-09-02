using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Queries.GetAssignmentRuleById;

internal sealed class GetAssignmentRuleByIdQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetAssignmentRuleByIdQuery, Result<AssignmentRuleDto>>
{
    public async Task<Result<AssignmentRuleDto>> Handle(GetAssignmentRuleByIdQuery request, CancellationToken ct)
    {
        var rule = await dbContext.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Assignment rule '{request.Id}' was not found.");

        var (fullName, _) = await identityQueryService.GetUserInfoAsync(rule.AssignedToUserId, ct);

        var dto = new AssignmentRuleDto(
            rule.Id, rule.Name, rule.Category, rule.Priority, rule.AssignedToUserId, fullName,
            rule.EvaluationOrder, rule.IsActive, rule.CreatedOn, rule.UpdatedOn);

        return Result<AssignmentRuleDto>.Success(dto);
    }
}
