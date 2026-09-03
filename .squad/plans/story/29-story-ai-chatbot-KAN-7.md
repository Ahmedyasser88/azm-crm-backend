# Story 29 — AI Chatbot for Customer Self-Service (Story: KAN-7)

## Prerequisites

- [25-story-ai-ticket-summaries-KAN-7.md](25-story-ai-ticket-summaries-KAN-7.md) completed: requires `IAiClient`, `OpenAiSettings`/`OpenAiClient`, and their DI registration.
- Story 08 completed (KAN-3): requires `Conversation`, `Message`, `CommunicationChannel`, `IApplicationDbContext.Conversations`/`Messages`, `ConversationsController`.
- Story 12 completed (KAN-3, Live Chat): `StartLiveChatCommand`/Handler and `ChatHub` are this story's direct structural precedent — read both in full.
- **Does not depend on** [28-story-ai-knowledge-base-suggestions-KAN-7.md](28-story-ai-knowledge-base-suggestions-KAN-7.md) — this story has no `Ticket` to key a suggestion off; it independently reuses the same `Contains`-based knowledge base matching technique, scoped to a customer's free-text chat message instead of a ticket's title.

## Story Goal

Let an anonymous customer start a chat session and exchange messages with an AI chatbot that answers using the published knowledge base, satisfying KAN-7's "Deploy AI chatbot for customer self-service" acceptance criterion.

Outcomes:
1. `CommunicationChannel` gains a new `Chatbot` member (after `WebForm`) — additive, no migration required, since `Channel` is stored via `HasConversion<string>().HasMaxLength(20)` (`ConversationConfiguration`) and `"Chatbot"` (7 characters) fits the existing column.
2. `POST /api/conversations/chatbot/start` is a new, `[AllowAnonymous]`, rate-limited (`[EnableRateLimiting("fixed")]`) action — the same public shape as KAN-3 Story 12's `POST /api/conversations/live-chat/start` — that finds-or-creates a `Customer` by email (identical to `StartLiveChatCommandHandler`), creates a `Conversation` with `Channel = CommunicationChannel.Chatbot`, persists the customer's opening message as an `Inbound` `Message`, and **synchronously generates and persists an AI-written, knowledge-base-grounded `Outbound` reply**, returning both messages plus the new conversation id in one response.
3. `POST /api/conversations/chatbot/{id:guid}/messages` is a second new, `[AllowAnonymous]`, rate-limited action for every subsequent turn in an existing chatbot conversation: persists the customer's message as `Inbound`, generates and persists another AI reply as `Outbound`, and returns both.
4. The AI reply is grounded in the published knowledge base: the handler searches `Published` `KnowledgeArticle` rows whose `Title`/`Content` case-insensitively contains a term from the customer's message (the same `Contains` technique KAN-6 Story 24 and [28-story-ai-knowledge-base-suggestions-KAN-7.md](28-story-ai-knowledge-base-suggestions-KAN-7.md) already use, scoped here to the top 3 matches), and includes their `Title`/`Content` as context in the AI system prompt, instructing it to answer only from that context or say a human agent will follow up.
5. If the AI call fails (provider unreachable, misconfigured key, non-2xx), the customer's own message is **still persisted** (it was saved before the AI call in both actions), and a static, friendly fallback message ("Thanks for reaching out — one of our agents will follow up shortly.") is persisted as the bot's `Outbound` reply instead of surfacing a 500 — mirrors `SendMessageCommandHandler`'s "the message is already saved, a downstream failure must never make this request look like it failed" principle (KAN-3 Story 08).

**Not in scope**: real-time push delivery for chatbot messages — this story is HTTP request/response only; `ChatHub` (KAN-3 Story 12) is **not** extended to the `Chatbot` channel (its existing `GetLiveChatConversationOrThrowAsync` check already rejects a non-`LiveChat` conversation id, so no change to `ChatHub.cs` is needed), flagged as a follow-up below; escalating/handing off a chatbot conversation to a human agent (an agent can still use the existing, unauthenticated-agnostic `GET/POST /api/conversations/{id}/messages` actions once authenticated, since `Conversation`/`Message` are channel-agnostic aggregates — no new action is added for this); multi-turn conversation memory beyond what's already persisted in `Message` rows (each AI call only receives the current customer message plus matched KB context, not the full prior chat history — flagged as a follow-up); rate-limiting or abuse-prevention beyond the existing `"fixed"` policy already applied to every other public conversation-starting action.

