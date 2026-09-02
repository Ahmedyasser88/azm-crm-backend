using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;
using AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;
using AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;
using AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;
using AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;
using AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;
using AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;
using AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;
using AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticlesList;
using AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;
using AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticlesList;
using AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

// Explicit kebab-case route: without this override, ApiControllerBase's "api/[controller]"
// resolves to "api/KnowledgeArticles", never the hyphenated form every other multi-word
// controller in this codebase uses (see SlaPoliciesController/QuickReplyTemplatesController).
[Route("api/knowledge-articles")]
public sealed class KnowledgeArticlesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeArticleRequest request, CancellationToken ct)
    {
        var command = new CreateKnowledgeArticleCommand(
            request.Title, request.Content, request.Type, request.Category, request.Tags);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/knowledge-articles/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<KnowledgeArticleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetKnowledgeArticleByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<KnowledgeArticleListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] KnowledgeArticleType? type = null, [FromQuery] KnowledgeArticleStatus? status = null,
        [FromQuery] string? category = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetKnowledgeArticlesListQuery(pageNumber, pageSize, type, status, category), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateKnowledgeArticleRequest request, CancellationToken ct)
    {
        var command = new UpdateKnowledgeArticleCommand(
            id, request.Title, request.Content, request.Type, request.Category, request.Tags);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteKnowledgeArticleCommand(id), ct);
        return ToNoContentResult(result);
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new PublishKnowledgeArticleCommand(id), ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new UnpublishKnowledgeArticleCommand(id), ct);
        return ToResult(result);
    }

    // Public, unauthenticated: customers browse published knowledge base content directly,
    // per KAN-6's "so common issues can be resolved without creating tickets."
    [AllowAnonymous]
    [HttpGet("published")]
    [ProducesResponseType(typeof(Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublishedList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] KnowledgeArticleType? type = null, [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetPublishedKnowledgeArticlesListQuery(pageNumber, pageSize, type, category), ct);
        return ToResult(result);
    }

    [AllowAnonymous]
    [HttpGet("published/{id:guid}")]
    [ProducesResponseType(typeof(Result<KnowledgeArticlePublicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublishedById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPublishedKnowledgeArticleByIdQuery(id), ct);
        return ToResult(result);
    }

    [AllowAnonymous]
    [HttpGet("search")]
    [ProducesResponseType(typeof(Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string query, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new SearchKnowledgeArticlesQuery(query, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/steps")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStep(
        Guid id, [FromBody] AddKnowledgeArticleStepRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AddKnowledgeArticleStepCommand(id, request.StepNumber, request.Title, request.Description), ct);
        return ToCreatedResult(result, stepId => $"/api/knowledge-articles/{id}/steps/{stepId}");
    }

    [HttpPut("{id:guid}/steps/{stepId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStep(
        Guid id, Guid stepId, [FromBody] UpdateKnowledgeArticleStepRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateKnowledgeArticleStepCommand(id, stepId, request.StepNumber, request.Title, request.Description),
            ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}/steps/{stepId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStep(Guid id, Guid stepId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteKnowledgeArticleStepCommand(id, stepId), ct);
        return ToNoContentResult(result);
    }
}
