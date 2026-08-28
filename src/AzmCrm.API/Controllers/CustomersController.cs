using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Customers.Commands.CreateCustomer;
using AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;
using AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;
using AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;
using AzmCrm.Application.Features.Customers.Commands.UpdateCustomer;
using AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerById;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerNotes;
using AzmCrm.Application.Features.Customers.Queries.GetCustomersList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class CustomersController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerCommand(
            request.FullName, request.CompanyName, request.Email, request.PhoneNumber,
            request.AddressLine1, request.AddressLine2, request.City, request.State,
            request.PostalCode, request.Country);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/customers/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCustomerByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<CustomerListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCustomersListQuery(pageNumber, pageSize, search), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var command = new UpdateCustomerCommand(
            id, request.FullName, request.CompanyName, request.Email, request.PhoneNumber,
            request.AddressLine1, request.AddressLine2, request.City, request.State,
            request.PostalCode, request.Country);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCustomerCommand(id), ct);
        return ToNoContentResult(result);
    }

    [HttpPost("{customerId:guid}/interactions")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddInteraction(
        Guid customerId, [FromBody] CreateInteractionRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerInteractionCommand(
            customerId, request.Type, request.Subject, request.Description, request.OccurredOn);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/customers/{customerId}/interactions/{id}");
    }

    [HttpGet("{customerId:guid}/interactions")]
    [ProducesResponseType(typeof(Result<PaginatedResult<CustomerInteractionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInteractions(
        Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCustomerInteractionsQuery(customerId, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpPost("{customerId:guid}/notes")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(Guid customerId, [FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerNoteCommand(customerId, request.Content);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/customers/{customerId}/notes/{id}");
    }

    [HttpGet("{customerId:guid}/notes")]
    [ProducesResponseType(typeof(Result<PaginatedResult<CustomerNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotes(
        Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCustomerNotesQuery(customerId, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpPost("{customerId:guid}/attachments")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAttachment(Guid customerId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();

        var command = new UploadCustomerAttachmentCommand(
            customerId, file.FileName, file.ContentType, file.Length, stream);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/customers/{customerId}/attachments/{id}");
    }

    [HttpGet("{customerId:guid}/attachments")]
    [ProducesResponseType(typeof(Result<PaginatedResult<CustomerAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachments(
        Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCustomerAttachmentsQuery(customerId, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpGet("{customerId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid customerId, Guid attachmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCustomerAttachmentContentQuery(customerId, attachmentId), ct);

        return File(result.Data!.Content, result.Data.ContentType, result.Data.FileName);
    }
}
