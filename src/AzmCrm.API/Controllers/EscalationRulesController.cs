using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;
using AzmCrm.Application.Features.Automation.Commands.DeleteEscalationRule;
using AzmCrm.Application.Features.Automation.Commands.UpdateEscalationRule;
using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Features.Automation.Queries.GetEscalationRuleById;
using AzmCrm.Application.Features.Automation.Queries.GetEscalationRulesList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/EscalationRules" (case-insensitively matched as "api/escalationrules", but
// never "api/escalation-rules" — a hyphen is not a casing difference).
[Route("api/escalation-rules")]
public sealed class EscalationRulesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEscalationRuleRequest request, CancellationToken ct)
    {
        var command = new CreateEscalationRuleCommand(request.Name, request.Priority, request.OverdueMinutes);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/escalation-rules/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<EscalationRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetEscalationRuleByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<EscalationRuleListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] TicketPriority? priority = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetEscalationRulesListQuery(pageNumber, pageSize, priority, isActive), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateEscalationRuleRequest request, CancellationToken ct)
    {
        var command = new UpdateEscalationRuleCommand(id, request.Name, request.Priority, request.OverdueMinutes, request.IsActive);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteEscalationRuleCommand(id), ct);
        return ToNoContentResult(result);
    }
}
