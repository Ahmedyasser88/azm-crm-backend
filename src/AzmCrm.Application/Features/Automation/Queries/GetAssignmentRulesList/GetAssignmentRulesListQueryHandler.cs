using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Queries.GetAssignmentRulesList;

internal sealed class GetAssignmentRulesListQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetAssignmentRulesListQuery, Result<PaginatedResult<AssignmentRuleListItemDto>>>
{
    public async Task<Result<PaginatedResult<AssignmentRuleListItemDto>>> Handle(
        GetAssignmentRulesListQuery request, CancellationToken ct)
    {
        var query = dbContext.AssignmentRules.AsQueryable();

        if (request.Category is not null)
            query = query.Where(r => r.Category == request.Category);

        if (request.Priority is not null)
            query = query.Where(r => r.Priority == request.Priority);

        if (request.IsActive is not null)
            query = query.Where(r => r.IsActive == request.IsActive);

        var totalCount = await query.CountAsync(ct);

        var rules = await query
            .OrderBy(r => r.EvaluationOrder)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var assigneeNames = await identityQueryService.GetUsersInfoAsync(
            rules.Select(r => r.AssignedToUserId), ct);

        var items = rules.Select(r => new AssignmentRuleListItemDto(
            r.Id, r.Name, r.Category, r.Priority, r.AssignedToUserId,
            assigneeNames.TryGetValue(r.AssignedToUserId, out var info) ? info.FullName : null,
            r.EvaluationOrder, r.IsActive));

        var result = new PaginatedResult<AssignmentRuleListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<AssignmentRuleListItemDto>>.Success(result);
    }
}
