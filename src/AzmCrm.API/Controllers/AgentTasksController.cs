using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;
using AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;
using AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;
using AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;
using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/AgentTasks" (case-insensitively matched as "api/agenttasks", but never
// "api/agent-tasks" — a hyphen is not a casing difference). Every frontend caller and this
// story's own plan expect "api/agent-tasks".
[Route("api/agent-tasks")]
public sealed class AgentTasksController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAgentTaskRequest request, CancellationToken ct)
    {
        var command = new CreateAgentTaskCommand(
            request.Title, request.Description, request.DueOn, request.CustomerId, request.TicketId);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/agent-tasks/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<AgentTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAgentTaskByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<AgentTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool? isCompleted = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAgentTasksListQuery(pageNumber, pageSize, isCompleted), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentTaskRequest request, CancellationToken ct)
    {
        var command = new UpdateAgentTaskCommand(id, request.Title, request.Description, request.DueOn);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/completion")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCompletion(
        Guid id, [FromBody] SetAgentTaskCompletionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SetAgentTaskCompletionCommand(id, request.IsCompleted), ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteAgentTaskCommand(id), ct);
        return ToNoContentResult(result);
    }
}
