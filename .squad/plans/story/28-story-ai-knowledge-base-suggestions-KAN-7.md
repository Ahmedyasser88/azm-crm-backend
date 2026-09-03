# Story 28 — AI-Suggested Knowledge Base Solutions for Tickets (Story: KAN-7)

## Prerequisites

- [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) completed: requires `KnowledgeArticle`, `IApplicationDbContext.KnowledgeArticles`.
- [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md) completed: requires `KnowledgeArticleStatus.Published` and `KnowledgeArticlePublicListItemDto`, the exact DTO shape this story's result item reuses.
- [23-story-knowledge-article-guide-steps-KAN-6.md](23-story-knowledge-article-guide-steps-KAN-6.md) completed: requires `IApplicationDbContext.KnowledgeArticleSteps`.
- [24-story-knowledge-base-search-KAN-6.md](24-story-knowledge-base-search-KAN-6.md) completed: this story's matching query is a ticket-scoped variant of `SearchKnowledgeArticlesQueryHandler` — read it in full before implementing (Context item 1).
- Story 05 completed: requires `Ticket`, `TicketsController`.
- **Does not depend on** [25-story-ai-ticket-summaries-KAN-7.md](25-story-ai-ticket-summaries-KAN-7.md) — this story reuses the existing `Contains`-based knowledge base search technique, not `IAiClient`, matching KAN-6 Story 24's own explicit scope decision to use substring search rather than an AI/embedding-based approach (see Story Goal below).

## Story Goal

Let an agent (or, later, the Story 29 chatbot) see which published knowledge base articles are most likely to help resolve a given ticket, satisfying KAN-7's "Suggest solutions from knowledge base" acceptance criterion.

Outcomes:
1. `GET /api/tickets/{id}/suggested-articles?maxResults={n}` is a new, `[Authorize]`-protected (default) action returning up to `maxResults` (default 5) `Published` knowledge base articles whose `Title`/`Content`/`Category`/`Tags` (or any attached step's `Title`/`Description`) case-insensitively contains a term drawn from the ticket's own `Title` — the exact multi-field `Contains` predicate `SearchKnowledgeArticlesQueryHandler` (KAN-6 Story 24) already implements, reused here keyed on `ticket.Title` instead of a user-supplied search string.
2. The result is a plain `IReadOnlyList<KnowledgeArticlePublicListItemDto>` (**not** a `PaginatedResult<T>`) ordered by `PublishedOn` descending — a deliberate, explicit choice: this endpoint always returns "the top few suggestions for this specific ticket," not a paged, browsable list, so paging metadata (`TotalCount`/`PageNumber`/`HasNextPage`) would be meaningless here, unlike Story 24's own paginated `Search` endpoint.
3. Zero matches is a valid, successful result (an empty list), not a failure — mirrors Story 24's `Search_with_no_matches_returns_empty_result_with_correct_TotalCount` precedent.
4. Only `Status == Published` articles are ever returned, enforced by reusing the exact same fixed `Where` clause Story 24's handler already uses — a `Draft` article is never suggested regardless of how well its content matches the ticket.

**Not in scope**: relevance ranking/scoring beyond `PublishedOn` descending (identical scope limitation to Story 24 — no semantic/embedding-based similarity is introduced here either, flagged as a follow-up below); combining the ticket's `Description` into the match term (only `Title` is used, kept simple and consistent — see Edge Cases); result snippets/highlighting; any UI/action to attach a suggested article to the ticket or auto-reply with it (that composition, if wanted, belongs to a future story combining this with Story 26's suggested-reply text).

## Context — Read These Files First

1. [24-story-knowledge-base-search-KAN-6.md](24-story-knowledge-base-search-KAN-6.md) — read in full, especially the `SearchKnowledgeArticlesQueryHandler` code block (its `Where` clause spans `Title`/`Content`/`Category`/`Tags` plus a correlated `KnowledgeArticleSteps.Any(...)` subquery) — this story's handler is a near-verbatim copy of that query, minus pagination, keyed on `ticket.Title`.
2. [src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryHandler.cs](../../../src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryHandler.cs) (full file, 54 lines, confirmed to exist at this exact path) — read in full for the exact, current `Where`/`OrderByDescending`/`Select` shape to copy.
3. [src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticlePublicListItemDto.cs](../../../src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticlePublicListItemDto.cs) (full file, 6 lines) — `public sealed record KnowledgeArticlePublicListItemDto(Guid Id, string Title, KnowledgeArticleType Type, string? Category, DateTime? PublishedOn);`, reused as-is by this story with no changes.
4. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs) lines 15-18 — the `dbContext.Tickets.FirstOrDefaultAsync(...) ?? throw new NotFoundException(...)` shape this story's handler reuses for its own ticket lookup.
5. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) lines 46-60 (`GetList` action) — the `[FromQuery] int pageSize = 20`-style optional-query-parameter shape this story's new action's `maxResults` parameter follows.
6. [src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryValidator.cs](../../../src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryValidator.cs) (15 lines, read in full) — the `RuleFor(x => x.PageSize).InclusiveBetween(1, 100)`-equivalent shape this story's `MaxResults` rule (`InclusiveBetween(1, 20)`) follows.

## Implementation tasks

### 1 — Application layer

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/SuggestKnowledgeArticlesForTicket/SuggestKnowledgeArticlesForTicketQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

public sealed record SuggestKnowledgeArticlesForTicketQuery(Guid TicketId, int MaxResults = 5)
    : IRequest<Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/SuggestKnowledgeArticlesForTicket/SuggestKnowledgeArticlesForTicketQueryValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

