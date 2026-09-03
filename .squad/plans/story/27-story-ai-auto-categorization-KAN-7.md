# Story 27 — Auto-Categorize Incoming Tickets (Story: KAN-7)

## Prerequisites

- [25-story-ai-ticket-summaries-KAN-7.md](25-story-ai-ticket-summaries-KAN-7.md) completed: requires `IAiClient`, `OpenAiSettings`/`OpenAiClient`, and their DI registration in `src/AzmCrm.Infrastructure/DependencyInjection.cs`.
- Story 05 completed: requires `Ticket`, `TicketCategory`, `CreateTicketCommand`/Handler/Validator, `TicketsController`.
- Story 18 completed (KAN-5, `AssignmentRule`): `CreateTicketCommandHandler`'s auto-assignment block keys on `Category`, so this story's categorization step must run **before** that block so a resolved category still participates in rule matching.

## Story Goal

Let a ticket be created **without** an explicit `Category`, in which case the backend classifies it into one of `TicketCategory`'s six existing values (`General`, `Technical`, `Billing`, `AccountAccess`, `FeatureRequest`, `Other`) using `IAiClient`, satisfying KAN-7's "Auto-categorize incoming tickets" acceptance criterion.

Outcomes:
1. `CreateTicketCommand.Category` (and `CreateTicketRequest.Category`) become `TicketCategory?` (nullable) instead of required. **Every existing caller that already supplies a `Category` is unaffected** — the AI classifier is never invoked when `Category` is provided; this is the story's core backward-compatibility guarantee.
2. When `Category` is omitted (`null`), `CreateTicketCommandHandler` calls a new `IIncomingTicketCategorizer.CategorizeAsync(title, description, ct)` (Application interface, `AiIncomingTicketCategorizer` Infrastructure implementation wrapping `IAiClient`) to classify the ticket from its `Title`/`Description` into one of the six `TicketCategory` values, and persists the resolved category on the `Ticket` exactly as if the caller had supplied it.
3. `IIncomingTicketCategorizer.CategorizeAsync` **never throws** — an AI-provider failure, timeout, or an unparseable/hallucinated response all fall back to `TicketCategory.General` (logged as a warning), so ticket creation is never blocked or failed by AI unavailability. This is the same reliability principle Story 25/26 apply to their own AI calls, but stronger here: those return a `Result` failure on AI trouble; this one must never fail ticket creation, since a category is mandatory on the `Ticket` row regardless.
4. SLA policy stamping and assignment-rule matching (both already in `CreateTicketCommandHandler`, KAN-5 Stories 17-18) use the **resolved** category (explicit or AI-classified) — an AI-classified ticket is assigned/SLA-stamped exactly as if an agent had picked that category manually.

**Not in scope**: re-categorizing an existing ticket after creation (no `POST /api/tickets/{id}/categorize` or similar action — this story only affects ticket creation); a confidence score or "AI-suggested, please confirm" UI flow (the resolved category is written directly, same as a manually-picked one, with no distinct visual/audit marker); batch/background re-categorization of tickets created before this story shipped; categorizing based on anything other than `Title`/`Description` (e.g. attachments, customer history).

## Context — Read These Files First

