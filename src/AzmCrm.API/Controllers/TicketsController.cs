using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Tickets.Commands.AssignTicket;
using AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;
using AzmCrm.Application.Features.Tickets.Commands.CreateTicket;
using AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;
using AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;
using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Features.Tickets.Queries.GetTicketById;
using AzmCrm.Application.Features.Tickets.Queries.GetTicketHistory;
using AzmCrm.Application.Features.Tickets.Queries.GetTicketsList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class TicketsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var command = new CreateTicketCommand(
            request.CustomerId, request.Title, request.Description, request.Category, request.Priority);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/tickets/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<TicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTicketByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<TicketListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null, [FromQuery] TicketStatus? status = null,
        [FromQuery] TicketCategory? category = null, [FromQuery] TicketPriority? priority = null,
        [FromQuery] string? search = null, [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] bool? isEscalated = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetTicketsListQuery(
                pageNumber, pageSize, customerId, status, category, priority, search,
                assignedToUserId, isEscalated), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var command = new UpdateTicketCommand(id, request.Title, request.Description, request.Category, request.Priority);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignTicketCommand(id, request.AssignedToUserId), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ChangeTicketStatusCommand(id, request.Status), ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new EscalateTicketCommand(id, request.Reason), ct);
        return ToResult(result);
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(Result<PaginatedResult<TicketHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTicketHistoryQuery(id, pageNumber, pageSize), ct);
        return ToResult(result);
    }
}
