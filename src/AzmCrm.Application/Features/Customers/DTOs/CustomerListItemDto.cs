namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerListItemDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    DateTime CreatedOn
);
