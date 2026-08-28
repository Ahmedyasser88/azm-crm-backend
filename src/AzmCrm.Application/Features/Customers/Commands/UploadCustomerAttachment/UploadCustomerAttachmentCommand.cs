using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

public sealed record UploadCustomerAttachmentCommand(
    Guid CustomerId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content
) : IRequest<Result<Guid>>;
