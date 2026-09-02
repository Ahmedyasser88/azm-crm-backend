using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationById;

public sealed record GetSlaBreachNotificationByIdQuery(Guid Id) : IRequest<Result<SlaBreachNotificationDto>>;