public sealed class SuggestKnowledgeArticlesForTicketQueryValidator
    : AbstractValidator<SuggestKnowledgeArticlesForTicketQuery>
{
    public SuggestKnowledgeArticlesForTicketQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 20)
            .WithMessage("Max Results must be between 1 and 20.");
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/SuggestKnowledgeArticlesForTicket/SuggestKnowledgeArticlesForTicketQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

internal sealed class SuggestKnowledgeArticlesForTicketQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SuggestKnowledgeArticlesForTicketQuery, Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>>
{
    public async Task<Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>> Handle(
        SuggestKnowledgeArticlesForTicketQuery request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        // Same multi-field Contains match Story 24's SearchKnowledgeArticlesQueryHandler uses,
        // keyed on the ticket's own Title instead of a user-supplied search string, and capped
        // to MaxResults instead of paginated — see this story's Story Goal for the reasoning.
        var term = ticket.Title.Trim().ToLower();

        var matches = await dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Content.ToLower().Contains(term) ||
                (a.Category != null && a.Category.ToLower().Contains(term)) ||
                (a.Tags != null && a.Tags.ToLower().Contains(term)) ||
                dbContext.KnowledgeArticleSteps.Any(s =>
                    s.KnowledgeArticleId == a.Id &&
                    (s.Title.ToLower().Contains(term) || s.Description.ToLower().Contains(term))))
            .OrderByDescending(a => a.PublishedOn)
            .Take(request.MaxResults)
            .Select(a => new KnowledgeArticlePublicListItemDto(a.Id, a.Title, a.Type, a.Category, a.PublishedOn))
            .ToListAsync(ct);

        return Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>.Success(matches);
    }
}
```

### 2 — API layer

**Edit file: `src/AzmCrm.API/Controllers/TicketsController.cs`** — add `using AzmCrm.Application.Features.KnowledgeBase.DTOs;` and `using AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;`, then append a new action:

```csharp
[HttpGet("{id:guid}/suggested-articles")]
[ProducesResponseType(typeof(Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> SuggestArticles(Guid id, [FromQuery] int maxResults = 5, CancellationToken ct = default)
{
    var result = await mediator.Send(new SuggestKnowledgeArticlesForTicketQuery(id, maxResults), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **Ticket id does not exist** — throws `NotFoundException` before any knowledge base query, returning 404, same as every other `Get*ById`-shaped handler in this codebase.
- **Ticket `Title` matches zero published articles** — returns an empty list with `Result.IsSuccess == true`; this is the expected, common case, not an error (matches Story 24's identical precedent for its own zero-match case).
- **A `Draft` article whose content matches the ticket's title exactly** — never returned; the `Where(a => a.Status == KnowledgeArticleStatus.Published)` clause runs before the text-match clause, identical to Story 24's handler.
- **A ticket with a short/generic `Title`** (e.g. `"Help"`) — may return broad, low-precision matches; an accepted, documented consequence of reusing `Contains`-based substring matching rather than a ranked/semantic search — this story does not attempt to improve on Story 24's own match-quality scope decision.
- **A soft-deleted article or step** — excluded automatically via the same `HasQueryFilter(x => !x.IsDeleted)` configuration Stories 21/23 already applied to `KnowledgeArticle`/`KnowledgeArticleStep`, inherited by this handler with no extra code.
- **`maxResults` outside 1-20** — rejected by `SuggestKnowledgeArticlesForTicketQueryValidator`'s `InclusiveBetween(1, 20)` rule, returning a 400 before the handler runs.
- **Follow-up flagged, not implemented**: incorporating the ticket's `Description` (not just `Title`) into the match term; upgrading to semantic/embedding-based similarity search (mirrors Story 24's own identically-worded flagged follow-up for `tsvector`/`GIN`-based full-text search) if `Contains`-based matching proves too imprecise at production scale.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/SuggestKnowledgeArticlesForTicketQueryHandlerTests.cs`** (seed data the same way `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/SearchKnowledgeArticlesQueryHandlerTests.cs` does, plus a `Ticket`):
   - `Suggest_returns_articles_matching_ticket_Title`
   - `Suggest_for_missing_ticket_throws_NotFoundException`
   - `Suggest_with_no_matches_returns_empty_list`
   - `Suggest_excludes_Draft_articles_even_on_exact_Title_match`
   - `Suggest_excludes_soft_deleted_articles`
   - `Suggest_respects_MaxResults_cap` (seed more matching articles than `MaxResults` and assert the returned count is capped)
   - `Suggest_orders_by_PublishedOn_descending`
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/SuggestKnowledgeArticlesForTicketQueryValidatorTests.cs`** — `Empty_TicketId_fails`; `MaxResults_below_1_fails`; `MaxResults_above_20_fails`; `Valid_request_passes`.
3. All new tests use `TestApplicationDbContext.Create()` and `StubLocalizationService` — no schema/DbSet changes in this story.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test**: publish a knowledge base article (KAN-6 Stories 21-22) titled `"How do I reset my password?"`; create a ticket titled `"password reset not working"`; call `GET /api/tickets/{id}/suggested-articles` and confirm the article is returned. Create a second, unpublished `Draft` article with an identically matching title and confirm it is never returned.

## Done Criteria

- [ ] `GET /api/tickets/{id}/suggested-articles` returns up to `maxResults` `Published` knowledge base articles matching the ticket's title.
- [ ] Zero matches returns an empty, successful result rather than a failure.
- [ ] Only `Published` articles are ever returned, regardless of match strength.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-7's "Suggest solutions from knowledge base" acceptance criterion.
