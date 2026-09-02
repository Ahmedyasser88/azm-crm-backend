namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record DashboardSummaryDto(
    int TotalAssigned,
    int New,
    int Open,
    int InProgress,
    int OnHold,
    int Resolved,
    int Closed,
    int Reopened,
    int EscalatedCount
);
