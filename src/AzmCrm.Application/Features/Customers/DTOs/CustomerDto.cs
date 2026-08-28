namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