1. [25-story-ai-ticket-summaries-KAN-7.md](25-story-ai-ticket-summaries-KAN-7.md) — read in full for `IAiClient`'s exact signature and existing DI wiring this story's `AiIncomingTicketCategorizer` depends on.
2. [src/AzmCrm.Domain/Features/Tickets/TicketCategory.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketCategory.cs) (full file, 11 lines) — the exact six enum member names (`General`, `Technical`, `Billing`, `AccountAccess`, `FeatureRequest`, `Other`) the classifier prompt must list verbatim and `Enum.TryParse` against.
3. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs) (full file, 13 lines) — the record whose `Category` parameter changes from `TicketCategory` to `TicketCategory?`.
4. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs) (full file, 71 lines) — the exact current step order: customer-existence check (lines 16-18) → build `Ticket` with `Category`/`Priority` as given (lines 20-27) → SLA policy stamping keyed on `Priority` (lines 29-37) → assignment-rule lookup keyed on `Category`+`Priority` (lines 39-47) → persist + `TicketHistory` rows (lines 49-64) → `SaveChangesAsync` (line 66). This story's category-resolution step is inserted **between** the customer-existence check and the `Ticket` object construction (i.e., resolve the final `category` value first, then use it when building `new Ticket { ... }`), and the assignment-rule `Where` clause (`r.Category == null || r.Category == request.Category`) must be changed to compare against the resolved `category` variable, not `request.Category`, since the latter may now be `null`.
5. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs) (full file, 26 lines) — the `RuleFor(x => x.Category).IsInEnum()...` rule (lines 18-19) that must become conditional (`.When(x => x.Category is not null)`), since an unconditional `IsInEnum()` on a `TicketCategory?` would reject `null` as an "invalid" enum value.
6. [src/AzmCrm.Application/Features/Tickets/DTOs/CreateTicketRequest.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/CreateTicketRequest.cs) (full file, 11 lines) — the request DTO whose `Category` parameter also becomes `TicketCategory?`.
7. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) lines 27-30 (`Create` action) — confirm the `new CreateTicketCommand(request.CustomerId, request.Title, request.Description, request.Category, request.Priority)` call site needs **no code change** (positional construction still compiles once both `Category` properties are `TicketCategory?` in lockstep).
8. [src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs) (full file, 11 lines) and [src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs) (full file, 23 lines) — the exact "thin interface in Application, XML-doc-commented, implementation in Infrastructure" shape `IIncomingTicketCategorizer` follows.
9. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — Story 25 will have already inserted the `OpenAiSettings`/`OpenAiClient`/`IAiClient` block immediately before `return services;`; this story's `services.AddScoped<IIncomingTicketCategorizer, AiIncomingTicketCategorizer>();` line is appended immediately after that block, still before `return services;`.
10. [tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs](../../../tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs) (full file, 192 lines) — **all 6 existing `[Fact]` tests** construct `new CreateTicketCommandHandler(dbContext)` with a single argument; every one of them must be updated to pass a second argument once the handler's constructor gains `IIncomingTicketCategorizer categorizer` — every existing test already supplies an explicit non-null `Category` in its `CreateTicketCommand`, so the categorizer is never actually invoked in any of them and the stub's configured return value is irrelevant to their existing assertions.
11. [tests/AzmCrm.Application.Tests/TestDoubles/StubEmailSender.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubEmailSender.cs) (full file, 18 lines) — the exact test-double shape (`List<TCallArgs> Calls`, configurable return value) `StubIncomingTicketCategorizer` follows.

## Implementation tasks

