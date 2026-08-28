# Story 11 — SMS Channel: Send & Receive (Story: KAN-3)

## Prerequisites

- [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) completed: requires the `Conversation`/`Message` entities, `IApplicationDbContext.Conversations`/`Messages`, `ConversationsController`, and the `IChannelMessageSender` extension point.
- Independent of [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md), [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md), and [12-story-live-chat-channel-KAN-3.md](12-story-live-chat-channel-KAN-3.md) — see Story 09's Prerequisites for why these four channel stories can be implemented and merged in any order.
- This story follows the exact structure of [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md) (phone-number-based customer resolution, HTTP-based outbound provider, `ExternalMessageId` idempotency) with a Twilio-shaped inbound webhook contract instead of Meta's, since Twilio is the most common SMS gateway with a simple, well-documented webhook. Read that story first even though this one doesn't depend on it being implemented.

## Story Goal

Satisfy KAN-3's "Support SMS communication" acceptance criterion. An agent's reply on an `Sms`-channel conversation is sent through an SMS gateway provider automatically; an inbound SMS is turned into a new (or continued) `Sms`-channel `Conversation` via a webhook endpoint shaped after Twilio's standard inbound-SMS webhook contract (`application/x-www-form-urlencoded` with `From`/`Body`/`MessageSid` fields).

Outcomes:
1. Sending a message on an `Sms`-channel conversation dispatches it via an HTTP call to a configured SMS gateway, using the existing `POST /api/conversations/{id}/messages` endpoint.
2. `POST /api/conversations/sms/inbound` accepts Twilio-shaped, form-encoded inbound SMS data, resolves the sender to an existing `Customer` by phone number or creates one, appends the message to that customer's most recent open `Sms` conversation (creating one if none exists), and returns 202 Accepted.

**Not in scope**: MMS/media attachments, delivery-status callbacks (Twilio's separate `StatusCallback` mechanism), obtaining a real Twilio (or other gateway) account/phone number/API credentials, and phone-number normalization (see Story 10's Edge Cases — the same limitation applies here unchanged).

## Context — Read These Files First

1. [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md) — read in full. This story's `ReceiveInboundSmsCommand`/handler/validator, `SmsSettings`, and DI registrations are structurally identical to that story's WhatsApp equivalents; only the provider's HTTP contract and the inbound webhook's payload shape differ (form-encoded vs. JSON, since Twilio's webhook convention is form-encoded while Meta's is JSON).
2. [src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs) — created by Story 08. `SmsChannelMessageSender` (Task 2) is this story's implementation.
3. [src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundWhatsAppMessage/ReceiveInboundWhatsAppMessageCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundWhatsAppMessage/ReceiveInboundWhatsAppMessageCommandHandler.cs) — created by Story 10 (read Story 10's file for its exact content; this story's `ReceiveInboundSmsCommandHandler`, Task 2, is the same phone-number-matching logic with `CommunicationChannel.Sms` substituted for `CommunicationChannel.WhatsApp`).
4. [src/AzmCrm.Infrastructure/Communications/WhatsAppCloudApiProvider.cs](../../../src/AzmCrm.Infrastructure/Communications/WhatsAppCloudApiProvider.cs) (Story 10) — the `IHttpClientFactory`-typed-client pattern `SmsGatewayProvider` (Task 3) follows, substituting a generic REST send call for Meta's specific payload shape.
5. There is no existing `[FromForm]` model-binding usage anywhere in this codebase (confirmed by grep for `IFormFile`/`FromForm` across `src/` — only `IFormFile` appears, for the unrelated customer-attachment upload in KAN-1 Story 04) — this story is the first to bind a `[FromForm]` DTO with plain string fields (not a file). ASP.NET Core supports this natively via `[ApiController]`'s model binding; no new package is required.
6. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) — created by Story 08. This story edits this file to add one new `[AllowAnonymous]` action bound to a form-encoded body rather than JSON.

## Implementation tasks

### 1 — Domain layer

