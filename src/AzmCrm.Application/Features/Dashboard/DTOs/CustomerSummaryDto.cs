namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record CustomerSummaryDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber
);