## Context — Read These Files First

1. [25-story-ai-ticket-summaries-KAN-7.md](25-story-ai-ticket-summaries-KAN-7.md) — read in full for `IAiClient`'s exact signature and existing DI wiring.
2. [src/AzmCrm.Domain/Features/Communications/CommunicationChannel.cs](../../../src/AzmCrm.Domain/Features/Communications/CommunicationChannel.cs) (full file, 10 lines) — the enum this story adds `Chatbot` to, after `WebForm`.
3. [src/AzmCrm.Infrastructure/Data/Configurations/ConversationConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/ConversationConfiguration.cs) lines 18-21 — confirms `Channel` is `HasConversion<string>().HasMaxLength(20)`, so adding `Chatbot` needs no migration (Context item verified directly, not assumed).
4. [src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommand.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommand.cs) and [StartLiveChatCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommandHandler.cs) (read both in full, 6 and 44 lines) — the exact find-or-create-customer-by-email + create-`Conversation`-with-a-fixed-`Channel` + add-inbound-`Message` + single-`SaveChangesAsync` shape this story's `StartAiChatCommandHandler` follows, extended with a second save for the bot's reply.
5. [src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommandValidator.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/StartLiveChat/StartLiveChatCommandValidator.cs) — read in full for its exact `Name`/`Email`/`Body` validation rules; `StartAiChatCommandValidator` copies them verbatim (do not invent different rules).
6. [src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommand.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommand.cs), [SendMessageCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandHandler.cs), and [SendMessageCommandValidator.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandValidator.cs) — read all three in full. `SendMessageCommandHandler.cs` lines demonstrating the "message already saved, a downstream failure must never fail this request" pattern (its `try/catch` around the channel-sender dispatch) is the direct precedent for this story's own "save customer message, then try/catch the AI reply" structure. `SendMessageCommandValidator.cs`'s `Body` rule is copied verbatim for `SendChatbotMessageCommandValidator`.
7. [src/AzmCrm.Application/Features/Communications/DTOs/](../../../src/AzmCrm.Application/Features/Communications/DTOs/) — grep for `record MessageDto` to confirm its exact field list/order (`Id, ConversationId, Direction, Body, CreatedBy, CreatedOn` per prior research; verify directly) before constructing new `MessageDto` instances in this story's handlers.
8. [src/AzmCrm.API/Hubs/ChatHub.cs](../../../src/AzmCrm.API/Hubs/ChatHub.cs) (full file, 78 lines) — confirm `GetLiveChatConversationOrThrowAsync`'s `conversation.Channel != CommunicationChannel.LiveChat` check (lines ~67-77) already excludes a `Chatbot`-channel conversation id from being joined via this hub, so **no edit to this file is required or made** by this story (see Story Goal, "Not in scope").
9. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) lines 170-182 (`StartLiveChat` action) — the exact `[HttpPost("live-chat/start")] [AllowAnonymous] [EnableRateLimiting("fixed")]` action shape this story's two new actions follow; appended after line 182, before the closing brace at 183.
10. [src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryHandler.cs](../../../src/AzmCrm.Application/Features/KnowledgeBase/Queries/SearchKnowledgeArticles/SearchKnowledgeArticlesQueryHandler.cs) (full file, 54 lines) — the `Contains`-based `Published`-only match pattern this story's `ChatbotReplyGenerator` reuses (scaled down to `Title`/`Content` only, top 3, no pagination).
11. [tests/AzmCrm.Application.Tests/TestDoubles/StubAiClient.cs](25-story-ai-ticket-summaries-KAN-7.md) (created by Story 25 at `tests/AzmCrm.Application.Tests/TestDoubles/StubAiClient.cs`) — reused directly by this story's tests.

## Implementation tasks

### 1 — Domain

**Edit file: `src/AzmCrm.Domain/Features/Communications/CommunicationChannel.cs`** — add `Chatbot` after `WebForm`:

```csharp
public enum CommunicationChannel
{
    Email,
    WhatsApp,
    LiveChat,
    Sms,
    WebForm,
    Chatbot
}
```

No changes to `ConversationConfiguration.cs` and no EF migration are needed — `HasConversion<string>().HasMaxLength(20)` already accommodates the new member (verified in Context item 3).

