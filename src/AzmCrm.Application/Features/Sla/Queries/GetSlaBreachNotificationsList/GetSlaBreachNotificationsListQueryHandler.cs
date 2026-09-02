using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;

internal sealed class GetSlaBreachNotificationsListQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetSlaBreachNotificationsListQuery, Result<PaginatedResult<SlaBreachNotificationDto>>>
{
    public async Task<Result<PaginatedResult<SlaBreachNotificationDto>>> Handle(
        GetSlaBreachNotificationsListQuery request, CancellationToken ct)
    {
        var query = dbContext.SlaBreachNotifications.AsQueryable();

        if (request.TicketId is not null)
            query = query.Where(n => n.TicketId == request.TicketId);

        if (request.NotifiedUserId is not null)
            query = query.Where(n => n.NotifiedUserId == request.NotifiedUserId);

        if (request.BreachType is not null)
            query = query.Where(n => n.BreachType == request.BreachType);

        var totalCount = await query.CountAsync(ct);

        var notifications = await query
            .OrderByDescending(n => n.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var notifiedUserIds = notifications.Where(n => n.NotifiedUserId is not null)
            .Select(n => n.NotifiedUserId!.Value);
        var userInfo = await identityQueryService.GetUsersInfoAsync(notifiedUserIds, ct);

        var items = notifications.Select(n => new SlaBreachNotificationDto(
            n.Id, n.TicketId, n.BreachType, n.NotifiedUserId,
            n.NotifiedUserId is not null && userInfo.TryGetValue(n.NotifiedUserId.Value, out var info)
                ? info.FullName
                : null,
            n.Message, n.EmailSent, n.CreatedOn));

        var result = new PaginatedResult<SlaBreachNotificationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<SlaBreachNotificationDto>>.Success(result);
    }
}
