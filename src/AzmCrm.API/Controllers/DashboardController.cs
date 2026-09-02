using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;
using AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class DashboardController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(Result<PaginatedResult<DashboardTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] TicketStatus? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyTicketsQuery(pageNumber, pageSize, status), ct);
        return ToResult(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(Result<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await mediator.Send(new GetDashboardSummaryQuery(), ct);
        return ToResult(result);
    }
}
