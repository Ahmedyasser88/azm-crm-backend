using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;

internal sealed class GetDashboardSummaryQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var myTickets = dbContext.Tickets.Where(t => t.AssignedToUserId == userId);

        var statusCounts = await myTickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        var escalatedCount = await myTickets.CountAsync(t => t.IsEscalated, ct);

        int CountFor(TicketStatus status) => statusCounts.GetValueOrDefault(status);

        var dto = new DashboardSummaryDto(
            TotalAssigned: statusCounts.Values.Sum(),
            New: CountFor(TicketStatus.New),
            Open: CountFor(TicketStatus.Open),
            InProgress: CountFor(TicketStatus.InProgress),
            OnHold: CountFor(TicketStatus.OnHold),
            Resolved: CountFor(TicketStatus.Resolved),
            Closed: CountFor(TicketStatus.Closed),
            Reopened: CountFor(TicketStatus.Reopened),
            EscalatedCount: escalatedCount);

        return Result<DashboardSummaryDto>.Success(dto);
    }
}
