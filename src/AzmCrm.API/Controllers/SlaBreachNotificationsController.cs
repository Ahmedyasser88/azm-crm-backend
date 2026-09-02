using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationById;
using AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

[Route("api/sla-breach-notifications")]
public sealed class SlaBreachNotificationsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<SlaBreachNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSlaBreachNotificationByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<SlaBreachNotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? ticketId = null, [FromQuery] Guid? notifiedUserId = null,
        [FromQuery] SlaBreachType? breachType = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetSlaBreachNotificationsListQuery(pageNumber, pageSize, ticketId, notifiedUserId, breachType), ct);
        return ToResult(result);
    }
}
