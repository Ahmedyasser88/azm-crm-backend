using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Queries.GetEscalationRulesList;

internal sealed class GetEscalationRulesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetEscalationRulesListQuery, Result<PaginatedResult<EscalationRuleListItemDto>>>
{
    public async Task<Result<PaginatedResult<EscalationRuleListItemDto>>> Handle(
        GetEscalationRulesListQuery request, CancellationToken ct)
    {
        var query = dbContext.EscalationRules.AsQueryable();

        if (request.Priority is not null)
            query = query.Where(r => r.Priority == request.Priority);

        if (request.IsActive is not null)
            query = query.Where(r => r.IsActive == request.IsActive);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(r => r.Priority)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new EscalationRuleListItemDto(r.Id, r.Name, r.Priority, r.OverdueMinutes, r.IsActive))
            .ToListAsync(ct);

        var result = new PaginatedResult<EscalationRuleListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<EscalationRuleListItemDto>>.Success(result);
    }
}
