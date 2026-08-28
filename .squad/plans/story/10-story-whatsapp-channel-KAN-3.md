# Story 10 — WhatsApp Channel: Send & Receive (Story: KAN-3)

## Prerequisites

- [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) completed: requires the `Conversation`/`Message` entities, `IApplicationDbContext.Conversations`/`Messages`, `ConversationsController`, and the `IChannelMessageSender` extension point.
- Independent of [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md), [11-story-sms-channel-KAN-3.md](11-story-sms-channel-KAN-3.md), and [12-story-live-chat-channel-KAN-3.md](12-story-live-chat-channel-KAN-3.md) — see Story 09's Prerequisites for why these four channel stories can be implemented and merged in any order.
- This story follows the exact structure of [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md) (same `IChannelMessageSender` extension point, same "find-or-create customer, find-or-create open conversation" inbound pattern, same idempotency-by-`ExternalMessageId` mechanism) with phone numbers instead of email addresses and an HTTP-based provider instead of SMTP. Read that story first even though this one doesn't depend on it being implemented.

## Story Goal

Satisfy KAN-3's "Integrate WhatsApp messaging" acceptance criterion. An agent's reply on a `WhatsApp`-channel conversation is sent through a WhatsApp Business API provider automatically; an inbound WhatsApp message is turned into a new (or continued) `WhatsApp`-channel `Conversation` via a webhook endpoint shaped after the Meta (Facebook) WhatsApp Cloud API's public webhook contract — the most common way to receive WhatsApp messages without running your own on-premise Business API client.

Outcomes:
1. Sending a message on a `WhatsApp`-channel conversation dispatches it via an HTTP call to a configured WhatsApp Business API endpoint, using the existing `POST /api/conversations/{id}/messages` endpoint.
2. `GET /api/conversations/whatsapp/inbound` handles the Meta webhook verification handshake (a one-time setup step every Meta webhook subscription requires).
3. `POST /api/conversations/whatsapp/inbound` accepts an inbound-message notification, resolves the sender to an existing `Customer` by phone number or creates one, appends the message to that customer's most recent open `WhatsApp` conversation (creating one if none exists), and returns 202 Accepted.

**Not in scope**: media messages (images, documents, voice notes — text only), message templates/interactive buttons, WhatsApp Business Account (WABA) onboarding and phone number registration with Meta, and obtaining real Meta API credentials — this story wires the abstraction and Meta's documented webhook *shape*; the actual `AccessToken`/`PhoneNumberId`/`WebhookVerifyToken` values must be obtained from a real Meta Business account and supplied via configuration before this integration can send or receive anything for real (see Edge Cases).

## Context — Read These Files First

1. [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md) — read in full. This story's Application-layer command (`ReceiveInboundWhatsAppMessageCommand`), Infrastructure settings class, and DI registrations are structurally identical to that story's `ReceiveInboundEmail`/`SmtpSettings` equivalents — read it first for the pattern, then apply the differences called out below.
2. [src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs) — created by Story 08. `WhatsAppChannelMessageSender` (Task 2) is this story's implementation; resolved automatically by `SendMessageCommandHandler`'s `IEnumerable<IChannelMessageSender>` (Story 08) — that handler is **not edited** by this story.
3. [src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs) (Story 08) — the customer-resolution shape; here matched by `PhoneNumber` instead of `Email`.
4. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs) — lines 18-21 (the phone `Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")` rule). Reused by `ReceiveInboundWhatsAppMessageCommandValidator` for `FromPhoneNumber`.
5. [src/AzmCrm.Infrastructure/Storage/FileStorageSettings.cs](../../../src/AzmCrm.Infrastructure/Storage/FileStorageSettings.cs) — the settings-class shape `WhatsAppSettings` (Task 3) follows.
6. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — read in full. This story appends its own `Configure<WhatsAppSettings>`/`AddHttpClient`/`AddScoped` lines; it does not touch any line Story 09 or 11 add, since each channel's registration is a self-contained block.
7. There is no existing `IHttpClientFactory`/`AddHttpClient` usage anywhere in this codebase (confirmed by grep across `src/`) — this story is the first to introduce one. Read the [Microsoft.Extensions.Http](https://learn.microsoft.com/aspnet/core/fundamentals/http-requests) typed-client pattern's shape from the code in Task 3 below rather than an in-repo precedent; there isn't one.
8. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) — created by Story 08. This story edits this file to add two new actions (`GET` and `POST` on `whatsapp/inbound`).
9. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) — this story adds a new top-level `"WhatsApp"` section, independent of Story 09's `"Smtp"` section.

## Implementation tasks

### 1 — Domain layer

