# Story 24 — Full-Text Search Across Knowledge Base (Story: KAN-6)

## Prerequisites

- [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) completed: requires `KnowledgeArticle`, `IApplicationDbContext.KnowledgeArticles`, `KnowledgeArticlesController`.
- [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md) completed: requires `KnowledgeArticleStatus.Published`, the `[AllowAnonymous]` public-endpoint pattern on `KnowledgeArticlesController`, and `KnowledgeArticlePublicListItemDto` as the DTO shape this story's result item follows.
- [23-story-knowledge-article-guide-steps-KAN-6.md](23-story-knowledge-article-guide-steps-KAN-6.md) completed: requires `IApplicationDbContext.KnowledgeArticleSteps` — "search across all knowledge base content" (KAN-6's exact wording) must also match a guide's step titles/descriptions, not just the parent article's `Title`/`Content`/`Tags`.

## Story Goal

Let a customer or agent search across every field of every published knowledge base article — `Title`, `Content`, `Category`, `Tags`, and every attached step's `Title`/`Description` — with a single keyword query, satisfying KAN-6's "Full-text search across all knowledge base content" acceptance criterion.

Outcomes:
1. `GET /api/knowledge-articles/search?query={term}` is a new, `[AllowAnonymous]` action (matching Story 22's public-read precedent — a customer must be able to search without an account, per KAN-6's "so common issues can be resolved without creating tickets") that returns paginated `KnowledgeArticlePublicListItemDto`-shaped results, restricted to `Status == Published` articles only — a `Draft` article's content, however well it matches `term`, must never appear here, mirroring Story 22's identical restriction on `GetPublishedKnowledgeArticlesListQueryHandler`.
2. A match on **any** of `Title`, `Content`, `Category`, `Tags` (case-insensitive substring), or **any** of that article's steps' `Title`/`Description` (case-insensitive substring) includes the parent article exactly once in the results — an article with three matching steps is not returned three times.
3. Search is implemented as a case-insensitive `.ToLower().Contains(term)` predicate across those fields — the same technique every other list query's `Search` parameter already uses in this codebase (`GetCustomersListQueryHandler`, `GetQuickReplyTemplatesListQueryHandler`, `GetTicketsListQueryHandler`). **This is a deliberate, explicit scope decision, not an oversight**: true PostgreSQL full-text search (`tsvector`/`tsquery`, `GIN` indexes, relevance ranking, stemming) is not used anywhere in this codebase today, and introducing it for the first time here — a new indexing strategy, a computed/generated column, and Npgsql-specific EF Core function translation with no existing precedent to follow or test pattern to copy — is out of scope for this story. `Contains`-based matching satisfies the acceptance criterion's literal requirement ("full-text search across all knowledge base content" — a query can match any field, anywhere in that field's text) without introducing unproven infrastructure. Upgrading to genuine `tsvector` ranking is flagged as a follow-up in Edge Cases.
4. An empty or whitespace-only `query` returns a validation failure (400), not an unfiltered "list everything" result — `query` is required, unlike `GetKnowledgeArticlesList`'s/`GetPublishedKnowledgeArticlesList`'s optional `search`-equivalent filters.

**Not in scope**: relevance ranking/scoring (results are ordered `PublishedOn` descending, the same tiebreak `GetPublishedKnowledgeArticlesListQueryHandler` uses — a keyword match does not currently affect result order at all); search result highlighting/snippets around the matched term; typo-tolerance/fuzzy matching; searching `Draft` articles (an agent managing content still uses Story 21's `GET /api/knowledge-articles?category=...`/`?type=...` filters, not this endpoint); and search analytics (tracking what customers search for).

## Context — Read These Files First

1. [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md) — read in full for `KnowledgeArticlePublicListItemDto`'s exact field list and `GetPublishedKnowledgeArticlesListQueryHandler`'s exact `Where(a => a.Status == KnowledgeArticleStatus.Published)` + ordering shape this story's handler mirrors.
2. [23-story-knowledge-article-guide-steps-KAN-6.md](23-story-knowledge-article-guide-steps-KAN-6.md) — read in full for `KnowledgeArticleStep`'s shape and `IApplicationDbContext.KnowledgeArticleSteps`.
3. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) lines 1-36 — read for the exact multi-field `.ToLower().Contains(term)` `Where` clause shape (`t.Title.ToLower().Contains(term) || (t.Description != null && t.Description.ToLower().Contains(term))`) this story's `SearchKnowledgeArticlesQueryHandler` extends to five/six fields, including a step-level `Any(...)` subclause.
4. [src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryHandler.cs) — read in full again for the `!string.IsNullOrWhiteSpace(request.Search)` guard shape; this story's validator makes the equivalent field **required** instead of guarding it as optional (see Story Goal outcome 4), so the guard itself is not reused, only the `.Trim().ToLower()` normalization line.
5. [src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs) (15 lines, read in full) — `SearchKnowledgeArticlesQueryValidator`'s `PageNumber`/`PageSize` rules copy this exactly, plus a new leading `RuleFor(x => x.Query).NotEmpty()...` rule that has no equivalent in this file (since `Search` there is optional).

## Implementation tasks

### 1 — Application layer

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;

public sealed record SearchKnowledgeArticlesQuery(
    string Query, int PageNumber = 1, int PageSize = 20
) : IRequest<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;

internal sealed class SearchKnowledgeArticlesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SearchKnowledgeArticlesQuery, Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>
{
    public async Task<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>> Handle(
        SearchKnowledgeArticlesQuery request, CancellationToken ct)
    {
        var term = request.Query.Trim().ToLower();

        // Matches on the parent article's own text fields, or on any of its steps' text fields
        // (KAN-6 asks for search "across all knowledge base content", which this codebase's
        // model expresses as KnowledgeArticle + child KnowledgeArticleStep rows — see Story 23).
        // Draft articles are excluded the same way GetPublishedKnowledgeArticlesListQueryHandler
        // excludes them (Story 22): Status == Published is a fixed clause, not an optional filter.
        var query = dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Content.ToLower().Contains(term) ||
                (a.Category != null && a.Category.ToLower().Contains(term)) ||
                (a.Tags != null && a.Tags.ToLower().Contains(term)) ||
                dbContext.KnowledgeArticleSteps.Any(s =>
                    s.KnowledgeArticleId == a.Id &&
                    (s.Title.ToLower().Contains(term) || s.Description.ToLower().Contains(term))));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.PublishedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new KnowledgeArticlePublicListItemDto(a.Id, a.Title, a.Type, a.Category, a.PublishedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<KnowledgeArticlePublicListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;

public sealed class SearchKnowledgeArticlesQueryValidator : AbstractValidator<SearchKnowledgeArticlesQuery>
{
    public SearchKnowledgeArticlesQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Query"]);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
```

### 2 — API layer

**Edit file: `src/AzmCrm.API/Controllers/KnowledgeArticlesController.cs`** — add one new action (placed among the other `[AllowAnonymous]` actions added by Story 22):

```csharp
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
```

Add `using AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;`.

**Route ordering note**: `GET api/knowledge-articles/search` is a literal path segment, matched before Story 21's `GET api/knowledge-articles/{id:guid}` route constraint could ever apply (`search` is not a valid `Guid`), so no collision is possible — same reasoning Story 22 documented for its `published`/`published/{id}` routes.

## Edge Cases & Failure Modes

- **Empty or whitespace-only `query`** — rejected by `SearchKnowledgeArticlesQueryValidator`'s `NotEmpty()` rule before the handler runs, returning a 400 `Result` — see Story Goal outcome 4. This is the only list-style query in this feature where the equivalent free-text field is mandatory rather than optional.
- **A term that matches a `Draft` article only** — never returned; the `Where(a => a.Status == KnowledgeArticleStatus.Published)` clause is evaluated before the text-match clause, so a draft is excluded regardless of how well it matches.
- **A term that matches only a step's `Title`/`Description`, not any field on the parent article itself** — the parent article is still returned exactly once, via the `dbContext.KnowledgeArticleSteps.Any(s => s.KnowledgeArticleId == a.Id && ...)` correlated-subquery clause; the returned `KnowledgeArticlePublicListItemDto` carries only the article's own summary fields (`Id`, `Title`, `Type`, `Category`, `PublishedOn`) — it does not indicate *which* step matched or include a snippet (see Story Goal, "Not in scope").
- **An article with multiple steps that all match `term`** — still returned exactly once; `Any(...)` short-circuits to a single boolean per article, it does not multiply result rows the way a `Join` against `KnowledgeArticleSteps` would.
- **A soft-deleted article or a soft-deleted step** — both are excluded automatically: `KnowledgeArticleConfiguration`'s and `KnowledgeArticleStepConfiguration`'s `HasQueryFilter(x => !x.IsDeleted)` (Stories 21/23) apply to `dbContext.KnowledgeArticles` and `dbContext.KnowledgeArticleSteps` respectively, including inside the correlated subquery.
- **Very short `query` values (e.g. a single character)** — allowed; `NotEmpty()` only rejects an empty/whitespace string, not a short one. A one-character query is expected to (and will) match broadly across `Content`, which is an accepted consequence of substring-`Contains` search rather than token-based full-text search (see Story Goal outcome 3's explicit scope note).
- **Follow-up flagged, not implemented**: upgrading this story's `Contains`-based matching to genuine PostgreSQL `tsvector`/`tsquery` full-text search (with `GIN` indexing and relevance ranking) if `Contains`-based `LIKE '%term%'` scanning becomes a performance problem at production content volume — no index accelerates the current implementation's `Contains` calls on `Title`/`Content`/`Category`/`Tags`, so every search is a full table (and correlated subquery) scan. Acceptable at this codebase's current scale (mirrors every other unindexed `Contains`-based search already shipped, e.g. `GetTicketsListQueryHandler`), flagged here explicitly per this story's own Story Goal reasoning.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/SearchKnowledgeArticlesQueryHandlerTests.cs`**:
   - `Search_matches_Title`
   - `Search_matches_Content`
   - `Search_matches_Category`
   - `Search_matches_Tags`
   - `Search_matches_step_Title_and_returns_parent_article_once` (seed one article with two matching steps; assert the result contains exactly one item for that article)
   - `Search_matches_step_Description`
   - `Search_excludes_Draft_articles_even_on_exact_Title_match`
   - `Search_excludes_soft_deleted_articles`
   - `Search_is_case_insensitive`
   - `Search_with_no_matches_returns_empty_result_with_correct_TotalCount`
2. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/SearchKnowledgeArticlesQueryValidatorTests.cs`** — `Empty_Query_fails`; `Whitespace_Query_fails`; `Valid_Query_passes`.
3. All new tests use `TestApplicationDbContext.Create()` and `StubLocalizationService` exactly as established in prior stories — no new test doubles are needed.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test:** Publish an FAQ article (Stories 21-22) with `title:"How do I reset my password?"`, `tags:"password,reset"`; publish a `Guide` article (Story 23) with a step titled `"Click Forgot Password"`. `GET /api/knowledge-articles/search?query=password` (no auth token) returns both articles. `GET /api/knowledge-articles/search?query=` (empty) returns 400. Create a third, unpublished `Draft` article containing `"password"` in its content and confirm the same search does **not** return it.

## Done Criteria

- [ ] `GET /api/knowledge-articles/search?query={term}` is reachable without an `Authorization` header.
- [ ] A match on any of `Title`, `Content`, `Category`, `Tags`, or any attached step's `Title`/`Description` returns the parent article, exactly once per article.
- [ ] Only `Published` articles are ever returned by this endpoint, regardless of match strength.
- [ ] An empty/whitespace `query` returns a 400 validation failure rather than an unfiltered list.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-6's "Full-text search across all knowledge base content" acceptance criterion and completes all four KAN-6 acceptance criteria across Stories 21-24.
