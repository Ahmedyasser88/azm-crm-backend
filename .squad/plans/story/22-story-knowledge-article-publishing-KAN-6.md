# Story 22 — Publish Workflow for Help Articles & Guides (Story: KAN-6)

## Prerequisites

- [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) completed: requires `KnowledgeArticle`/`KnowledgeArticleType`/`KnowledgeArticleStatus`, `IApplicationDbContext.KnowledgeArticles`, `KnowledgeArticlesController`, `KnowledgeArticleDto`, and `TestApplicationDbContext`.
- [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md) — read only for its precedent of a controller mixing `[Authorize]` (class-level default) and per-action `[AllowAnonymous]` overrides; this story's `ConversationsController`-style public actions follow that same mixed-authorization shape (see [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) lines 89, 103, 125, 140, 156, each a bare `[AllowAnonymous]` line directly above one public action).

## Story Goal

Let an agent publish a `Draft` `KnowledgeArticle` so customers can read it, and unpublish it again, satisfying KAN-6's "Publish help articles and guides" acceptance criterion. Publishing exposes the article through a new, unauthenticated read surface — this codebase's first customer-facing (not just agent-facing) knowledge base endpoint.

Outcomes:
1. `POST /api/knowledge-articles/{id}/publish` flips a `Draft` article to `Published`, stamping `PublishedOn = DateTime.UtcNow` and `PublishedBy` from `ICurrentUserService.UserId`. Publishing an already-`Published` article is a no-op success (idempotent), matching this codebase's precedent of `EscalateTicketCommandHandler` (KAN-2 Story 07) tolerating a repeat call.
2. `POST /api/knowledge-articles/{id}/unpublish` flips a `Published` article back to `Draft`, clearing `PublishedOn`/`PublishedBy` to `null`. Unpublishing an already-`Draft` article is likewise idempotent.
3. `GET /api/knowledge-articles/published` — a new, `[AllowAnonymous]` action — lists only `Status == Published` articles, optionally filtered by `Type`/`Category`, ordered newest-published-first. This is the first read endpoint on `KnowledgeArticlesController` reachable without a bearer token.
4. `GET /api/knowledge-articles/published/{id}` — also `[AllowAnonymous]` — returns a single published article's full content, or 404 if the id doesn't exist **or** exists but is `Draft` (a customer must never be able to probe a draft's existence or content by guessing its id).
5. Both public endpoints project onto a new `KnowledgeArticlePublicDto` that excludes `CreatedBy`/`UpdatedBy`/`PublishedBy` (internal user ids with no meaning to a customer) — the existing, agent-only `GetKnowledgeArticleByIdQuery`/`KnowledgeArticleDto` from Story 21 are untouched and keep returning every field to authenticated callers regardless of `Status`.

**Not in scope**: a "schedule publish for later" date; requiring a specific role to publish (every authenticated agent can, matching KAN-4 Story 15's identical "no role model beyond `ApplicationUser`" precedent); notifying anyone when an article is published/unpublished; and re-publishing automatically restamping `PublishedOn` to the original first-publish time — every `Publish` call (including a repeat one) sets `PublishedOn` to the current `DateTime.UtcNow`, so unpublish-then-republish produces a new timestamp, which is acceptable because there is no "originally published on" requirement in the acceptance criteria.

## Context — Read These Files First

1. [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) — read in full for `KnowledgeArticle`'s exact shape and the `KnowledgeArticlesController`/CQRS file layout this story extends.
2. [src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandHandler.cs) — read in full as the precedent for an idempotent state-flip command (a repeat call on an already-escalated ticket returns `Result.Success()` without re-writing history), the same idempotency this story's `PublishKnowledgeArticleCommandHandler`/`UnpublishKnowledgeArticleCommandHandler` implement.
3. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) lines 1-40 — read for the `using Microsoft.AspNetCore.Authorization;` + per-action `[AllowAnonymous]` pattern layered on top of `ApiControllerBase`'s class-level `[Authorize]`; `KnowledgeArticlesController`'s two new public actions add the same `[AllowAnonymous]` line directly above each action method, with every other existing action on that controller left as-is (still requiring `[Authorize]`).
4. [src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs) — the `AsQueryable()` + sequential `Where` shape `GetPublishedKnowledgeArticlesQueryHandler` follows, with a fixed, hardcoded `Where(a => a.Status == KnowledgeArticleStatus.Published)` clause that no caller can override (unlike the optional `Type`/`Category` filters).
5. [src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs) — read in full (9 lines); `PublishKnowledgeArticleCommandHandler` injects this to read `UserId` for the `PublishedBy` stamp, the same dependency `DeleteQuickReplyTemplateCommandHandler`/`DeleteKnowledgeArticleCommandHandler` (Story 21) already use for `DeletedBy`.

## Implementation tasks

### 1 — Application layer: publish/unpublish commands

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/PublishKnowledgeArticle/PublishKnowledgeArticleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;

public sealed record PublishKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/PublishKnowledgeArticle/PublishKnowledgeArticleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;

internal sealed class PublishKnowledgeArticleCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<PublishKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(PublishKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        if (article.Status != KnowledgeArticleStatus.Published)
        {
            article.Status = KnowledgeArticleStatus.Published;
            article.PublishedOn = DateTime.UtcNow;
            article.PublishedBy = currentUserService.UserId ?? Guid.Empty;

            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/PublishKnowledgeArticle/PublishKnowledgeArticleCommandValidator.cs`** — copy [DeleteQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandValidator.cs)'s single `RuleFor(x => x.Id).NotEmpty()...` rule exactly, substituting the command type name.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UnpublishKnowledgeArticle/UnpublishKnowledgeArticleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;

public sealed record UnpublishKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UnpublishKnowledgeArticle/UnpublishKnowledgeArticleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;

internal sealed class UnpublishKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UnpublishKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UnpublishKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        if (article.Status != KnowledgeArticleStatus.Draft)
        {
            article.Status = KnowledgeArticleStatus.Draft;
            article.PublishedOn = null;
            article.PublishedBy = null;

            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UnpublishKnowledgeArticle/UnpublishKnowledgeArticleCommandValidator.cs`** — same single `Id` rule as `PublishKnowledgeArticleCommandValidator`.

### 2 — Application layer: public read surface

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticlePublicDto.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticlePublicDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type,
    string? Category, string? Tags, DateTime? PublishedOn);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticlePublicListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticlePublicListItemDto(
    Guid Id, string Title, KnowledgeArticleType Type, string? Category, DateTime? PublishedOn);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticleById/GetPublishedKnowledgeArticleByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;

public sealed record GetPublishedKnowledgeArticleByIdQuery(Guid Id) : IRequest<Result<KnowledgeArticlePublicDto>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticleById/GetPublishedKnowledgeArticleByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;

internal sealed class GetPublishedKnowledgeArticleByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetPublishedKnowledgeArticleByIdQuery, Result<KnowledgeArticlePublicDto>>
{
    public async Task<Result<KnowledgeArticlePublicDto>> Handle(
        GetPublishedKnowledgeArticleByIdQuery request, CancellationToken ct)
    {
        // The Status == Published condition is part of the lookup predicate itself (not a
        // post-fetch check) so a Draft article's id 404s exactly like a nonexistent id —
        // a customer must never learn a draft exists by probing ids. See Story Goal, outcome 4.
        var article = await dbContext.KnowledgeArticles
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.Status == KnowledgeArticleStatus.Published, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        var dto = new KnowledgeArticlePublicDto(
            article.Id, article.Title, article.Content, article.Type,
            article.Category, article.Tags, article.PublishedOn);

        return Result<KnowledgeArticlePublicDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticleById/GetPublishedKnowledgeArticleByIdQueryValidator.cs`** — single `RuleFor(x => x.Id).NotEmpty()...` rule, same shape as `DeleteQuickReplyTemplateCommandValidator`.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticlesList/GetPublishedKnowledgeArticlesListQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticlesList;

public sealed record GetPublishedKnowledgeArticlesListQuery(
    int PageNumber = 1, int PageSize = 20,
    KnowledgeArticleType? Type = null, string? Category = null
) : IRequest<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticlesList/GetPublishedKnowledgeArticlesListQueryHandler.cs`** — same `AsQueryable()`/sequential-`Where` shape as [GetKnowledgeArticlesListQueryHandler.cs](21-story-knowledge-base-core-crud-KAN-6.md) (Story 21), but starting from `dbContext.KnowledgeArticles.Where(a => a.Status == KnowledgeArticleStatus.Published)` (not optional — always applied), then the optional `Type`/`Category` filters, ordered `.OrderByDescending(a => a.PublishedOn)` (newest-published-first, the natural reading order for a customer-facing list), projected into `KnowledgeArticlePublicListItemDto`.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticlesList/GetPublishedKnowledgeArticlesListQueryValidator.cs`** — copy [GetQuickReplyTemplatesListQueryValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs)'s `PageNumber`/`PageSize` rules exactly.

### 3 — API layer

**Edit file: `src/AzmCrm.API/Controllers/KnowledgeArticlesController.cs`** (created by Story 21) — add `using Microsoft.AspNetCore.Authorization;` plus four new actions:

```csharp
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
```

Add the corresponding `using AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;`, `...Commands.UnpublishKnowledgeArticle;`, `...Queries.GetPublishedKnowledgeArticleById;`, and `...Queries.GetPublishedKnowledgeArticlesList;` lines.

**Route ordering note**: ASP.NET Core route matching resolves `GET api/knowledge-articles/published` and `GET api/knowledge-articles/published/{id:guid}` against Story 21's existing `GET api/knowledge-articles/{id:guid}` (`GetById`) unambiguously — `published` is a literal segment, not a `{id:guid}` match, so no route ever collides; action declaration order in the file does not matter.

## Edge Cases & Failure Modes

- **Publishing an already-`Published` article** — `PublishKnowledgeArticleCommandHandler`'s `if (article.Status != KnowledgeArticleStatus.Published)` guard makes the call a no-op success; `PublishedOn`/`PublishedBy` are **not** re-stamped on a repeat call while already published (only a full unpublish-then-republish cycle produces a new `PublishedOn`). See `EscalateTicketCommandHandler`'s identical idempotency precedent (Story Goal, Context item 2).
- **Unpublishing an already-`Draft` article** — symmetric no-op via `UnpublishKnowledgeArticleCommandHandler`'s guard; `PublishedOn`/`PublishedBy` are already `null` and stay `null`.
- **A customer requests `GET /api/knowledge-articles/published/{id}` for a `Draft` article's real id** — `GetPublishedKnowledgeArticleByIdQueryHandler`'s combined `a.Id == request.Id && a.Status == KnowledgeArticleStatus.Published` predicate returns no row, so the handler throws the same `NotFoundException` it would for a nonexistent id — a 404 either way, with no observable difference that would let a customer distinguish "doesn't exist" from "exists but unpublished."
- **A customer requests `GET /api/knowledge-articles/published/{id}` for a soft-deleted article's id** — `KnowledgeArticleConfiguration`'s `HasQueryFilter(a => !a.IsDeleted)` (Story 21) applies to every query against `dbContext.KnowledgeArticles`, including this one, so a soft-deleted article 404s the same way regardless of its `Status`.
- **An agent unpublishes an article that customers currently have open in a browser tab** — no invalidation/notification mechanism exists; the next `GET /api/knowledge-articles/published/{id}` call (e.g. a page refresh) 404s. Documented as an accepted gap, matching this codebase's existing lack of any real-time push for content changes outside the Live Chat `ChatHub` (KAN-3 Story 12).
- **Publishing/unpublishing a nonexistent article id** — both handlers throw `NotFoundException` from the same `?? throw` pattern as Story 21's `Update`/`Delete` handlers, rendered as 404 by `ApiControllerBase`'s exception-to-response mapping (unchanged by this story).

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/PublishKnowledgeArticleCommandHandlerTests.cs`** — `Publish_Draft_article_sets_Published_status_and_stamps_PublishedOn_and_PublishedBy`; `Publish_already_Published_article_is_idempotent_noop`; `Publish_missing_article_throws_NotFoundException`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/UnpublishKnowledgeArticleCommandHandlerTests.cs`** — `Unpublish_Published_article_sets_Draft_status_and_clears_PublishedOn_and_PublishedBy`; `Unpublish_already_Draft_article_is_idempotent_noop`; `Unpublish_missing_article_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetPublishedKnowledgeArticleByIdQueryHandlerTests.cs`** — `GetPublishedById_returns_Published_article`; `GetPublishedById_Draft_article_throws_NotFoundException`; `GetPublishedById_missing_article_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetPublishedKnowledgeArticlesListQueryHandlerTests.cs`** — `List_returns_only_Published_articles_ordered_by_PublishedOn_descending`; `List_excludes_Draft_articles`; `List_filters_by_Type`; `List_filters_by_Category`.
5. All new tests use `TestApplicationDbContext.Create()`, `StubLocalizationService`, and a stub `ICurrentUserService` (already established by prior stories, e.g. Story 15/21's `DeleteQuickReplyTemplateCommandHandlerTests`/`DeleteKnowledgeArticleCommandHandlerTests`) — no new test doubles are needed.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test:** Create a `Draft` article via Story 21's `POST /api/knowledge-articles` (with a bearer token). `GET /api/knowledge-articles/published` (no `Authorization` header) confirms it does **not** appear. `POST /api/knowledge-articles/{id}/publish` (with a bearer token), confirm 200. `GET /api/knowledge-articles/published` (no token) now includes it; `GET /api/knowledge-articles/published/{id}` (no token) returns its full content. `POST /api/knowledge-articles/{id}/unpublish`, confirm the two public endpoints 404/omit it again.

## Done Criteria

- [ ] `POST /api/knowledge-articles/{id}/publish` and `POST /api/knowledge-articles/{id}/unpublish` work, are idempotent, and correctly stamp/clear `PublishedOn`/`PublishedBy`.
- [ ] `GET /api/knowledge-articles/published` and `GET /api/knowledge-articles/published/{id}` are reachable without an `Authorization` header and only ever return `Published` articles.
- [ ] A `Draft` article's id 404s on `GET /api/knowledge-articles/published/{id}` indistinguishably from a nonexistent id.
- [ ] Every other existing `KnowledgeArticlesController` action (Story 21) still requires authentication.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-6's "Publish help articles and guides" acceptance criterion.
