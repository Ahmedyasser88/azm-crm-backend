using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;
using AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;
using AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Features.Sla.Queries.GetSlaPoliciesList;
using AzmCrm.Application.Features.Sla.Queries.GetSlaPolicyById;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/SlaPolicies" (case-insensitively matched as "api/slapolicies", but never
// "api/sla-policies" — a hyphen is not a casing difference). Every frontend caller and this
// story's own plan expect "api/sla-policies".
[Route("api/sla-policies")]
public sealed class SlaPoliciesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSlaPolicyRequest request, CancellationToken ct)
    {
        var command = new CreateSlaPolicyCommand(
            request.Name, request.Priority, request.ResponseTimeMinutes, request.ResolutionTimeMinutes);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/sla-policies/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<SlaPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSlaPolicyByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<SlaPolicyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] TicketPriority? priority = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSlaPoliciesListQuery(pageNumber, pageSize, priority, isActive), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaPolicyRequest request, CancellationToken ct)
    {
        var command = new UpdateSlaPolicyCommand(
            id, request.Name, request.Priority, request.ResponseTimeMinutes,
            request.ResolutionTimeMinutes, request.IsActive);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSlaPolicyCommand(id), ct);
        return ToNoContentResult(result);
    }
}
