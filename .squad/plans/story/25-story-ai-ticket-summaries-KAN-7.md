# Story 25 — AI-Generated Ticket Summaries (Story: KAN-7)

## Prerequisites

- Story 05 completed: requires `Ticket`, `IApplicationDbContext.Tickets`, `TicketsController`.
- None of KAN-3/KAN-4/KAN-5/KAN-6 are required by this story specifically, but it is the **foundational** story for the KAN-7 "AI Features" epic — [26-story-ai-suggested-replies-KAN-7.md](26-story-ai-suggested-replies-KAN-7.md), [27-story-ai-auto-categorization-KAN-7.md](27-story-ai-auto-categorization-KAN-7.md), and [29-story-ai-chatbot-KAN-7.md](29-story-ai-chatbot-KAN-7.md) all depend on the `IAiClient` abstraction, `OpenAiSettings`, and DI wiring this story introduces. [28-story-ai-knowledge-base-suggestions-KAN-7.md](28-story-ai-knowledge-base-suggestions-KAN-7.md) does **not** depend on this story (see its own Prerequisites).

## Story Goal

Let an agent generate a short AI-written summary of a ticket — its title, description, and internal comment thread — on demand, satisfying KAN-7's "Generate AI ticket summaries" acceptance criterion. This story also establishes the reusable AI-provider abstraction every other KAN-7 story builds on.

Outcomes:
1. A new provider-agnostic `IAiClient` abstraction (Application layer) wraps a single "get a text completion for a system+user prompt pair" operation, with an `OpenAiClient` (Infrastructure layer) implementation calling an OpenAI-compatible Chat Completions HTTP API, configured via a new `OpenAi` appsettings section — this is the exact same "interface in Application, `HttpClient`-based typed-client implementation in Infrastructure, bound `IOptions<TSettings>` from a new appsettings section" shape KAN-3 Story 10 used for `IWhatsAppProvider`/`WhatsAppCloudApiProvider`/`WhatsAppSettings`.
2. `POST /api/tickets/{id}/ai-summary` is a new, `[Authorize]`-protected (default, no override) action that generates a summary via `IAiClient`, persists it onto the `Ticket` row (`AiSummary`/`AiSummaryGeneratedOn`, both new nullable columns), and returns it. Calling it again regenerates and overwrites the previous summary — no summary history/versioning is kept.
3. The persisted summary is also surfaced through the existing `GET /api/tickets/{id}` endpoint by appending `AiSummary`/`AiSummaryGeneratedOn` to `TicketDto`, so an agent viewing a ticket sees its last-generated summary without a second call.
4. A failure to reach the AI provider (misconfigured key, network error, non-2xx response) never throws an unhandled exception — the endpoint returns a `Result` failure (HTTP 400) with a generic message, and the ticket's existing `AiSummary`/`AiSummaryGeneratedOn` (if any) are left untouched.

**Not in scope**: summary history/versioning (each call overwrites the prior summary); summarizing a ticket's associated `Conversation`/`Message` thread (no FK exists between `Ticket` and `Conversation` in this codebase — KAN-3's own stories left that link out of scope — so this story's context is `Ticket.Title`/`Description` plus its `TicketComment` thread, KAN-4 Story 16's internal collaboration comments, only); automatic/scheduled summarization (this is an on-demand, agent-triggered action only); streaming responses; any UI for editing a generated summary before it's saved.

## Context — Read These Files First

