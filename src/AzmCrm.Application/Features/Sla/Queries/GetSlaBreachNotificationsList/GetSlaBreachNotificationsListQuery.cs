using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;

public sealed record GetSlaBreachNotificationsListQuery(
    int PageNumber = 1, int PageSize = 20,
    Guid? TicketId = null, Guid? NotifiedUserId = null, SlaBreachType? BreachType = null
) : IRequest<Result<PaginatedResult<SlaBreachNotificationDto>>>;
