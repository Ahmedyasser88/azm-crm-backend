# Story 12 — Live Chat Channel: Real-Time Conversation (Story: KAN-3)

## Prerequisites

- [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) completed: requires the `Conversation`/`Message` entities, `IApplicationDbContext.Conversations`/`Messages`, `ConversationsController`, `SendMessageCommand`, and `GetConversationMessagesQuery`.
- Independent of [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md), [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md), and [11-story-sms-channel-KAN-3.md](11-story-sms-channel-KAN-3.md) — see Story 09's Prerequisites for why these four channel stories can be implemented and merged in any order. This one does **not** implement `IChannelMessageSender` at all — live chat delivery is push-based (SignalR), not a request/response send, so it is structurally different from Stories 09-11.

## Story Goal

Satisfy KAN-3's "Provide live chat functionality" acceptance criterion. Both an anonymous customer (using a support widget) and an authenticated agent can exchange messages on a `LiveChat`-channel `Conversation` in real time, without polling, using a SignalR hub. A conversation's own `Guid` id acts as the widget's lightweight access credential — anyone who knows a specific conversation's id can join its real-time group, the same way a shareable link works; there is no separate customer account/login system anywhere in this codebase to build a stronger credential on top of (see Edge Cases for the security implications of this choice).

Outcomes:
1. `POST /api/conversations/live-chat/start` is a public, unauthenticated endpoint a chat widget calls to begin a session: it resolves the visitor to an existing `Customer` by email or creates one (same pattern as Story 08's web-form submission), creates a new `LiveChat`-channel `Conversation`, and returns its id — the widget then uses that id as its SignalR group key for the rest of the session.
2. `ChatHub` (`/hubs/chat`) lets any connected client (anonymous customer widget or authenticated agent) call `JoinConversation(conversationId)` to start receiving that conversation's messages in real time, and `SendMessage(conversationId, body)` to post one — the hub infers `Inbound` (customer) vs. `Outbound` (agent) from whether the caller is authenticated, and broadcasts every new message to everyone currently joined to that conversation's group.
3. `GET /api/conversations/{id}/messages` (already built in Story 08) still works for a client that wants to load message history before/without connecting to the hub — e.g. an agent's ticket-list UI showing a conversation transcript.

**Not in scope**: a customer identity/session system stronger than "knows the conversation id" (see Story Goal above), typing indicators/read receipts/presence, closing a live chat conversation from the widget side, scaling the hub across multiple API instances (SignalR's default in-memory backplane only broadcasts within a single process — a horizontally-scaled deployment would need a Redis or Azure SignalR backplane, not configured here since this codebase has no existing multi-instance deployment concern to size this against), and reconnection/message-replay-on-reconnect logic beyond what `GET /api/conversations/{id}/messages` already provides for a client to catch up on demand.

## Context — Read These Files First

1. [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) — read in full. `StartLiveChatCommandHandler` (Task 2) reuses `SubmitWebFormCommandHandler`'s exact customer-resolution logic; `ChatHub.SendMessage` (Task 4) calls the existing `SendMessageCommand` for the agent path.
2. [src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommand.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommand.cs) and [SendMessageCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandHandler.cs) — created by Story 08, read both in full. `ChatHub.SendMessage`'s authenticated path (Task 4) sends this exact command via `IMediator` — no changes to this command/handler.
3. [src/AzmCrm.Infrastructure/Identity/CurrentUserService.cs](../../../src/AzmCrm.Infrastructure/Identity/CurrentUserService.cs) — read in full (61 lines). Its constructor captures `httpContextAccessor.HttpContext?.User` **at construction time** (lines 16-19), which happens once per DI scope. `ApplicationDbContext.SaveChangesAsync` (`src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`, lines 41-61) reads `_currentUserService.UserId` to stamp `CreatedBy` on every new row — including a `Message` created from inside `ChatHub.SendMessage`. **This has never been exercised against a SignalR hub invocation in this codebase** (every existing usage is a classic per-HTTP-request MVC controller action). ASP.NET Core's `IHttpContextAccessor` is documented to resolve correctly inside Hub methods when `services.AddHttpContextAccessor()` is registered (already true here — `src/AzmCrm.Infrastructure/DependencyInjection.cs` line 91) — but confirm this explicitly during Verification Steps (Task on this below) rather than assuming it, since a wrong resolution would silently stamp every hub-authored `Message.CreatedBy` as `Guid.Empty` instead of the connected agent's real id.
4. [src/AzmCrm.Application/Features/Communications/Queries/GetConversationById/GetConversationByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Queries/GetConversationById/GetConversationByIdQueryHandler.cs) (Story 08) — `ChatHub.JoinConversation` (Task 4) sends this exact query via `IMediator` to validate the conversation exists before adding the caller to its group.
5. [src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs) (Story 08) — `StartLiveChatCommandHandler` (Task 2) is a near-identical copy with `CommunicationChannel.LiveChat` substituted for `CommunicationChannel.WebForm`.
6. [src/AzmCrm.API/Program.cs](../../../src/AzmCrm.API/Program.cs) — read in full (91 lines). Line 64, `app.MapControllers();`, is where `app.MapHub<ChatHub>("/hubs/chat");` (Task 4) is added, immediately after. Lines 61-62 (`app.UseAuthentication(); app.UseAuthorization();`) already run before that point, which is required for the hub to see `Context.User` for an authenticated agent connection at all.
7. [src/AzmCrm.API/Extensions/ApplicationExtensions.cs](../../../src/AzmCrm.API/Extensions/ApplicationExtensions.cs) — read in full (44 lines). `AddApplicationServices` (lines 10-28) is where `services.AddSignalR();` (Task 3) is added — SignalR ships inside the `Microsoft.AspNetCore.App` shared framework, already referenced via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in `src/AzmCrm.API/AzmCrm.API.csproj`, so no new `PackageReference` is needed.
8. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) lines 53-75 (the JWT bearer authentication setup). SignalR's standard way to authenticate a hub connection over WebSockets is to pass the JWT as an `access_token` query-string parameter (browsers can't set `Authorization` headers on a WebSocket upgrade); Task 3 adds a small `JwtBearerEvents.OnMessageReceived` hook to this existing configuration so a connection to `/hubs/chat?access_token=<jwt>` authenticates the same way a normal `Authorization: Bearer` header would for a REST call — **without this hook, an agent's browser can still connect anonymously but `Context.User` will never be authenticated, breaking the `IsAuthenticated` check in Task 4**.
9. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) (Story 08) — this story edits this file to add one new `[AllowAnonymous]` action (`live-chat/start`), following the exact shape of the existing `SubmitWebForm` action.
10. [src/AzmCrm.API/Extensions/RateLimitingExtensions.cs](../../../src/AzmCrm.API/Extensions/RateLimitingExtensions.cs) — the `"fixed"` policy `live-chat/start` reuses, same as every other anonymous endpoint added by this KAN-3 slice.

