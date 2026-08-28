using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerNotes;

internal sealed class GetCustomerNotesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerNotesQuery, Result<PaginatedResult<CustomerNoteDto>>>
{
    public async Task<Result<PaginatedResult<CustomerNoteDto>>> Handle(
        GetCustomerNotesQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerNotes.Where(n => n.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new CustomerNoteDto(n.Id, n.CustomerId, n.Content, n.CreatedBy, n.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerNoteDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerNoteDto>>.Success(result);
    }
}