### 1 — Categorizer abstraction

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IIncomingTicketCategorizer.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Classifies a new ticket's title/description into one of the existing <see cref="TicketCategory"/>
/// values when no category was supplied at creation time. Implementations must never throw — an
/// unavailable AI provider or an unparseable response must fall back to
/// <see cref="TicketCategory.General"/> rather than blocking ticket creation.
/// </summary>
public interface IIncomingTicketCategorizer
{
    Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Infrastructure/AiFeatures/AiIncomingTicketCategorizer.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.AiFeatures;

internal sealed class AiIncomingTicketCategorizer(IAiClient aiClient, ILogger<AiIncomingTicketCategorizer> logger)
    : IIncomingTicketCategorizer
{
    private static readonly string CategoryNames = string.Join(", ", Enum.GetNames<TicketCategory>());

    public async Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default)
    {
        var systemPrompt =
            $"You classify support tickets into exactly one of these categories: {CategoryNames}. " +
            "Respond with only the category name, exactly as written above, with no punctuation or explanation.";

        var userPrompt = $"Title: {title}\nDescription: {description ?? "(none)"}";

        try
        {
            var response = await aiClient.GetCompletionAsync(systemPrompt, userPrompt, ct);

            if (Enum.TryParse<TicketCategory>(response.Trim(), ignoreCase: true, out var category))
                return category;

            logger.LogWarning("AI categorizer returned an unparseable category '{Response}'; falling back to General.", response);
            return TicketCategory.General;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI categorization failed; falling back to General.");
            return TicketCategory.General;
        }
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — append immediately after Story 25's `IAiClient` registration block, still before `return services;`:

```csharp
services.AddScoped<IIncomingTicketCategorizer, AiIncomingTicketCategorizer>();
```

### 2 — Make `Category` optional on ticket creation

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`**:

```csharp
public sealed record CreateTicketCommand(
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory? Category,
    TicketPriority Priority
) : IRequest<Result<Guid>>;
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/CreateTicketRequest.cs`**:

```csharp
public sealed record CreateTicketRequest(
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory? Category,
    TicketPriority Priority
);
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs`** — change the `Category` rule (lines 18-19) to:

```csharp
RuleFor(x => x.Category)
    .IsInEnum().When(x => x.Category is not null)
    .WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Category"]);
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`** — inject `IIncomingTicketCategorizer categorizer` as a second constructor parameter, resolve the category before building the `Ticket`, and use the resolved value in both the entity initializer and the assignment-rule lookup:

```csharp
internal sealed class CreateTicketCommandHandler(IApplicationDbContext dbContext, IIncomingTicketCategorizer categorizer)
    : IRequestHandler<CreateTicketCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var category = request.Category
            ?? await categorizer.CategorizeAsync(request.Title, request.Description, ct);

        var ticket = new Ticket
        {
            CustomerId = request.CustomerId,
            Title = request.Title,
            Description = request.Description,
            Category = category,
            Priority = request.Priority
        };

        var slaPolicy = await dbContext.SlaPolicies
            .FirstOrDefaultAsync(p => p.Priority == request.Priority && p.IsActive, ct);

        if (slaPolicy is not null)
        {
            ticket.SlaPolicyId = slaPolicy.Id;
            ticket.ResponseDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResponseTimeMinutes);
            ticket.ResolutionDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResolutionTimeMinutes);
        }

        var assignmentRule = await dbContext.AssignmentRules
            .Where(r => r.IsActive)
            .Where(r => r.Category == null || r.Category == category)
            .Where(r => r.Priority == null || r.Priority == request.Priority)
            .OrderBy(r => r.EvaluationOrder)
            .FirstOrDefaultAsync(ct);

        // ... unchanged from here (assignment, TicketHistories.Add(Created), optional Assigned, SaveChangesAsync)
    }
}
```

Keep every other line in the handler (assignment-rule application, `TicketHistories.Add` calls, `SaveChangesAsync`, return) exactly as it exists today — only the two `request.Category` references shown above change to `category`, and the constructor gains the `categorizer` parameter.

### 3 — No API-layer code change needed

`TicketsController.Create` (lines 27-34) requires **no edits** — it already passes `request.Category` positionally into `CreateTicketCommand`; once both are `TicketCategory?`, the existing call site still compiles unchanged.

## Edge Cases & Failure Modes

- **`Category` omitted and AI provider unreachable/misconfigured** — `AiIncomingTicketCategorizer` catches internally and returns `TicketCategory.General`; ticket creation succeeds exactly as if `General` had been explicitly requested — enforced in `AiIncomingTicketCategorizer.CategorizeAsync`'s `catch` block, never surfaced to `CreateTicketCommandHandler`.
- **`Category` omitted and the AI response is not one of the six enum names** (hallucination, extra punctuation, wrong case) — `Enum.TryParse<TicketCategory>(..., ignoreCase: true, ...)` fails, falls back to `General`, logged as a warning — enforced in the same method.
- **`Category` explicitly provided** — `categorizer.CategorizeAsync` is never called at all (short-circuited by the `??` operator in the handler); zero behavior change from pre-Story-27 behavior for any existing caller, including every other KAN-5 story's tests that construct `CreateTicketCommand` with an explicit category.
- **`Category` omitted and `Title`/`Description` are both minimal or generic** (e.g. `Title: "Help"`, no description) — the classifier still returns some value (worst case `General`, a valid existing category); this is an accepted quality limitation of prompt-based classification, not a defect.
- **Assignment rules keyed on `Category`** (KAN-5 Story 18) — correctly match against the *resolved* category (AI-classified or explicit), never against a `null` `request.Category`, because the `Where` clause was changed to compare against the local `category` variable.

## Test Plan

1. **Edit file: `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs`** — update all 6 existing `new CreateTicketCommandHandler(dbContext)` call sites to `new CreateTicketCommandHandler(dbContext, new StubIncomingTicketCategorizer())` (a default `StubIncomingTicketCategorizer` returning `TicketCategory.General` is fine, since none of these tests omit `Category`).
2. **Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubIncomingTicketCategorizer.cs`**:
   ```csharp
   public sealed class StubIncomingTicketCategorizer : IIncomingTicketCategorizer
   {
       public List<(string Title, string? Description)> Calls { get; } = [];
       public TicketCategory CategoryToReturn { get; set; } = TicketCategory.General;

       public Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default)
       {
           Calls.Add((title, description));
           return Task.FromResult(CategoryToReturn);
       }
   }
   ```