### 2 — Shared reply-generation logic

Both new commands need identical AI-reply-generation logic (search published KB articles for the customer's message, build a grounded prompt, call `IAiClient`, fall back to a static message on failure). Rather than duplicating this ~20-line block across two handlers (a larger duplication than the small, already-established handler-level duplications in Stories 25/26), factor it into one internal helper shared by both — a deliberate, explicit, small deviation from this codebase's usual "every handler is fully self-contained" style, justified because both call sites are in the same feature area and the logic is identical, not merely similar.

**Create file: `src/AzmCrm.Application/Features/Communications/ChatbotReplyGenerator.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications;

/// <summary>
/// Generates a knowledge-base-grounded AI reply to a customer's chatbot message. Shared by
/// StartAiChatCommandHandler and SendChatbotMessageCommandHandler. Never throws — a caller-visible
/// failure to generate a reply must never break the chatbot flow (see FallbackReply).
/// </summary>
internal static class ChatbotReplyGenerator
{
    private const string FallbackReply = "Thanks for reaching out — one of our agents will follow up shortly.";

    public static async Task<string> GenerateAsync(
        IApplicationDbContext dbContext, IAiClient aiClient, string customerMessage, CancellationToken ct)
    {
        var term = customerMessage.Trim().ToLower();

        var articles = await dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a => a.Title.ToLower().Contains(term) || a.Content.ToLower().Contains(term))
            .OrderByDescending(a => a.PublishedOn)
            .Take(3)
            .Select(a => new { a.Title, a.Content })
            .ToListAsync(ct);

        var context = articles.Count > 0
            ? string.Join("\n\n", articles.Select(a => $"Article: {a.Title}\n{a.Content}"))
            : "No matching knowledge base articles were found.";

        var systemPrompt =
            "You are a customer self-service chatbot for a support team. Answer the customer's message " +
            "using only the knowledge base context below. If the context does not contain a relevant " +
            "answer, politely say a human agent will follow up. Keep answers short and friendly.\n\n" +
            context;

        try
        {
            return await aiClient.GetCompletionAsync(systemPrompt, customerMessage, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FallbackReply;
        }
    }
}
```

### 3 — DTOs

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/ChatbotReplyDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record ChatbotReplyDto(Guid ConversationId, MessageDto CustomerMessage, MessageDto BotReply);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/StartAiChatRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record StartAiChatRequest(string Name, string Email, string Body);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/SendChatbotMessageRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record SendChatbotMessageRequest(string Body);
```

### 4 — StartAiChatCommand

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartAiChat/StartAiChatCommand.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.StartAiChat;

public sealed record StartAiChatCommand(string Name, string Email, string Body) : IRequest<Result<ChatbotReplyDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartAiChat/StartAiChatCommandValidator.cs`** — copy `StartLiveChatCommandValidator`'s exact rules verbatim (Context item 5), adjusted only for the class/command name.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/StartAiChat/StartAiChatCommandHandler.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.StartAiChat;

internal sealed class StartAiChatCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<StartAiChatCommand, Result<ChatbotReplyDto>>
{
    public async Task<Result<ChatbotReplyDto>> Handle(StartAiChatCommand request, CancellationToken ct)
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
            Channel = CommunicationChannel.Chatbot
        };
        dbContext.Conversations.Add(conversation);

        var customerMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        };
        dbContext.Messages.Add(customerMessage);

        // Customer message and conversation are saved before the AI call — an AI-provider
        // failure below must never lose the customer's own message. See ChatbotReplyGenerator.
        await dbContext.SaveChangesAsync(ct);

        var replyText = await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, request.Body, ct);

        var botMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = replyText
        };
        dbContext.Messages.Add(botMessage);

        await dbContext.SaveChangesAsync(ct);

        var dto = new ChatbotReplyDto(
            conversation.Id,
            new MessageDto(customerMessage.Id, conversation.Id, customerMessage.Direction, customerMessage.Body,
                customerMessage.CreatedBy, customerMessage.CreatedOn),
            new MessageDto(botMessage.Id, conversation.Id, botMessage.Direction, botMessage.Body,
                botMessage.CreatedBy, botMessage.CreatedOn));

        return Result<ChatbotReplyDto>.Success(dto);
    }
}
```

Adjust the two `new MessageDto(...)` constructions above to match `MessageDto`'s real, confirmed field order (Context item 7) if it differs from the assumed `(Id, ConversationId, Direction, Body, CreatedBy, CreatedOn)` shape.

### 5 — SendChatbotMessageCommand

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendChatbotMessage/SendChatbotMessageCommand.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;

public sealed record SendChatbotMessageCommand(Guid ConversationId, string Body) : IRequest<Result<ChatbotReplyDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendChatbotMessage/SendChatbotMessageCommandValidator.cs`** — copy `SendMessageCommandValidator`'s exact `Body` rule verbatim (Context item 6), plus a `RuleFor(x => x.ConversationId).NotEmpty()...` rule.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendChatbotMessage/SendChatbotMessageCommandHandler.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;