1. [src/AzmCrm.Domain/Common/Result.cs](../../../src/AzmCrm.Domain/Common/Result.cs) lines 1-38 — `Result`/`Result<T>` live in `AzmCrm.Domain.Common` (not `Application/Shared/Models`), sealed records with private constructors and static `Success`/`Failure` factories returning `IReadOnlyList<string>` errors. Every new handler in this story returns `Result<T>` built this way.
2. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) (full file, 23 lines) — the entity this story adds two nullable properties to (`AiSummary`, `AiSummaryGeneratedOn`), right after the existing `RespondedOn` property.
3. [src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs) (full file, 69 lines) — note the `builder.Property(t => t.Description).HasMaxLength(4000);` shape at lines 24-25; this story adds an equivalent `builder.Property(t => t.AiSummary).HasMaxLength(2000);` line. No index or FK needed for the new columns.
4. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs) (full file, 23 lines) — positional record; this story appends `string? AiSummary, DateTime? AiSummaryGeneratedOn` as the two new trailing parameters (after `RespondedOn`) to avoid breaking the existing 17-parameter positional-constructor call site.
5. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs) lines 27-32 — the `new TicketDto(...)` call site to extend with `ticket.AiSummary, ticket.AiSummaryGeneratedOn` at the end.
6. [src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs) (full file, 11 lines) — the exact minimal-interface shape (`Task<TResult> DoAsync(..., CancellationToken ct = default)`) `IAiClient` follows.
7. [src/AzmCrm.Infrastructure/Communications/WhatsAppCloudApiProvider.cs](../../../src/AzmCrm.Infrastructure/Communications/WhatsAppCloudApiProvider.cs) (full file, 31 lines) and [src/AzmCrm.Infrastructure/Communications/WhatsAppSettings.cs](../../../src/AzmCrm.Infrastructure/Communications/WhatsAppSettings.cs) (full file, 11 lines) — the exact template `OpenAiClient`/`OpenAiSettings` follow: primary-constructor `HttpClient`/`IOptions<TSettings>` injection, `Bearer` auth header set per call, `PostAsJsonAsync` + response parsing, `public const string SectionName = "..."` + `init`-only properties with `"CHANGE_ME_..."` placeholder secrets.
8. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) lines 116-131 — the exact three-line-block DI pattern per external integration (`Configure<TSettings>` → `AddHttpClient<TProvider>()` → `AddScoped<IProvider>(provider => provider.GetRequiredService<TProvider>())`), and the `return services;` line (131) this story's new block is inserted immediately before.
9. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) lines 57-77 — exact 2-space-indented JSON section style (`Smtp`, `WhatsApp`, `Sms`) this story's new `OpenAi` section (inserted after the `Sms` block, before `SlaMonitoring`) matches.
10. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) (full file, 136 lines) — note the `mediator.Send(...)` → `ToResult(result)` shape used by every action (e.g. lines 37-44); this story's new action is appended after line 135 (`GetComments`), before the closing brace at 136.
11. [src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs](../../../src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs) (full file, 29 lines) — `ToResult<T>(Result<T>)` helper (lines 14-15) used by the new action; class-level `[Authorize]` (line 8) applies by default, no override needed.
12. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) and [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs) lines 1-26 — the `localization[LocalizationKeys.Validation.Required, "Field"]` indexer pattern this story's new validator reuses for its single `TicketId` rule.
13. [tests/AzmCrm.Application.Tests/TestDoubles/StubEmailSender.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubEmailSender.cs) (full file, 18 lines) — the exact `List<TCallArgs> Calls` + `bool ThrowOnX` test-double shape this story's `StubAiClient` follows.
14. [tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs](../../../tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs) lines 12-41 — representative handler-test shape (`TestApplicationDbContext.Create()`, direct `new HandlerClass(...)` construction, `xUnit` `[Fact]`, no mocking framework) this story's new tests follow.
15. Grep for `TicketComment` under `src/AzmCrm.Domain/Features/Tickets/` and `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketComments/` to confirm the exact `TicketComment` entity shape (`TicketId`, `Content`, inherited `CreatedBy`/`CreatedOn`) and ordering (`GetTicketCommentsQueryHandler`'s `OrderBy`) before writing the prompt-context-building code in the handler below — read whichever handler file that query resolves to in full.

## Implementation tasks

### 1 — Domain & persistence

**File: `src/AzmCrm.Domain/Features/Tickets/Ticket.cs`** — add two nullable properties after `RespondedOn`:

```csharp
public DateTime? RespondedOn { get; set; }
public string? AiSummary { get; set; }
public DateTime? AiSummaryGeneratedOn { get; set; }
```

**File: `src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs`** — add after the `Description` property config (after line 25):

```csharp
builder.Property(t => t.AiSummary)
    .HasMaxLength(2000);
```

**Migration**: run from the repository root:

```bash
dotnet ef migrations add AddTicketAiSummary -p src/AzmCrm.Infrastructure -s src/AzmCrm.API
```

This adds two nullable columns (`AiSummary` varchar(2000), `AiSummaryGeneratedOn` timestamp) to the `Tickets` table — additive, no data migration, no index. The API applies it automatically on next startup (`AzmCrm.Infrastructure/Data/DatabaseInitializer.cs`, per `README.md`'s "Migrations" section) — do not run `dotnet ef database update` manually unless verifying locally against a reachable PostgreSQL instance.

### 2 — AI client abstraction

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IAiClient.cs`**

```csharp
namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Provider-agnostic abstraction for getting a single text completion from an LLM, given a
/// system prompt (instructions/context) and a user prompt (the actual request). The Application
/// layer never touches a specific AI provider's HTTP API directly — swap the Infrastructure-layer
/// implementation without changing any handler that depends on this interface.
/// </summary>
public interface IAiClient
{
    Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Infrastructure/AiFeatures/OpenAiSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.AiFeatures;

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAi";

    public string ApiBaseUrl { get; init; } = "https://api.openai.com/v1";
    public string ApiKey { get; init; } = "CHANGE_ME_OpenAiApiKey";
    public string Model { get; init; } = "gpt-4o-mini";
}
```

**Create file: `src/AzmCrm.Infrastructure/AiFeatures/OpenAiClient.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AzmCrm.Infrastructure.AiFeatures;

internal sealed class OpenAiClient(HttpClient httpClient, IOptions<OpenAiSettings> settings) : IAiClient
{
    private readonly OpenAiSettings _settings = settings.Value;

    public async Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiBaseUrl}/chat/completions";

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var payload = new
        {
            model = _settings.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3
        };

        var response = await httpClient.PostAsJsonAsync(url, payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(cancellationToken: ct);

        return body?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("OpenAI response contained no completion choices.");
    }

    private sealed class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; init; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; init; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
```

**Edit file: `src/AzmCrm.API/appsettings.json`** — insert after the `Sms` section (after line 76's closing `},`), before `SlaMonitoring` (line 78):

```json
  "OpenAi": {
    "ApiBaseUrl": "https://api.openai.com/v1",
    "ApiKey": "CHANGE_ME_OpenAiApiKey",
    "Model": "gpt-4o-mini"
  },
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — add `using AzmCrm.Infrastructure.AiFeatures;` to the using block (alongside the existing `using AzmCrm.Infrastructure.Communications;`-style usings at the top), then insert immediately before `return services;` (line 131):

```csharp
services.Configure<OpenAiSettings>(configuration.GetSection(OpenAiSettings.SectionName));
services.AddHttpClient<OpenAiClient>();
services.AddScoped<IAiClient>(provider => provider.GetRequiredService<OpenAiClient>());
```

### 3 — Application layer: generate-summary command

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketAiSummaryDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketAiSummaryDto(Guid TicketId, string Summary, DateTime GeneratedOn);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/GenerateTicketSummary/GenerateTicketSummaryCommand.cs`**

```csharp
using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

public sealed record GenerateTicketSummaryCommand(Guid TicketId) : IRequest<Result<TicketAiSummaryDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/GenerateTicketSummary/GenerateTicketSummaryCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

public sealed class GenerateTicketSummaryCommandValidator : AbstractValidator<GenerateTicketSummaryCommand>
{
    public GenerateTicketSummaryCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/GenerateTicketSummary/GenerateTicketSummaryCommandHandler.cs`**

```csharp
using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

internal sealed class GenerateTicketSummaryCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<GenerateTicketSummaryCommand, Result<TicketAiSummaryDto>>
{
    private const int MaxCommentsInContext = 20;

    public async Task<Result<TicketAiSummaryDto>> Handle(GenerateTicketSummaryCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        // Bounded to the most recent MaxCommentsInContext comments so the prompt does not grow
        // unbounded on a long-running ticket — an explicit scope choice, not an oversight.
        var comments = await dbContext.TicketComments
            .Where(c => c.TicketId == ticket.Id)
            .OrderByDescending(c => c.CreatedOn)
            .Take(MaxCommentsInContext)
            .OrderBy(c => c.CreatedOn)
            .Select(c => c.Content)
            .ToListAsync(ct);

        var userPrompt =
            $"Title: {ticket.Title}\n" +
            $"Category: {ticket.Category}\n" +
            $"Priority: {ticket.Priority}\n" +
            $"Status: {ticket.Status}\n" +
            $"Description: {ticket.Description ?? "(none)"}\n\n" +
            (comments.Count > 0
                ? "Internal comment thread (oldest first):\n" + string.Join("\n---\n", comments)
                : "No internal comments yet.");

        const string systemPrompt =
            "You are an assistant that writes concise internal summaries of customer support tickets " +
            "for a support agent. Summarize the ticket in 2-3 sentences: the customer's issue, its " +
            "current state, and any progress so far. Do not invent facts not present in the ticket.";

        string summary;
        try
        {
            summary = await aiClient.GetCompletionAsync(systemPrompt, userPrompt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TicketAiSummaryDto>.Failure("AI summary generation is currently unavailable. Please try again later.");
        }

        ticket.AiSummary = summary;
        ticket.AiSummaryGeneratedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result<TicketAiSummaryDto>.Success(
            new TicketAiSummaryDto(ticket.Id, ticket.AiSummary, ticket.AiSummaryGeneratedOn.Value));
    }
}
```

Adjust the `TicketComments` query above to match whatever `Content`-equivalent property name and ordering convention the real `TicketComment` entity/`GetTicketCommentsQueryHandler` use (Context item 15) if they differ from the assumed `TicketId`/`Content`/`CreatedOn` shape.

### 4 — API layer

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`** — extend the `new TicketDto(...)` call (lines 27-32) with two trailing arguments:

```csharp
var dto = new TicketDto(
    ticket.Id, ticket.CustomerId, ticket.Title, ticket.Description, ticket.Category,
    ticket.Priority, ticket.Status, ticket.CreatedOn, ticket.UpdatedOn,
    ticket.AssignedToUserId, assignedToUserName,
    ticket.IsEscalated, ticket.EscalatedOn,
    ticket.SlaPolicyId, ticket.ResponseDueOn, ticket.ResolutionDueOn, ticket.RespondedOn,
    ticket.AiSummary, ticket.AiSummaryGeneratedOn);
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs`** — append the two new trailing parameters after `RespondedOn`:

```csharp
public sealed record TicketDto(
    Guid Id,
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    DateTime? UpdatedOn,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    bool IsEscalated,
    DateTime? EscalatedOn,
    Guid? SlaPolicyId,
    DateTime? ResponseDueOn,
    DateTime? ResolutionDueOn,
    DateTime? RespondedOn,
    string? AiSummary,
    DateTime? AiSummaryGeneratedOn
);
```

**Edit file: `src/AzmCrm.API/Controllers/TicketsController.cs`** — add `using AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;`, then append a new action after `GetComments` (after line 135, before the closing brace at 136):

```csharp
[HttpPost("{id:guid}/ai-summary")]
[ProducesResponseType(typeof(Result<TicketAiSummaryDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GenerateAiSummary(Guid id, CancellationToken ct)
{
    var result = await mediator.Send(new GenerateTicketSummaryCommand(id), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **Ticket id does not exist** — `GenerateTicketSummaryCommandHandler` throws `NotFoundException` before any AI call, returning 404 (matches every other `Get*ById`/mutating-by-id handler in this codebase, e.g. `CreateTicketCommandHandler`'s customer-existence check).
- **AI provider unreachable, misconfigured `ApiKey`, or non-2xx response** — caught by the handler's `try/catch` around `aiClient.GetCompletionAsync`, returns a `Result` failure (400) with a generic message; `ticket.AiSummary`/`AiSummaryGeneratedOn` are left at their prior value (or both remain `null` if never generated before) — no partial/corrupt state is ever persisted, since the write only happens after a successful AI call.
- **Ticket with no `Description` and no `TicketComment` rows** — still summarizable from `Title`/`Category`/`Priority`/`Status` alone; the prompt explicitly states `"No internal comments yet."` rather than leaving that section blank.
- **Ticket with a very long comment thread** — bounded to the most recent `MaxCommentsInContext` (20) comments, re-sorted oldest-first for prompt readability, to keep the prompt payload bounded — an explicit, documented scope decision (see Story Goal), not a bug.
- **Calling the endpoint twice in a row** — the second call fully overwrites `AiSummary`/`AiSummaryGeneratedOn`; no history of prior summaries is kept (see Story Goal, "Not in scope").
- **Follow-up flagged, not implemented**: summarizing a ticket's linked customer conversation thread once (if ever) a `Ticket`↔`Conversation` link is added to this codebase; automatic summarization on ticket status change or SLA breach.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubAiClient.cs`**:
   ```csharp
   public sealed class StubAiClient : IAiClient
   {
       public List<(string SystemPrompt, string UserPrompt)> Calls { get; } = [];
       public string Response { get; set; } = "Stub AI summary.";
       public bool ThrowOnCall { get; set; }

       public Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
       {
           if (ThrowOnCall)
               throw new InvalidOperationException("Simulated AI provider failure.");
           Calls.Add((systemPrompt, userPrompt));
           return Task.FromResult(Response);
       }
   }
   ```
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/GenerateTicketSummaryCommandHandlerTests.cs`**:
   - `Generate_persists_summary_and_stamps_GeneratedOn`
   - `Generate_for_missing_ticket_throws_NotFoundException`
   - `Generate_when_AiClient_throws_returns_Failure_and_leaves_ticket_AiSummary_null`
   - `Generate_includes_recent_TicketComments_in_prompt` (assert on `StubAiClient.Calls` that the comment content appears in the captured `UserPrompt`)
   - `Generate_overwrites_previous_summary_on_second_call`
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/GenerateTicketSummaryCommandValidatorTests.cs`** — `Empty_TicketId_fails`; `Valid_TicketId_passes`. Uses `StubLocalizationService` per existing convention.
4. All new tests use `TestApplicationDbContext.Create()` — no changes to `TestApplicationDbContext.cs`/`IApplicationDbContext.cs`/`ApplicationDbContext.cs` are needed for this story (no new `DbSet`, only new properties on the existing `Ticket` entity, which the in-memory provider maps automatically).

## Migration / Rollback

- Forward: `dotnet ef migrations add AddTicketAiSummary -p src/AzmCrm.Infrastructure -s src/AzmCrm.API`, applied automatically on next API startup.
- Rollback: `dotnet ef database update <PreviousMigrationName> -p src/AzmCrm.Infrastructure -s src/AzmCrm.API` (drops the two new nullable columns). Since both columns are nullable with no default and no other code path writes to them outside this story's handler, a half-applied state (migration created but not yet run) is harmless — the API's automatic-migration-on-startup step (README.md) applies it before any request can reach the new handler.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test** (requires a reachable PostgreSQL and a valid `OpenAi:ApiKey`, or a local OpenAI-compatible mock server pointed to by `OpenAi:ApiBaseUrl`): create a ticket, add one or two `POST /api/tickets/{id}/comments`, call `POST /api/tickets/{id}/ai-summary`, confirm a 200 with a non-empty `Summary`, then `GET /api/tickets/{id}` and confirm `AiSummary`/`AiSummaryGeneratedOn` are populated with the same values.

## Done Criteria

- [ ] `POST /api/tickets/{id}/ai-summary` generates a summary from the ticket's title, description, and comment thread, and persists it onto the ticket.
- [ ] `GET /api/tickets/{id}` returns the persisted `AiSummary`/`AiSummaryGeneratedOn` via `TicketDto`.
- [ ] An AI-provider failure returns a 400 `Result` failure rather than a 500 or unhandled exception, and never corrupts the ticket's existing summary state.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.
