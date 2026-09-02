using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;
using AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;
using AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;
using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Features.Automation.Queries.GetAssignmentRuleById;
using AzmCrm.Application.Features.Automation.Queries.GetAssignmentRulesList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/AssignmentRules" (case-insensitively matched as "api/assignmentrules", but
// never "api/assignment-rules" — a hyphen is not a casing difference).
[Route("api/assignment-rules")]
public sealed class AssignmentRulesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentRuleRequest request, CancellationToken ct)
    {
        var command = new CreateAssignmentRuleCommand(
            request.Name, request.Category, request.Priority, request.AssignedToUserId, request.EvaluationOrder);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/assignment-rules/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<AssignmentRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAssignmentRuleByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<AssignmentRuleListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] TicketCategory? category = null, [FromQuery] TicketPriority? priority = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetAssignmentRulesListQuery(pageNumber, pageSize, category, priority, isActive), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateAssignmentRuleRequest request, CancellationToken ct)
    {
        var command = new UpdateAssignmentRuleCommand(
            id, request.Name, request.Category, request.Priority,
            request.AssignedToUserId, request.EvaluationOrder, request.IsActive);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteAssignmentRuleCommand(id), ct);
        return ToNoContentResult(result);
    }
}
