using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationById;

internal sealed class GetSlaBreachNotificationByIdQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetSlaBreachNotificationByIdQuery, Result<SlaBreachNotificationDto>>
{
    public async Task<Result<SlaBreachNotificationDto>> Handle(
        GetSlaBreachNotificationByIdQuery request, CancellationToken ct)
    {
        var notification = await dbContext.SlaBreachNotifications.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA breach notification '{request.Id}' was not found.");

        string? notifiedUserName = null;
        if (notification.NotifiedUserId is not null)
        {
            var (fullName, _) = await identityQueryService.GetUserInfoAsync(notification.NotifiedUserId.Value, ct);
            notifiedUserName = fullName;
        }

        var dto = new SlaBreachNotificationDto(
            notification.Id, notification.TicketId, notification.BreachType, notification.NotifiedUserId,
            notifiedUserName, notification.Message, notification.EmailSent, notification.CreatedOn);

        return Result<SlaBreachNotificationDto>.Success(dto);
    }
}