3. **Add to `CreateTicketCommandHandlerTests.cs`** (new `[Fact]`s):
   - `Create_with_null_Category_calls_categorizer_and_persists_resolved_category` — construct the command with `Category: null`, configure `StubIncomingTicketCategorizer.CategoryToReturn = TicketCategory.Billing`, assert `ticket.Category == TicketCategory.Billing` and `Assert.Single(stub.Calls)`.
   - `Create_with_explicit_Category_never_calls_categorizer` — assert `Assert.Empty(stub.Calls)` after a normal create with an explicit `Category`.
   - `Create_with_null_Category_uses_resolved_category_for_assignment_rule_matching` — seed an `AssignmentRule` for `TicketCategory.Billing`, create with `Category: null` and a stub that resolves to `Billing`, assert the ticket is auto-assigned.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/AiFeatures/AiIncomingTicketCategorizerTests.cs`** (uses `StubAiClient` from Story 25):
   - `Categorize_parses_valid_enum_name_case_insensitively`
   - `Categorize_falls_back_to_General_on_unparseable_response`
   - `Categorize_falls_back_to_General_when_AiClient_throws`
5. **Edit file: `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandValidatorTests.cs`** — locate this file first (grep to confirm its exact existing test names before editing) and add a case asserting a `null` `Category` passes validation.

## Edge Cases note on file layout

`tests/AzmCrm.Application.Tests/Features/AiFeatures/` is a new test folder (mirrors the new `src/AzmCrm.Infrastructure/AiFeatures/` source folder introduced by Story 25) — create it if it does not already exist.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test** (requires a reachable PostgreSQL and a valid `OpenAi:ApiKey`/mock endpoint): `POST /api/tickets` with `"category": null` (or the field omitted) and a clearly technical `title`/`description`; confirm the created ticket's `GET /api/tickets/{id}` response shows `"category": "Technical"` (or another plausible category) rather than a validation error. Repeat with an explicit `"category": "Billing"` and confirm it is used unchanged.

## Done Criteria

- [ ] `POST /api/tickets` accepts a request with `category` omitted or `null`.
- [ ] When `category` is omitted, the created ticket's category is resolved via `IIncomingTicketCategorizer`, never left `null` (the `Ticket.Category` column remains non-nullable).
- [ ] When `category` is explicitly provided, behavior is byte-for-byte identical to before this story (the categorizer is never invoked).
- [ ] SLA stamping and assignment-rule matching use the resolved category in both the explicit and AI-classified cases.
- [ ] An AI-provider failure during categorization never fails or blocks ticket creation — it silently falls back to `TicketCategory.General`.
- [ ] All existing and new handler/validator/categorizer unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.
