using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomersList;

internal sealed class GetCustomersListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomersListQuery, Result<PaginatedResult<CustomerListItemDto>>>
{
    public async Task<Result<PaginatedResult<CustomerListItemDto>>> Handle(
        GetCustomersListQuery request, CancellationToken ct)
    {
        var query = dbContext.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CustomerListItemDto(
                c.Id, c.FullName, c.CompanyName, c.Email, c.PhoneNumber, c.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerListItemDto>>.Success(result);
    }
}
