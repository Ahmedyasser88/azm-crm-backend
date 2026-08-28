namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerAttachmentDto(
    Guid Id,
    Guid CustomerId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CreatedOn
);
