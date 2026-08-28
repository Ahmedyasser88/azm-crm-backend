namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerNoteDto(
    Guid Id,
    Guid CustomerId,
    string Content,
    Guid CreatedBy,
    DateTime CreatedOn
);
