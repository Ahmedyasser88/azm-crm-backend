namespace AzmCrm.Application.Features.Identity.DTOs;

public sealed record AgentSummaryDto(Guid Id, string FullName, string? Email);
