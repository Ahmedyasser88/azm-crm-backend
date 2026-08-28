using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;

internal sealed class GetCustomerAttachmentsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerAttachmentsQuery, Result<PaginatedResult<CustomerAttachmentDto>>>
{
    public async Task<Result<PaginatedResult<CustomerAttachmentDto>>> Handle(
        GetCustomerAttachmentsQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerAttachments.Where(a => a.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new CustomerAttachmentDto(
                a.Id, a.CustomerId, a.FileName, a.ContentType, a.FileSizeBytes, a.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerAttachmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerAttachmentDto>>.Success(result);
    }
}