No domain changes required.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IWhatsAppProvider.cs`**

```csharp
namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single WhatsApp text message. Shaped around the
/// Meta WhatsApp Cloud API (the most common way to integrate WhatsApp without an on-premise
/// client), but the Application layer never depends on Meta's SDK/HTTP contract directly.
/// </summary>
public interface IWhatsAppProvider
{
    Task SendMessageAsync(string toPhoneNumber, string body, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/WhatsAppInboundWebhookRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record WhatsAppInboundWebhookRequest(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
);
```

Meta's actual webhook payload is a deeply nested JSON object (`entry[].changes[].value.messages[]`, etc.). This DTO is the *flattened* shape the controller action extracts into before dispatching a command — see Task 4 for where that extraction happens, and Edge Cases for why the exact Meta field paths are not hard-coded here without verifying them against Meta's current webhook documentation first.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundWhatsAppMessage/ReceiveInboundWhatsAppMessageCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;

public sealed record ReceiveInboundWhatsAppMessageCommand(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundWhatsAppMessage/ReceiveInboundWhatsAppMessageCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;

internal sealed class ReceiveInboundWhatsAppMessageCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ReceiveInboundWhatsAppMessageCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReceiveInboundWhatsAppMessageCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ExternalMessageId))
        {
            var existing = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.ExternalMessageId == request.ExternalMessageId, ct);
            if (existing is not null)
                return Result<Guid>.Success(existing.ConversationId);
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == request.FromPhoneNumber, ct);

        if (customer is null)
        {
            customer = new Customer
            {
                FullName = request.FromPhoneNumber,
                PhoneNumber = request.FromPhoneNumber
            };
            dbContext.Customers.Add(customer);
        }

        var conversation = await dbContext.Conversations
            .Where(c => c.CustomerId == customer.Id
                        && c.Channel == CommunicationChannel.WhatsApp
                        && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerId = customer.Id,
                Channel = CommunicationChannel.WhatsApp
            };
            dbContext.Conversations.Add(conversation);
        }

        dbContext.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Body = request.Body,
            ExternalMessageId = request.ExternalMessageId
        });

        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
```

Phone-number matching here is an exact string match (`c.PhoneNumber == request.FromPhoneNumber`), not case-insensitive `.ToLower()` like Story 09's email match — phone numbers have no casing, but they do have formatting variance (`+9665...` vs `05...` vs with/without spaces); see Edge Cases for why this story does not attempt phone-number normalization.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundWhatsAppMessage/ReceiveInboundWhatsAppMessageCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;

public sealed class ReceiveInboundWhatsAppMessageCommandValidator
    : AbstractValidator<ReceiveInboundWhatsAppMessageCommand>
{
    public ReceiveInboundWhatsAppMessageCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FromPhoneNumber)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "From Phone Number"])
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")
            .WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Communications/WhatsAppSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.Communications;

