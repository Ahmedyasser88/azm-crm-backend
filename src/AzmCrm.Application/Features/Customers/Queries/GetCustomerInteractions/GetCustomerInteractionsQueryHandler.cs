using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;

internal sealed class GetCustomerInteractionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerInteractionsQuery, Result<PaginatedResult<CustomerInteractionDto>>>
{
    public async Task<Result<PaginatedResult<CustomerInteractionDto>>> Handle(
        GetCustomerInteractionsQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerInteractions.Where(i => i.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.OccurredOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new CustomerInteractionDto(
                i.Id, i.CustomerId, i.Type, i.Subject, i.Description, i.OccurredOn, i.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerInteractionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerInteractionDto>>.Success(result);
    }
}