internal sealed class SendChatbotMessageCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<SendChatbotMessageCommand, Result<ChatbotReplyDto>>
{
    public async Task<Result<ChatbotReplyDto>> Handle(SendChatbotMessageCommand request, CancellationToken ct)
    {
        // A conversation id belonging to a different channel 404s indistinguishably from a
        // nonexistent id — never confirms existence of a mismatched-channel resource. Same
        // reasoning as ChatHub.GetLiveChatConversationOrThrowAsync's Channel check (KAN-3 Story 12).
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.Channel == CommunicationChannel.Chatbot, ct)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var customerMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body
        };
        dbContext.Messages.Add(customerMessage);

        await dbContext.SaveChangesAsync(ct);

        var replyText = await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, request.Body, ct);

        var botMessage = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = replyText
        };
        dbContext.Messages.Add(botMessage);

        await dbContext.SaveChangesAsync(ct);

        var dto = new ChatbotReplyDto(
            conversation.Id,
            new MessageDto(customerMessage.Id, conversation.Id, customerMessage.Direction, customerMessage.Body,
                customerMessage.CreatedBy, customerMessage.CreatedOn),
            new MessageDto(botMessage.Id, conversation.Id, botMessage.Direction, botMessage.Body,
                botMessage.CreatedBy, botMessage.CreatedOn));

        return Result<ChatbotReplyDto>.Success(dto);
    }
}
```

### 6 — API layer

**Edit file: `src/AzmCrm.API/Controllers/ConversationsController.cs`** — add `using AzmCrm.Application.Features.Communications.Commands.StartAiChat;` and `using AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;`, then append two new actions after `StartLiveChat` (after line 182, before the closing brace at 183):

```csharp
[HttpPost("chatbot/start")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
[ProducesResponseType(typeof(Result<ChatbotReplyDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> StartAiChat([FromBody] StartAiChatRequest request, CancellationToken ct)
{
    var command = new StartAiChatCommand(request.Name, request.Email, request.Body);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, dto => $"/api/conversations/{dto?.ConversationId}");
}

[HttpPost("chatbot/{id:guid}/messages")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
[ProducesResponseType(typeof(Result<ChatbotReplyDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> SendChatbotMessage(Guid id, [FromBody] SendChatbotMessageRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new SendChatbotMessageCommand(id, request.Body), ct);
    return ToResult(result);
}
```

**Route ordering note**: `chatbot/start` and `chatbot/{id:guid}/messages` both sit under a new `chatbot/` literal prefix, distinct from the existing `{id:guid}/messages` (no prefix) and `live-chat/start` routes — no collision is possible.

## Edge Cases & Failure Modes

- **AI provider unreachable/misconfigured during either action** — the customer's own message is already committed to the database (first `SaveChangesAsync`, before `ChatbotReplyGenerator.GenerateAsync` runs) regardless of what happens next; `ChatbotReplyGenerator` itself catches the AI-call exception and returns a static fallback string, so the second `SaveChangesAsync` (the bot's reply) always succeeds and the HTTP response is always 200/201 with a graceful bot message — never a 500.
- **`ConversationId` does not exist, or exists but is not a `Chatbot`-channel conversation** (e.g. a `LiveChat` conversation id passed to `SendChatbotMessage`) — both cases 404 identically via the combined `c.Id == ... && c.Channel == CommunicationChannel.Chatbot` predicate, never distinguishing "wrong channel" from "doesn't exist" in the response (same non-disclosure reasoning as `ChatHub`'s existing channel check).
- **Same customer email starts a second chatbot session** — `StartAiChatCommandHandler` finds and reuses the existing `Customer` row by normalized email, exactly as `StartLiveChatCommandHandler` already does; a new `Conversation` is still created per `start` call (this story does not attempt to resume a customer's prior chatbot conversation automatically).
- **No knowledge base articles match the customer's message** — `ChatbotReplyGenerator` still calls the AI with `"No matching knowledge base articles were found."` as its context, so the AI is expected (via its system prompt) to say a human agent will follow up rather than fabricate an answer — this depends on the model actually honoring that instruction, which is not independently enforced/validated by this story's code.
- **Empty/whitespace `Body`** on either action — rejected by the copied `SendMessageCommandValidator`/`StartLiveChatCommandValidator` rules before either handler runs.
- **A customer joins a `Chatbot`-channel conversation id through `ChatHub`** — already prevented by `ChatHub.GetLiveChatConversationOrThrowAsync`'s existing `Channel != CommunicationChannel.LiveChat` check (KAN-3 Story 12); no change needed.
- **Follow-up flagged, not implemented**: real-time push delivery of chatbot replies via `ChatHub`/SignalR; multi-turn conversation memory (each AI call only sees the current message, not the full `Message` history for that `Conversation`); escalation/handoff from chatbot to a human agent as an explicit action.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/StartAiChatCommandHandlerTests.cs`** (uses `StubAiClient` from Story 25):
   - `Start_creates_customer_conversation_and_persists_inbound_and_outbound_messages`
   - `Start_reuses_existing_customer_by_email`
   - `Start_when_AiClient_throws_persists_fallback_bot_reply_and_still_succeeds`
   - `Start_creates_Conversation_with_Chatbot_channel`
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/SendChatbotMessageCommandHandlerTests.cs`**:
   - `Send_persists_inbound_and_outbound_messages`
   - `Send_for_missing_conversation_throws_NotFoundException`
   - `Send_for_conversation_with_different_channel_throws_NotFoundException`
   - `Send_when_AiClient_throws_persists_fallback_bot_reply_and_still_succeeds`
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ChatbotReplyGeneratorTests.cs`**:
   - `Generate_includes_matching_Published_article_content_in_prompt`
   - `Generate_excludes_Draft_articles_from_context`
   - `Generate_returns_fallback_message_when_AiClient_throws`
4. **Create validator test files** for `StartAiChatCommandValidator` and `SendChatbotMessageCommandValidator`, mirroring whatever test file already exists for `StartLiveChatCommandValidator`/`SendMessageCommandValidator` (grep for them under `tests/AzmCrm.Application.Tests/Features/Communications/` first).
5. All new tests use `TestApplicationDbContext.Create()`, `StubLocalizationService`, and `StubAiClient` — no schema/DbSet changes in this story (the `CommunicationChannel.Chatbot` addition needs no new `HasQueryFilter`/`DbSet` entry anywhere, since it's a new enum value on an existing entity/column, not a new entity).

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test** (requires a reachable PostgreSQL and a valid `OpenAi:ApiKey`/mock endpoint): publish a knowledge base article (KAN-6) about a common issue; `POST /api/conversations/chatbot/start` with a `Body` matching that issue (no `Authorization` header) and confirm a 201 with both `CustomerMessage`/`BotReply` populated and a bot reply that reflects the article's content; `POST /api/conversations/chatbot/{id}/messages` with a follow-up message on the returned conversation id and confirm a 200; then attempt the same follow-up call against a `live-chat/start`-created conversation id and confirm 404.

## Done Criteria

- [ ] `POST /api/conversations/chatbot/start` and `POST /api/conversations/chatbot/{id}/messages` are reachable without an `Authorization` header.
- [ ] A customer's message is persisted as an `Inbound` `Message` and an AI-generated reply is persisted as an `Outbound` `Message`, both retrievable via the existing `GET /api/conversations/{id}/messages` endpoint.
- [ ] The AI reply is grounded in matching `Published` knowledge base articles when any exist.
- [ ] An AI-provider failure never returns a 500 — it persists a graceful fallback bot reply instead, and the customer's own message is never lost.
- [ ] A `ConversationId` for a non-`Chatbot`-channel conversation 404s on `SendChatbotMessage`.
- [ ] All new handler/validator/`ChatbotReplyGenerator` unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-7's "Deploy AI chatbot for customer self-service" acceptance criterion and completes all five KAN-7 acceptance criteria across Stories 25-29.