public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public string ApiBaseUrl { get; init; } = "https://graph.facebook.com/v21.0";
    public string PhoneNumberId { get; init; } = "";
    public string AccessToken { get; init; } = "CHANGE_ME";
    public string WebhookVerifyToken { get; init; } = "CHANGE_ME";
}
```

**Create file: `src/AzmCrm.Infrastructure/Communications/WhatsAppCloudApiProvider.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class WhatsAppCloudApiProvider(HttpClient httpClient, IOptions<WhatsAppSettings> settings)
    : IWhatsAppProvider
{
    private readonly WhatsAppSettings _settings = settings.Value;

    public async Task SendMessageAsync(string toPhoneNumber, string body, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiBaseUrl}/{_settings.PhoneNumberId}/messages";

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new { body }
        };

        var response = await httpClient.PostAsJsonAsync(url, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

The exact JSON payload shape (`messaging_product`/`type`/`text.body`) follows Meta's publicly documented WhatsApp Cloud API "send message" request format. **This has not been exercised against a live Meta endpoint in this session** — verify it against Meta's current API documentation and a real WhatsApp Business Account sandbox before relying on it (see Edge Cases).

**Create file: `src/AzmCrm.Infrastructure/Communications/WhatsAppChannelMessageSender.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class WhatsAppChannelMessageSender(
    IWhatsAppProvider provider,
    ILogger<WhatsAppChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.WhatsApp;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.PhoneNumber))
        {
            logger.LogWarning(
                "Skipped WhatsApp dispatch for message {MessageId}: customer {CustomerId} has no phone number on file",
                message.Id, conversation.CustomerId);
            return;
        }

        await provider.SendMessageAsync(conversation.Customer.PhoneNumber, message.Body, ct);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — add `using AzmCrm.Infrastructure.Communications;` (already added by Story 09 if both stories land; harmless duplicate `using` is a compile error, so if Story 09 already added it, don't add it twice) and, after Story 09's block (or after the existing `IFileStorageService` registration if Story 09 hasn't landed yet):

```csharp
services.Configure<WhatsAppSettings>(configuration.GetSection(WhatsAppSettings.SectionName));
services.AddHttpClient<WhatsAppCloudApiProvider>();
services.AddScoped<IWhatsAppProvider>(provider => provider.GetRequiredService<WhatsAppCloudApiProvider>());
services.AddScoped<IChannelMessageSender, WhatsAppChannelMessageSender>();
```

`services.AddHttpClient<WhatsAppCloudApiProvider>()` registers `WhatsAppCloudApiProvider` itself as a typed client (it becomes resolvable directly, with a managed `HttpClient` injected) — the extra `AddScoped<IWhatsAppProvider>(...)` line bridges that typed-client registration to the `IWhatsAppProvider` interface so `WhatsAppChannelMessageSender`'s constructor (which depends on the interface, not the concrete type) resolves correctly.

**Edit file: `src/AzmCrm.API/appsettings.json`** — add a new top-level section:

```json
"WhatsApp": {
  "ApiBaseUrl": "https://graph.facebook.com/v21.0",
  "PhoneNumberId": "",
  "AccessToken": "CHANGE_ME_MetaWhatsAppCloudApiAccessToken",
  "WebhookVerifyToken": "CHANGE_ME_MetaWebhookVerifyToken"
}
```

No migration required.

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/ConversationsController.cs`** — add `using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;`, add an `IOptions<WhatsAppSettings> whatsAppSettings` constructor parameter (alongside Story 09's `smtpSettings` parameter, if both stories have landed — otherwise just this one), and add two new actions:

```csharp
[HttpGet("whatsapp/inbound")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public IActionResult VerifyWhatsAppWebhook(
    [FromQuery(Name = "hub.mode")] string? mode,
    [FromQuery(Name = "hub.verify_token")] string? verifyToken,
    [FromQuery(Name = "hub.challenge")] string? challenge)
{
    if (mode != "subscribe" || verifyToken != whatsAppSettings.Value.WebhookVerifyToken)
        return Forbid();

    return Content(challenge ?? "", "text/plain");
}

[HttpPost("whatsapp/inbound")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ReceiveInboundWhatsAppMessage(
    [FromBody] WhatsAppInboundWebhookRequest request, CancellationToken ct)
{
    var command = new ReceiveInboundWhatsAppMessageCommand(
        request.FromPhoneNumber, request.Body, request.ExternalMessageId);

    await mediator.Send(command, ct);

    return Accepted();
}
```

`VerifyWhatsAppWebhook` implements Meta's one-time webhook verification handshake: when a webhook subscription is configured in the Meta Business dashboard, Meta sends a `GET` with these three query parameters and expects the raw `hub.challenge` value echoed back as plain text if `hub.verify_token` matches what was configured — otherwise a non-200 response. This action takes the *flattened* `WhatsAppInboundWebhookRequest` shape (Task 2) rather than Meta's actual nested JSON envelope; **a real integration needs an extra deserialization/extraction step in front of this action** (or a small adapter service) to pull `FromPhoneNumber`/`Body`/`ExternalMessageId` out of Meta's actual `entry[].changes[].value.messages[]` structure — flagged here rather than hard-coded speculatively (see Edge Cases).

## Edge Cases & Failure Modes

- **This story's `POST whatsapp/inbound` action takes a flattened DTO, not Meta's actual nested webhook envelope** — as called out in Task 4, wiring this against a real Meta WhatsApp Business Account requires either (a) a small adapter/transform between Meta's webhook and this endpoint, or (b) rewriting this action to deserialize Meta's actual JSON structure directly and extract the same three fields before constructing `ReceiveInboundWhatsAppMessageCommand`. Do not treat this story's DTO shape as Meta's real payload — verify Meta's current webhook documentation before wiring a live subscription.
- **The Meta webhook verification handshake (`GET`) has no shared-secret check beyond `hub.verify_token`** — this is Meta's own documented mechanism, not a gap introduced here; the `WebhookVerifyToken` value must be set to match exactly what's entered in the Meta Business dashboard's webhook configuration, or the subscription will never activate.
- **Phone number formatting inconsistency** — `ReceiveInboundWhatsAppMessageCommandHandler` matches `Customer.PhoneNumber` with an exact string comparison, no normalization (no stripping of `+`, spaces, or country-code assumptions). A customer stored as `"0512345678"` will not match an inbound message reporting `"+966512345678"` even though they're the same number. Meta's Cloud API always reports numbers in E.164 format (`+<countrycode><number>`, no leading zero) — if customers are stored in a local format instead, this handler creates a *second*, duplicate `Customer` record rather than matching the existing one. Normalizing phone numbers to a single canonical format is out of scope for this story and for KAN-1; flag this as a follow-up if duplicate-customer creation from WhatsApp becomes a real problem.
- **Retried webhook deliveries** — handled the same way as Story 09's `ExternalMessageId` idempotency check; same caveat that it only works if Meta's `ExternalMessageId`-equivalent (the WhatsApp message id, `messages[].id` in Meta's real payload) is actually threaded through into `ReceiveInboundWhatsAppMessageCommand.ExternalMessageId` by whatever extracts Meta's envelope into this story's flattened DTO.
- **Sending fails because `Conversation.Customer.PhoneNumber` is null** — handled the same way as Story 09's missing-email case: `WhatsAppChannelMessageSender` logs a warning and returns without throwing; the outbound `Message` is still persisted by `SendMessageCommandHandler` regardless (Story 08).
- **WhatsApp API call fails** (invalid access token, rate limit, phone number not opted in to receive messages) — `HttpClient.PostAsJsonAsync`'s response is checked with `EnsureSuccessStatusCode()`, which throws `HttpRequestException` on any non-2xx response; caught by `SendMessageCommandHandler`'s existing try/catch (Story 08), logged as a warning, message still persisted, request still returns success.
- **No real Meta Business Account/credentials exist in this environment** — `WhatsAppSettings.AccessToken`/`WebhookVerifyToken` default to `"CHANGE_ME"` placeholders; `WhatsAppCloudApiProvider.SendMessageAsync` will fail with an authentication error against the real Meta API until real credentials are configured. This is expected and does not block merging this story — the abstraction and wiring are complete and testable in isolation (see Test Plan) independent of having live credentials.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundWhatsAppMessageCommandHandlerTests.cs`** — mirrors Story 09's `ReceiveInboundEmailCommandHandlerTests` structure: `Receive_with_new_sender_phone_creates_customer_and_conversation`; `Receive_with_existing_open_conversation_appends_to_it`; `Receive_with_closed_existing_conversation_creates_new_conversation`; `Receive_with_duplicate_ExternalMessageId_is_idempotent`; `Receive_with_differently_formatted_existing_phone_number_creates_duplicate_customer` (seed a customer with `PhoneNumber = "0512345678"`, receive from `"+966512345678"`, assert **two** `Customer` rows exist — this test documents the known limitation from Edge Cases rather than asserting incorrect "fixed" behavior).
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundWhatsAppMessageCommandValidatorTests.cs`** — `Empty_FromPhoneNumber_fails`; `Invalid_FromPhoneNumber_fails`; `Empty_Body_fails`; `Valid_command_passes`.
3. **Create file: `tests/AzmCrm.Infrastructure.Tests/Communications/WhatsAppChannelMessageSenderTests.cs`** (create the `AzmCrm.Infrastructure.Tests` project first if Story 09 hasn't already created it) — a small private `RecordingWhatsAppProvider : IWhatsAppProvider` recording its arguments. Tests: `SendAsync_with_customer_phone_calls_provider_with_expected_arguments`; `SendAsync_with_no_customer_phone_does_not_call_provider`.

## Migration / Rollback

No migration required — this story adds no new tables, columns, or entities. Rollback is simply reverting the code changes.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` and (if created) `dotnet test tests/AzmCrm.Infrastructure.Tests/AzmCrm.Infrastructure.Tests.csproj`.
3. **Manual smoke test (webhook verification):** `GET /api/conversations/whatsapp/inbound?hub.mode=subscribe&hub.verify_token=<configured token>&hub.challenge=12345`, confirm the response body is exactly `12345`; repeat with a wrong `hub.verify_token` and confirm `403 Forbidden`.
4. **Manual smoke test (inbound message):** `POST /api/conversations/whatsapp/inbound` with `{"fromPhoneNumber":"+966512345678","body":"Hi, is my order shipped?"}`, confirm `202 Accepted`; confirm via `GET /api/conversations?channel=WhatsApp` (with an agent token) that a new conversation appears.
5. **Manual smoke test (outbound, requires real Meta credentials):** configure real `WhatsApp` settings against a Meta WhatsApp Business test number, create a `WhatsApp` conversation for a customer whose phone number is registered with that test number, `POST /api/conversations/{id}/messages`, and confirm the message arrives on the test device.

## Done Criteria

- [ ] `IWhatsAppProvider`/`WhatsAppCloudApiProvider` and `WhatsAppChannelMessageSender` exist and are registered in DI; sending a message on a `WhatsApp` conversation attempts dispatch via the Meta Cloud API shape.
- [ ] `GET /api/conversations/whatsapp/inbound` correctly implements Meta's webhook verification handshake.
- [ ] `POST /api/conversations/whatsapp/inbound` creates or reuses a customer by phone number and appends to (or creates) an open `WhatsApp` conversation, with `ExternalMessageId`-based idempotency.
- [ ] All new unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09, 11, or 12 (whichever haven't landed yet).**
