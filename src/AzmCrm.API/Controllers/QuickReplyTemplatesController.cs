using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;
using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/QuickReplyTemplates" (case-insensitively matched as
// "api/quickreplytemplates", but never "api/quick-reply-templates" — a hyphen is not a casing
// difference). Every frontend caller and this story's own plan expect
// "api/quick-reply-templates".
[Route("api/quick-reply-templates")]
public sealed class QuickReplyTemplatesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateQuickReplyTemplateCommand(request.Title, request.Body), ct);

        return ToCreatedResult(result, id => $"/api/quick-reply-templates/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<QuickReplyTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetQuickReplyTemplateByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<QuickReplyTemplateListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetQuickReplyTemplatesListQuery(pageNumber, pageSize, search), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateQuickReplyTemplateCommand(id, request.Title, request.Body), ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteQuickReplyTemplateCommand(id), ct);
        return ToNoContentResult(result);
    }
}
