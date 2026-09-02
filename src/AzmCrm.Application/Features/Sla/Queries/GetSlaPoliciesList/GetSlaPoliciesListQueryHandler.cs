using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPoliciesList;

internal sealed class GetSlaPoliciesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSlaPoliciesListQuery, Result<PaginatedResult<SlaPolicyListItemDto>>>
{
    public async Task<Result<PaginatedResult<SlaPolicyListItemDto>>> Handle(
        GetSlaPoliciesListQuery request, CancellationToken ct)
    {
        var query = dbContext.SlaPolicies.AsQueryable();

        if (request.Priority is not null)
            query = query.Where(p => p.Priority == request.Priority);

        if (request.IsActive is not null)
            query = query.Where(p => p.IsActive == request.IsActive);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Priority)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new SlaPolicyListItemDto(
                p.Id, p.Name, p.Priority, p.ResponseTimeMinutes, p.ResolutionTimeMinutes, p.IsActive))
            .ToListAsync(ct);

        var result = new PaginatedResult<SlaPolicyListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<SlaPolicyListItemDto>>.Success(result);
    }
}
