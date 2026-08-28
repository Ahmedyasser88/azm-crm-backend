using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;

internal sealed class GetCustomerAttachmentContentQueryHandler(
    IApplicationDbContext dbContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetCustomerAttachmentContentQuery, Result<CustomerAttachmentContentDto>>
{
    public async Task<Result<CustomerAttachmentContentDto>> Handle(
        GetCustomerAttachmentContentQuery request, CancellationToken ct)
    {
        var attachment = await dbContext.CustomerAttachments
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct)
            ?? throw new NotFoundException(
                $"Attachment '{request.AttachmentId}' was not found for customer '{request.CustomerId}'.");

        var stream = await fileStorage.OpenReadAsync(attachment.StorageKey, ct);

        var dto = new CustomerAttachmentContentDto(stream, attachment.ContentType, attachment.FileName);

        return Result<CustomerAttachmentContentDto>.Success(dto);
    }
}
