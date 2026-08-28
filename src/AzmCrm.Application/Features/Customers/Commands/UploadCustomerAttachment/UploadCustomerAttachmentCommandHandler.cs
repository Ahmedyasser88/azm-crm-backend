using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

internal sealed class UploadCustomerAttachmentCommandHandler(
    IApplicationDbContext dbContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadCustomerAttachmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadCustomerAttachmentCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var storageKey = await fileStorage.SaveAsync(request.Content, request.FileName, ct);

        var attachment = new CustomerAttachment
        {
            CustomerId = request.CustomerId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            StorageKey = storageKey
        };

        dbContext.CustomerAttachments.Add(attachment);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(attachment.Id);
    }
}
