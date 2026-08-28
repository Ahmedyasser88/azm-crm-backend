namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record UpdateCustomerRequest(
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);