## Implementation tasks

### 1 — Domain layer

No domain changes required — `CommunicationChannel.LiveChat` already exists from Story 08.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/StartLiveChatRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record StartLiveChatRequest(string Name, string Email, string Body);
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

public sealed record StartLiveChatCommand(string Name, string Email, string Body) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

internal sealed class StartLiveChatCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<StartLiveChatCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartLiveChatCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail, ct);

        if (customer is null)
        {
            customer = new Customer { FullName = request.Name, Email = request.Email };
            dbContext.Customers.Add(customer);
        }

        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.LiveChat
        };
        dbContext.Conversations.Add(conversation);

        dbContext.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

public sealed class StartLiveChatCommandValidator : AbstractValidator<StartLiveChatCommand>
{
    public StartLiveChatCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

No new command is needed for a customer's *subsequent* messages in the same session — `ChatHub.SendMessage` (Task 4) handles those directly via a small inline insert, described there, rather than a new MediatR command, since the hub needs the freshly created `Message` for the broadcast payload regardless and the logic is only a few lines.

### 3 — API layer: SignalR wiring

**Edit file: `src/AzmCrm.API/Extensions/ApplicationExtensions.cs`** — in `AddApplicationServices`, after `services.AddControllers()...`:

```csharp
services.AddSignalR();
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — inside the existing `.AddJwtBearer(options => { ... })` call (after the `options.TokenValidationParameters = ...` assignment), add:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            context.Token = accessToken;

        return Task.CompletedTask;
    }
};
```

This is ASP.NET Core's standard documented pattern for authenticating a SignalR connection with a JWT passed as a query-string parameter (browsers cannot attach an `Authorization` header to the WebSocket handshake request) — restricted to the `/hubs` path prefix specifically so it never weakens how a normal REST `Authorization` header is validated for every other endpoint. Requires `using Microsoft.AspNetCore.Authentication.JwtBearer;` at the top of the file (already present, since `JwtBearerDefaults.AuthenticationScheme` is already used a few lines above).

### 4 — API layer: the hub and controller action

**Create file: `src/AzmCrm.API/Hubs/ChatHub.cs`**

```csharp
using AzmCrm.Application.Features.Communications.Commands.SendMessage;
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Features.Communications.Queries.GetConversationById;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace AzmCrm.API.Hubs;

/// <summary>
/// No <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/> at the class level —
/// both an anonymous customer widget and an authenticated agent connect to this same hub. A
/// conversation's own Guid id is the group key and the widget's de facto access credential (see
/// Story Goal); <see cref="SendMessage"/> infers message direction from whether the caller is
/// authenticated, not from a parameter the caller could lie about.
/// </summary>
public sealed class ChatHub(IMediator mediator, IApplicationDbContext dbContext) : Hub
{
    public async Task JoinConversation(Guid conversationId)
    {
        var result = await mediator.Send(new GetConversationByIdQuery(conversationId));
        if (!result.IsSuccess)
            throw new HubException($"Conversation '{conversationId}' was not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task SendMessage(Guid conversationId, string body)
    {
        MessageDto dto;

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var result = await mediator.Send(new SendMessageCommand(conversationId, body));
            if (!result.IsSuccess)
                throw new HubException(string.Join(" ", result.Errors));

            dto = new MessageDto(result.Data, conversationId, MessageDirection.Outbound, body,
                Guid.Empty, DateTime.UtcNow); // CreatedBy/CreatedOn placeholders — see note below
        }
        else
        {
            var conversationExists = await dbContext.Conversations.AnyAsync(c => c.Id == conversationId);
            if (!conversationExists)
                throw new HubException($"Conversation '{conversationId}' was not found.");

            var message = new Message
            {
                ConversationId = conversationId,
                Direction = MessageDirection.Inbound,
                Body = body
            };
            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();

            dto = new MessageDto(message.Id, conversationId, MessageDirection.Inbound, body,
                message.CreatedBy, message.CreatedOn);
        }

        await Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", dto);
    }
}
```

The authenticated branch's `MessageDto` is built with placeholder `CreatedBy`/`CreatedOn` values because `SendMessageCommand`'s `Result<Guid>` only returns the new message's id (Story 08), not the full row — **this is a real gap**: the broadcast payload's `createdBy`/`createdOn` will not match what `GET /api/conversations/{id}/messages` later returns for the same message. Fix this properly during implementation by either (a) changing `SendMessageCommand`'s return type to `Result<MessageDto>` (a small, self-contained edit only this story would make to a Story 08 file, so confirm Stories 09-11 haven't already taken a dependency on the `Result<Guid>` shape before doing this — they haven't, per their Context sections, since none of them call `SendMessageCommand` directly), or (b) having the hub re-fetch the message via a new small query after sending. Do not ship the placeholder values as-is; this is flagged here rather than hidden because it's a correctness gap, not a style choice.

**Edit file: `src/AzmCrm.API/Program.cs`** — add `using AzmCrm.API.Hubs;` and, immediately after `app.MapControllers();` (line 64):

```csharp
app.MapHub<ChatHub>("/hubs/chat");
```

**Edit file: `src/AzmCrm.API/Controllers/ConversationsController.cs`** — add `using AzmCrm.Application.Features.Communications.Commands.StartLiveChat;` and one new action:

```csharp
[HttpPost("live-chat/start")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> StartLiveChat([FromBody] StartLiveChatRequest request, CancellationToken ct)
{
    var command = new StartLiveChatCommand(request.Name, request.Email, request.Body);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, id => $"/api/conversations/{id}");
}
```

No migration required.

## Edge Cases & Failure Modes

- **Anyone who obtains a conversation's Guid id can join its live-chat group and read/post to it** — this is the deliberate, documented tradeoff from Story Goal, not an oversight: `Guid.CreateVersion7()` ids (`BaseEntity.Id`, `src/AzmCrm.Domain/Common/BaseEntity.cs`) are effectively unguessable, so this is comparable in strength to a private support-chat share-link, but it is **not** equivalent to real customer authentication — the id could leak via a referrer header, browser history, or a copy-pasted URL. Do not extend this pattern to any conversation containing information more sensitive than ordinary support-chat correspondence without building real customer identity first.
- **`ChatHub` is not authorized at the class level, so a completely unauthenticated WebSocket client can call `JoinConversation`/`SendMessage` for *any* conversation id, including `Email`/`WhatsApp`/`Sms`/`WebForm` ones, not just `LiveChat` ones** — nothing in `JoinConversation` or `SendMessage` checks `conversation.Channel`. This means an anonymous caller who guesses or obtains any conversation's id (not just a `LiveChat` one) can inject a fake inbound message into an email or WhatsApp thread via the hub. **This is a real gap to close before production use** — either check `Channel == CommunicationChannel.LiveChat` inside both hub methods and throw `HubException` otherwise, or split live-chat-specific real-time access into its own, narrower validation. Flagged here explicitly rather than fixed silently because it changes the hub's contract (a caller currently relying on cross-channel joining, if any exists by the time this is read, would break) — decide and apply this fix as part of implementing this story, not as a follow-up.
- **The JWT-via-query-string authentication hook (Task 3) applies to any path under `/hubs`, not just `/hubs/chat`** — harmless today since this is the only hub, but if a second hub is ever added under `/hubs/*` without its own reasoning, revisit whether the same broad match is still appropriate.
- **`Context.User` inside a Hub method may not resolve the same way `IHttpContextAccessor`-based `CurrentUserService` expects** — flagged in Context item 3; verify this explicitly (see Verification Steps) rather than assuming `Message.CreatedBy` is correctly stamped for hub-originated agent messages before considering this story complete.
- **The placeholder `CreatedBy`/`CreatedOn` values in `ChatHub.SendMessage`'s authenticated branch** (Task 4) — a real, flagged correctness gap; do not ship without addressing it (see Task 4's note for the two suggested fixes).
- **No SignalR backplane is configured** — `services.AddSignalR()` (Task 3) defaults to in-memory, single-process group management. If the API is ever deployed with more than one instance behind a load balancer, two customers/agents connected to different instances but joined to the same conversation's "group" will not see each other's messages, because SignalR's default in-memory backplane doesn't share group membership across processes. This is explicitly out of scope (see Story Goal) since nothing in this codebase's current deployment configuration (`src/AzmCrm.API/Program.cs`, `appsettings.json`) indicates a multi-instance setup exists yet — revisit if/when it does.
- **A conversation created via `POST /api/conversations` with `channel: "LiveChat"` (Story 08's agent-initiated path) rather than via `POST /api/conversations/live-chat/start`** — nothing prevents an agent from doing this, but no customer widget would know its id to join, so it would just sit as an agent-only, one-sided conversation. Not a bug, just a reminder that `live-chat/start` (customer-initiated) and `POST /api/conversations` (agent-initiated) both work for the `LiveChat` channel but serve different flows.
- **A `SendMessage` hub call for a nonexistent `conversationId`** — the authenticated branch surfaces `SendMessageCommand`'s existing `NotFoundException` (Story 08) as an unhandled exception inside the hub method unless wrapped; **this code as written does not catch `NotFoundException` from `mediator.Send(...)`** in the authenticated branch (only the anonymous branch explicitly checks existence before inserting) — add a `try/catch` around the authenticated branch's `mediator.Send` call that translates `NotFoundException` into a `HubException` the same way the anonymous branch's explicit check does, so a bad id produces a clean client-facing hub error instead of an unhandled server exception. This is a second real gap in the Task 4 code as drafted — fix it during implementation, not after.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/StartLiveChatCommandHandlerTests.cs`** — `Start_with_new_email_creates_customer_conversation_and_inbound_message`; `Start_with_existing_email_reuses_customer`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/StartLiveChatCommandValidatorTests.cs`** — `Empty_Name_fails`; `Invalid_Email_fails`; `Empty_Body_fails`; `Valid_command_passes`.
3. **`ChatHub` itself is not covered by `tests/AzmCrm.Application.Tests/`** (that project only references `AzmCrm.Application`, not `AzmCrm.API` — confirmed by `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`'s single `ProjectReference`). If hub-level logic (the authenticated-vs-anonymous branching, the channel-check fix from Edge Cases, the `NotFoundException` handling fix) needs unit coverage, create a new `tests/AzmCrm.API.Tests/` project referencing `AzmCrm.API` and use `Microsoft.AspNetCore.SignalR.Testing` or a hand-rolled fake `HubCallerContext`/`IGroupManager` — there is no existing precedent for this in the codebase, so establish the project structure by mirroring `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`'s shape (same `TargetFramework`, `IsTestProject`, xUnit packages) with the project reference changed to `AzmCrm.API`. At minimum, cover: `SendMessage_from_authenticated_context_dispatches_SendMessageCommand`; `SendMessage_from_anonymous_context_creates_inbound_message_directly`; `SendMessage_for_missing_conversation_throws_HubException_not_unhandled_exception`; `JoinConversation_for_missing_conversation_throws_HubException`.
4. **Manual/integration verification is the primary coverage for this story** given the lack of hub-testing precedent (see Verification Steps) — prioritize fixing the two flagged Edge Cases gaps (channel check, `NotFoundException` handling) and manually confirming `CreatedBy` resolution over building out full hub unit-test infrastructure if time is constrained, since those are the story's real risk areas.

## Migration / Rollback

No migration required — this story adds no new tables, columns, or entities. Rollback is simply reverting the code changes (including the `Program.cs` and `DependencyInjection.cs` edits).

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` (and `tests/AzmCrm.API.Tests/`, if created).
3. **Manual smoke test (customer start + message):** `POST /api/conversations/live-chat/start` with `{"name":"Jane Doe","email":"jane@example.com","body":"Hi, are you open?"}`, confirm 201 and capture the returned conversation id; using a WebSocket/SignalR test client (e.g. a minimal browser console script using `@microsoft/signalr`, or Postman's WebSocket support) connect to `ws://localhost:5100/hubs/chat` with no auth, call `JoinConversation` with that id, confirm it succeeds; call `SendMessage` with that id and a body, confirm the caller itself receives a `ReceiveMessage` broadcast.
4. **Manual smoke test (agent joins and replies, with `CreatedBy` verification):** obtain a bearer token via `POST /api/identity/login`; connect a second SignalR client to `ws://localhost:5100/hubs/chat?access_token=<jwt>`, call `JoinConversation` with the same conversation id, confirm it succeeds; call `SendMessage` from the agent client, confirm the customer client (still connected from step 3) receives the broadcast; then call `GET /api/conversations/{id}/messages` over REST and confirm the agent's message's `createdBy` matches the agent's real user id (not `Guid.Empty`) — this is the concrete check for the `IHttpContextAccessor`-in-a-hub concern raised in Context item 3.
5. **Manual smoke test (cross-channel access check, once the Edge Cases fix is applied):** create an `Email`-channel conversation via `POST /api/conversations`, then attempt `JoinConversation`/`SendMessage` against its id from an anonymous hub client, and confirm it is now rejected.

## Done Criteria

- [ ] `POST /api/conversations/live-chat/start` creates or reuses a customer and starts a new `LiveChat` conversation.
- [ ] `ChatHub` is mapped at `/hubs/chat`; both an anonymous customer connection and a JWT-authenticated agent connection (via `?access_token=`) can join a conversation and exchange real-time messages.
- [ ] `Message.CreatedBy` on a hub-authored agent message is verified (not assumed) to resolve to the real agent's user id.
- [ ] The cross-channel hub access gap and the missing `NotFoundException` handling in `ChatHub.SendMessage`'s authenticated branch (both flagged in Edge Cases) are fixed, not shipped as-is.
- [ ] The placeholder `CreatedBy`/`CreatedOn` values in the authenticated branch's broadcast payload are replaced with real values (via one of the two fixes Task 4 describes), not shipped as-is.
- [ ] All new unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user — this KAN-3 slice (Stories 08-12) is now complete once this story lands.**