No domain changes required.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Shared/Interfaces/ISmsProvider.cs`**

```csharp
namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single SMS text message via an HTTP-based SMS
/// gateway (e.g. Twilio). The Application layer never depends on a specific gateway's HTTP
/// contract directly.
/// </summary>
public interface ISmsProvider
{
    Task SendAsync(string toPhoneNumber, string body, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/SmsInboundWebhookRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record SmsInboundWebhookRequest(string From, string Body, string? MessageSid);
```

Field names (`From`, `Body`, `MessageSid`) match Twilio's actual inbound-SMS webhook form fields exactly, so a real Twilio "Messaging webhook" configured to POST to `/api/conversations/sms/inbound` needs no adapter in front of it — unlike Story 09 (email) and Story 10 (WhatsApp), which both need a translation step for their respective providers' real payloads.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundSms/ReceiveInboundSmsCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;

public sealed record ReceiveInboundSmsCommand(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundSms/ReceiveInboundSmsCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;

internal sealed class ReceiveInboundSmsCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ReceiveInboundSmsCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReceiveInboundSmsCommand request, CancellationToken ct)
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
                        && c.Channel == CommunicationChannel.Sms
                        && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerId = customer.Id,
                Channel = CommunicationChannel.Sms
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

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundSms/ReceiveInboundSmsCommandValidator.cs`** — identical rule shape to Story 10's `ReceiveInboundWhatsAppMessageCommandValidator` (`FromPhoneNumber` required + phone pattern, `Body` required + max 4000).

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Communications/SmsSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.Communications;

public sealed class SmsSettings
{
    public const string SectionName = "Sms";

    public string ApiBaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "CHANGE_ME";
    public string SenderId { get; init; } = "AzmCRM";
}
```

**Create file: `src/AzmCrm.Infrastructure/Communications/SmsGatewayProvider.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class SmsGatewayProvider(HttpClient httpClient, IOptions<SmsSettings> settings) : ISmsProvider
{
    private readonly SmsSettings _settings = settings.Value;

    public async Task SendAsync(string toPhoneNumber, string body, CancellationToken ct = default)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var payload = new { from = _settings.SenderId, to = toPhoneNumber, body };

        var response = await httpClient.PostAsJsonAsync(_settings.ApiBaseUrl, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

Unlike Story 10's `WhatsAppCloudApiProvider` (which targets Meta's one specific, documented API), this provider's request shape (`from`/`to`/`body`, bearer-token auth) is a **generic placeholder**, not any one named gateway's actual contract — SMS gateways vary widely in their send-API shape (Twilio's REST API, for instance, uses form-encoded `Body`/`To`/`From` against a Basic-Auth-protected, account-SID-scoped URL, not a bearer-token JSON POST). Replace this method's body with the actual chosen provider's documented send-API contract before going live; do not assume this placeholder shape works against any real gateway unmodified (see Edge Cases).

**Create file: `src/AzmCrm.Infrastructure/Communications/SmsChannelMessageSender.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class SmsChannelMessageSender(
    ISmsProvider provider,
    ILogger<SmsChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.Sms;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.PhoneNumber))
        {
            logger.LogWarning(
                "Skipped SMS dispatch for message {MessageId}: customer {CustomerId} has no phone number on file",
                message.Id, conversation.CustomerId);
            return;
        }

        await provider.SendAsync(conversation.Customer.PhoneNumber, message.Body, ct);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — after the existing channel registrations (or after `IFileStorageService`'s registration if this is the first channel story to land):

```csharp
services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));
services.AddHttpClient<SmsGatewayProvider>();
services.AddScoped<ISmsProvider>(provider => provider.GetRequiredService<SmsGatewayProvider>());
services.AddScoped<IChannelMessageSender, SmsChannelMessageSender>();
```

**Edit file: `src/AzmCrm.API/appsettings.json`** — add a new top-level section:

```json
"Sms": {
  "ApiBaseUrl": "",
  "ApiKey": "CHANGE_ME_SmsGatewayApiKey",
  "SenderId": "AzmCRM"
}
```

No migration required.

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/ConversationsController.cs`** — add `using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;` and one new action:

```csharp
[HttpPost("sms/inbound")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
[Consumes("application/x-www-form-urlencoded")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ReceiveInboundSms([FromForm] SmsInboundWebhookRequest request, CancellationToken ct)
{
    var command = new ReceiveInboundSmsCommand(request.From, request.Body, request.MessageSid);

    await mediator.Send(command, ct);

    return Accepted();
}
```

`[Consumes("application/x-www-form-urlencoded")]` documents (and, via Swagger, advertises) that this action expects form-encoded data, matching Twilio's actual webhook `Content-Type` — the same effect `[FromForm]` model binding already produces at runtime regardless, but the attribute makes the contract explicit in the generated OpenAPI spec (`Swashbuckle.AspNetCore`, already referenced by `AzmCrm.API.csproj`).

## Edge Cases & Failure Modes

- **This story's outbound `SmsGatewayProvider` uses a generic, unverified request shape** (see Task 3) — unlike Story 10's WhatsApp provider, which targets one specific, named, documented API (Meta's), no specific SMS gateway is targeted here since the story doesn't name one. Whoever picks a real SMS gateway (Twilio, Vonage, a regional Saudi provider, etc.) must replace `SmsGatewayProvider.SendAsync`'s HTTP call with that gateway's actual documented contract — this is a more significant gap than Story 10's, and should be called out to reviewers explicitly as "send path is a placeholder, not yet provider-specific."
- **The inbound webhook, by contrast, *is* provider-specific (Twilio-shaped)** — `SmsInboundWebhookRequest`'s field names (`From`, `Body`, `MessageSid`) match Twilio's real webhook form fields exactly, so if the chosen gateway ends up being a different provider than Twilio for sending, the inbound webhook may still need to point at Twilio specifically (or be reshaped) if that provider's inbound-SMS webhook uses different field names. Sending and receiving do not have to use the same underlying vendor, but this story's code currently assumes Twilio's shape for receiving regardless of what `SmsGatewayProvider` ends up calling for sending.
- **Missing shared-secret validation on the inbound webhook** — unlike Story 09 (email), which validates an `X-Webhook-Secret` header, this action has no equivalent check. Twilio's actual production integration pattern validates the `X-Twilio-Signature` header (an HMAC over the request using the Twilio Account's Auth Token) instead of a simple shared secret — implementing that signature-verification scheme correctly requires the exact Auth Token from a real Twilio account and is not implemented here (out of scope, same reasoning as not having real credentials to test against). **This means `/api/conversations/sms/inbound` is unauthenticated in this story** — flag this explicitly to reviewers and add signature verification (or, at minimum, an `X-Webhook-Secret`-style header check like Story 09's) before exposing this endpoint publicly.
- **Phone number formatting inconsistency and retried-webhook idempotency** — identical limitations to Story 10's WhatsApp equivalents; see that story's Edge Cases (the same `Customer.PhoneNumber` exact-match and `ExternalMessageId`-based dedup logic is reused here unchanged, just against `MessageSid` instead of Meta's message id).
- **Sending fails because `Conversation.Customer.PhoneNumber` is null, or the gateway HTTP call fails** — handled identically to Story 10's WhatsApp equivalents: `SmsChannelMessageSender` logs and skips for a missing phone number; `SendMessageCommandHandler`'s existing try/catch (Story 08) catches and logs any `HttpRequestException` from `EnsureSuccessStatusCode()`, and the outbound message is still persisted either way.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundSmsCommandHandlerTests.cs`** — same test names and shapes as Story 10's `ReceiveInboundWhatsAppMessageCommandHandlerTests`, substituting `CommunicationChannel.Sms` and `ReceiveInboundSmsCommand`: `Receive_with_new_sender_phone_creates_customer_and_conversation`; `Receive_with_existing_open_conversation_appends_to_it`; `Receive_with_closed_existing_conversation_creates_new_conversation`; `Receive_with_duplicate_ExternalMessageId_is_idempotent`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundSmsCommandValidatorTests.cs`** — `Empty_FromPhoneNumber_fails`; `Invalid_FromPhoneNumber_fails`; `Empty_Body_fails`; `Valid_command_passes`.
3. **Create file: `tests/AzmCrm.Infrastructure.Tests/Communications/SmsChannelMessageSenderTests.cs`** (create the `AzmCrm.Infrastructure.Tests` project first if neither Story 09 nor 10 has already created it) — a private `RecordingSmsProvider : ISmsProvider`. Tests: `SendAsync_with_customer_phone_calls_provider_with_expected_arguments`; `SendAsync_with_no_customer_phone_does_not_call_provider`.

## Migration / Rollback

No migration required — this story adds no new tables, columns, or entities. Rollback is simply reverting the code changes.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` and (if created) `dotnet test tests/AzmCrm.Infrastructure.Tests/AzmCrm.Infrastructure.Tests.csproj`.
3. **Manual smoke test (inbound):** `curl -X POST http://localhost:5100/api/conversations/sms/inbound -H "Content-Type: application/x-www-form-urlencoded" -d "From=%2B966512345678&Body=Hello&MessageSid=SM123"`, confirm `202 Accepted`; confirm via `GET /api/conversations?channel=Sms` (with an agent token) that a new conversation appears.
4. **Manual smoke test (outbound, requires a real SMS gateway account and an updated `SmsGatewayProvider` implementation matching that gateway's actual API):** configure real `Sms` settings, create an `Sms` conversation for a customer with a phone number that can receive SMS, `POST /api/conversations/{id}/messages`, and confirm delivery.

## Done Criteria

- [ ] `ISmsProvider`/`SmsGatewayProvider` and `SmsChannelMessageSender` exist and are registered in DI; sending a message on an `Sms` conversation attempts dispatch.
- [ ] `POST /api/conversations/sms/inbound` accepts Twilio-shaped form-encoded data, creates or reuses a customer by phone number, and appends to (or creates) an open `Sms` conversation, with `ExternalMessageId`-based idempotency.
- [ ] The lack of inbound-webhook signature/secret verification is explicitly flagged to reviewers as a known gap (see Edge Cases), not silently shipped as if it were secured.
- [ ] All new unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09, 10, or 12 (whichever haven't landed yet).**
