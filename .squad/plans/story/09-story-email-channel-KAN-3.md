# Story 09 — Email Channel: Send & Receive (Story: KAN-3)

## Prerequisites

- [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) completed: requires the `Conversation`/`Message` entities, `IApplicationDbContext.Conversations`/`Messages`, `ConversationsController`, the `IChannelMessageSender` extension point, and the `TestApplicationDbContext` test double.
- Independent of [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md), [11-story-sms-channel-KAN-3.md](11-story-sms-channel-KAN-3.md), and [12-story-live-chat-channel-KAN-3.md](12-story-live-chat-channel-KAN-3.md) — all four depend only on Story 08 and can be implemented and merged in any order. Each adds only new files plus a few small, additive edits to `DependencyInjection.cs`, `ConversationsController.cs`, and `appsettings.json` — none edit the same new lines another channel story adds, so merge conflicts are limited to nearby-but-distinct insertions in those three files.

## Story Goal

Satisfy KAN-3's "Send and receive emails within the CRM" acceptance criterion. An agent's reply on an `Email`-channel conversation (via Story 08's `POST /api/conversations/{id}/messages`) is sent out over SMTP automatically; an inbound email arriving at the support mailbox is turned into a new (or continued) `Email`-channel `Conversation` via a webhook endpoint that the email provider/forwarding rule calls.

Outcomes:
1. Sending a message on an `Email`-channel conversation dispatches it via SMTP to the conversation's customer, using the existing `POST /api/conversations/{id}/messages` endpoint — no new outbound endpoint is added.
2. `POST /api/conversations/email/inbound` accepts an inbound-email notification (from whatever forwarding mechanism the mail provider uses — see Edge Cases for why this story defines a generic JSON shape rather than a specific provider's webhook contract), resolves the sender to an existing `Customer` by email or creates one, appends the message to that customer's most recent **open** `Email` conversation (creating one if none exists), and returns 202 Accepted.

**Not in scope**: parsing MIME/multipart raw email content, attachments on emails, HTML email bodies (plain text only), email threading via `In-Reply-To`/`References` headers, and obtaining real SMTP credentials or configuring a specific inbound-forwarding provider (e.g. SendGrid Inbound Parse, Postmark, Mailgun) — this story wires the abstraction and a generic webhook shape; swapping in a specific provider's exact payload field names is a follow-up once one is chosen, and is called out explicitly in Edge Cases rather than guessed at.

## Context — Read These Files First

1. [08-story-communication-core-webforms-KAN-3.md](08-story-communication-core-webforms-KAN-3.md) — read in full. This story adds new files plus edits three files it created (`DependencyInjection.cs` is Infrastructure's, not Story 08's, but the other two — `ConversationsController.cs` and `appsettings.json` — are edited here for the first time since Story 08).
2. [src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs) — created by Story 08. `EmailChannelMessageSender` (Task 2) is this story's one implementation; it is resolved automatically by `SendMessageCommandHandler`'s `IEnumerable<IChannelMessageSender>` constructor parameter (Story 08) — that handler is **not edited** by this story.
3. [src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandHandler.cs) — created by Story 08, read in full. Confirms the `sender.SendAsync(conversation, message, ct)` call already passes `conversation.Customer` (eagerly `.Include`d) — `EmailChannelMessageSender` reads `conversation.Customer.Email` from the same object, no extra query needed.
4. [src/AzmCrm.Application/Shared/Interfaces/IFileStorageService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IFileStorageService.cs) and [src/AzmCrm.Infrastructure/Storage/LocalFileStorageService.cs](../../../src/AzmCrm.Infrastructure/Storage/LocalFileStorageService.cs) — read both in full. Exact precedent this story's `IEmailSender`/`SmtpEmailSender` pair follows: a small Application-layer interface plus one Infrastructure-layer implementation, backed by a settings class.
5. [src/AzmCrm.Infrastructure/Storage/FileStorageSettings.cs](../../../src/AzmCrm.Infrastructure/Storage/FileStorageSettings.cs) — read in full (9 lines). Exact shape `SmtpSettings` (Task 3) follows: a `public const string SectionName` plus `{ get; init; }` properties with defaults.
6. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — read in full (95 lines, current end-state after KAN-2). Lines 88-89 (`services.Configure<FileStorageSettings>(...)` / `services.AddScoped<IFileStorageService, LocalFileStorageService>();`) are the exact two-line pattern this story appends for `SmtpSettings`/`IEmailSender` and for `IChannelMessageSender`/`EmailChannelMessageSender`.
7. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) — read in full (63 lines). The `"FileStorage"` section (lines 53-56) is the exact shape a new top-level `"Smtp"` section (Task 3) follows.
8. [src/AzmCrm.API/Controllers/IdentityController.cs](../../../src/AzmCrm.API/Controllers/IdentityController.cs) — lines 18-35 (`Register`). `[AllowAnonymous]` + `[EnableRateLimiting("fixed")]` precedent for `POST /api/conversations/email/inbound`, the same pattern Story 08 already used for `web-form`.
9. [src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs](../../../src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs) — created by Story 08, read in full. `ReceiveInboundEmailCommandHandler` (Task 2) reuses this file's exact "case-insensitive email match, else create a `Customer` with just `FullName`/`Email`" logic, then diverges: instead of always creating a new `Conversation`, it looks for an existing **open** `Email` conversation for that customer first (see Task 2 and Edge Cases for why inbound email needs this and web-form submissions don't).
10. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs) — lines 14-16 (the `EmailAddress()` rule). `ReceiveInboundEmailCommandValidator` reuses this exact rule shape for `FromEmail`.
11. [src/AzmCrm.Domain/Features/Communications/Message.cs](../../../src/AzmCrm.Domain/Features/Communications/Message.cs) — created by Story 08. `ExternalMessageId` (defined but unused by Story 08) is set by `ReceiveInboundEmailCommandHandler` from the inbound webhook's message id, when the provider supplies one, and used for the idempotency check described in Edge Cases.
12. [src/AzmCrm.API/Controllers/ConversationsController.cs](../../../src/AzmCrm.API/Controllers/ConversationsController.cs) — created by Story 08, read in full. This story **edits** this file to add one new `[AllowAnonymous]` action, following the exact shape of the existing `SubmitWebForm` action.

## Implementation tasks

### 1 — Domain layer

No domain changes required — `CommunicationChannel.Email`, `MessageDirection`, `Conversation`, and `Message.ExternalMessageId` all already exist from Story 08.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs`**

```csharp
namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single plain-text email. The Application layer
/// never touches SMTP directly — swap the Infrastructure-layer implementation (e.g. to a
/// transactional-email API) without changing <see cref="IChannelMessageSender"/> or any handler.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/EmailInboundWebhookRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record EmailInboundWebhookRequest(
    string FromEmail,
    string? FromName,
    string? Subject,
    string Body,
    string? ExternalMessageId
);
```

This is a generic, provider-agnostic shape (see Story Goal, "Not in scope") — before wiring a specific inbound-email provider, map its actual webhook payload onto these five fields at the provider's edge (e.g. a small transform in the provider's dashboard, or a thin adapter) rather than reshaping this DTO to match one provider's exact field names.

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundEmail/ReceiveInboundEmailCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

public sealed record ReceiveInboundEmailCommand(
    string FromEmail,
    string? FromName,
    string? Subject,
    string Body,
    string? ExternalMessageId
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundEmail/ReceiveInboundEmailCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

internal sealed class ReceiveInboundEmailCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ReceiveInboundEmailCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReceiveInboundEmailCommand request, CancellationToken ct)
    {
        // Idempotency: a webhook provider may retry the same delivery. If this exact provider
        // message id was already recorded, return the conversation it was already filed under
        // instead of creating a duplicate Message.
        if (!string.IsNullOrWhiteSpace(request.ExternalMessageId))
        {
            var existing = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.ExternalMessageId == request.ExternalMessageId, ct);
            if (existing is not null)
                return Result<Guid>.Success(existing.ConversationId);
        }

        var normalizedEmail = request.FromEmail.Trim().ToLower();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail, ct);

        if (customer is null)
        {
            customer = new Customer
            {
                FullName = request.FromName ?? request.FromEmail,
                Email = request.FromEmail
            };
            dbContext.Customers.Add(customer);
        }

        // Unlike a web-form submission (always a new Conversation), a running email thread
        // should land in the customer's existing open Email conversation, if one exists.
        var conversation = await dbContext.Conversations
            .Where(c => c.CustomerId == customer.Id
                        && c.Channel == CommunicationChannel.Email
                        && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerId = customer.Id,
                Channel = CommunicationChannel.Email,
                Subject = request.Subject
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

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/ReceiveInboundEmail/ReceiveInboundEmailCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

public sealed class ReceiveInboundEmailCommandValidator : AbstractValidator<ReceiveInboundEmailCommand>
{
    public ReceiveInboundEmailCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FromEmail)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "From Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Communications/SmtpSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.Communications;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "support@azm.com.sa";
    public string FromName { get; init; } = "Azm CRM Support";
    public string InboundWebhookSecret { get; init; } = "CHANGE_ME";
}
```

**Create file: `src/AzmCrm.Infrastructure/Communications/SmtpEmailSender.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class SmtpEmailSender(IOptions<SmtpSettings> settings) : IEmailSender
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);

        using var message = new MailMessage(
            new MailAddress(_settings.FromAddress, _settings.FromName),
            new MailAddress(toEmail))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        await client.SendMailAsync(message, ct);
    }
}
```

`System.Net.Mail.SmtpClient` is part of the .NET base class library — no new NuGet package required. It is the first use of `System.Net.Mail` anywhere in this codebase; there is no existing precedent to compare against, so double-check `SmtpClient`'s synchronous-looking constructor/`Credentials` setup against the target SMTP provider's actual connection requirements (STARTTLS vs. implicit TLS, port 587 vs. 465) during manual verification (see Verification Steps) rather than assuming this default configuration works unchanged for every provider.

**Create file: `src/AzmCrm.Infrastructure/Communications/EmailChannelMessageSender.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class EmailChannelMessageSender(
    IEmailSender emailSender,
    ILogger<EmailChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.Email;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.Email))
        {
            logger.LogWarning(
                "Skipped email dispatch for message {MessageId}: customer {CustomerId} has no email on file",
                message.Id, conversation.CustomerId);
            return;
        }

        var subject = conversation.Subject ?? "Re: your support request";
        await emailSender.SendAsync(conversation.Customer.Email, subject, message.Body, ct);
    }
}
```

A missing `Customer.Email` is handled here (skip + log) rather than throwing, so `SendMessageCommandHandler`'s existing try/catch (Story 08) doesn't even need to fire for this specific, expected case — throwing would still be caught and swallowed there anyway, but logging a clearer, purpose-built message here is more useful than a generic caught-exception warning.

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — add `using AzmCrm.Infrastructure.Communications;` and, after the existing `services.AddScoped<IFileStorageService, LocalFileStorageService>();` line:

```csharp
services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
services.AddScoped<IEmailSender, SmtpEmailSender>();
services.AddScoped<IChannelMessageSender, EmailChannelMessageSender>();
```

**Edit file: `src/AzmCrm.API/appsettings.json`** — add a new top-level section (after `"FileStorage"`):

```json
"Smtp": {
  "Host": "localhost",
  "Port": 587,
  "EnableSsl": true,
  "Username": "",
  "Password": "",
  "FromAddress": "support@azm.com.sa",
  "FromName": "Azm CRM Support",
  "InboundWebhookSecret": "CHANGE_ME_SharedSecretForInboundEmailWebhook"
}
```

Real SMTP credentials and the shared webhook secret belong in user secrets / environment-specific configuration for anything beyond local development, the same way `JwtSettings.Secret` already has a `"CHANGE_ME"` placeholder in this exact file.

No migration required — this story adds no new tables or columns.

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/ConversationsController.cs`** — add `using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;`, `using AzmCrm.Infrastructure.Communications;`, and `using Microsoft.Extensions.Options;`, add an `IOptions<SmtpSettings> smtpSettings` constructor parameter, and add one new action:

```csharp
public sealed class ConversationsController(IMediator mediator, IOptions<SmtpSettings> smtpSettings) : ApiControllerBase
{
    // ... existing actions unchanged ...

    [HttpPost("email/inbound")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveInboundEmail(
        [FromHeader(Name = "X-Webhook-Secret")] string? webhookSecret,
        [FromBody] EmailInboundWebhookRequest request,
        CancellationToken ct)
    {
        if (webhookSecret != smtpSettings.Value.InboundWebhookSecret)
            return Unauthorized();

        var command = new ReceiveInboundEmailCommand(
            request.FromEmail, request.FromName, request.Subject, request.Body, request.ExternalMessageId);

        await mediator.Send(command, ct);

        return Accepted();
    }
}
```

Returns `202 Accepted` (not `201 Created`) because the caller here is an automated webhook, not a browser client that needs a `Location` header — the same reasoning most webhook-receiving endpoints use; there's no existing precedent for this exact status code elsewhere in this codebase, so this is a deliberate, new choice for this endpoint's shape (see Edge Cases).

## Edge Cases & Failure Modes

- **This story's inbound webhook accepts a generic JSON shape, not a specific provider's actual payload** (e.g. SendGrid Inbound Parse posts `multipart/form-data` with different field names entirely) — as stated in Story Goal, choosing and integrating a specific inbound-email provider is out of scope here. Whoever configures the real provider must either use one that can be configured to POST this generic shape directly, or add a thin adapter in front of `/api/conversations/email/inbound` that reshapes the provider's actual webhook into `EmailInboundWebhookRequest`'s five fields. Do not guess a specific provider's field names into this DTO without confirming them against that provider's actual current documentation first.
- **Missing or wrong `X-Webhook-Secret` header** — `ConversationsController.ReceiveInboundEmail` compares it directly against `SmtpSettings.InboundWebhookSecret` before invoking any command and returns `401 Unauthorized`; this check happens in the controller (not a handler) because it's a transport-level concern (verifying the caller is the configured webhook source), not a business rule. A production deployment must change the placeholder value in `appsettings.json`/user secrets before exposing this endpoint publicly — the same expectation as `JwtSettings.Secret`'s existing `"CHANGE_ME"` placeholder.
- **A retried webhook delivery for the same `ExternalMessageId`** — `ReceiveInboundEmailCommandHandler` checks `dbContext.Messages.FirstOrDefaultAsync(m => m.ExternalMessageId == request.ExternalMessageId, ct)` before doing anything else and returns the existing conversation id without creating a duplicate `Message`. This only works if the provider actually supplies a stable, unique `ExternalMessageId` on every delivery of the same message — a provider that omits it (or sends a fresh one per retry) will still produce a duplicate `Message`; there is no other dedup mechanism in this story.
- **No `Customer.Email` on file for an existing customer whose email later changes at the provider side** — this handler only matches by the `FromEmail` on the inbound payload; if a customer emails from a different address than the one on file, a new `Customer` row is created rather than merging into their existing record. Customer de-duplication/merge is out of scope for KAN-1 and remains out of scope here.
- **Sending fails because `Conversation.Customer.Email` is null** (e.g. the conversation was created via `POST /api/conversations` for a customer who has no email on file, for the `Email` channel specifically — a data-entry mistake, since nothing prevents creating an `Email`-channel conversation for an email-less customer) — `EmailChannelMessageSender.SendAsync` checks for this explicitly and logs a warning instead of throwing (see Task 3); the outbound `Message` is still persisted successfully by `SendMessageCommandHandler` (Story 08).
- **SMTP connection/auth failure at send time** (wrong credentials, host unreachable, `SmtpException`) — caught by `SendMessageCommandHandler`'s existing try/catch (Story 08); logged as a warning, message still persisted, request still returns success. This is the general case Story 08's Edge Cases already documented; this story doesn't add new handling beyond letting that existing catch-all cover it.
- **Very large inbound email bodies** — `ReceiveInboundEmailCommandValidator` caps `Body` at 4000 characters, same as every other message body in this codebase (`SendMessageCommandValidator`, `SubmitWebFormCommandValidator`); a longer inbound email is rejected with a 400 rather than silently truncated. A real inbound-email provider integration would likely need to raise this limit or strip HTML/quoted-reply text first — out of scope here since no specific provider is wired yet.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundEmailCommandHandlerTests.cs`** — `Receive_with_new_sender_email_creates_customer_and_conversation`; `Receive_with_existing_open_conversation_appends_to_it_instead_of_creating_new` (seed a `Customer` and an open `Email` `Conversation` for them, receive an inbound email from that customer's address, assert exactly one `Conversation` row exists afterward and a second `Message` was appended to it); `Receive_with_closed_existing_conversation_creates_new_conversation` (seed a `Closed` `Email` conversation for the customer, assert a new, second `Conversation` is created rather than reopening the closed one); `Receive_with_duplicate_ExternalMessageId_is_idempotent` (call the handler twice with the same `ExternalMessageId`, assert only one `Message` row exists and both calls return the same conversation id).
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/ReceiveInboundEmailCommandValidatorTests.cs`** — `Empty_FromEmail_fails`; `Invalid_FromEmail_fails`; `Empty_Body_fails`; `Valid_command_passes` — use `StubLocalizationService`.
3. **Create file: `tests/AzmCrm.Infrastructure.Tests/Communications/EmailChannelMessageSenderTests.cs`** — if `tests/AzmCrm.Infrastructure.Tests/` does not yet exist in this repository, create it following the project-reference and package-reference shape of `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`, adjusted to reference `AzmCrm.Infrastructure` instead. Define a small private `RecordingEmailSender : IEmailSender` in the test file that records its arguments instead of sending anything real. Tests: `SendAsync_with_customer_email_calls_IEmailSender_with_expected_arguments`; `SendAsync_with_no_customer_email_does_not_call_IEmailSender` (assert the recording sender's call count stays zero — use `NullLogger<EmailChannelMessageSender>.Instance` for the logger parameter).

## Migration / Rollback

No migration required — this story adds no new tables, columns, or entities. Rollback is simply reverting the code changes; nothing in the database needs to change back.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` and, if created, `dotnet test tests/AzmCrm.Infrastructure.Tests/AzmCrm.Infrastructure.Tests.csproj`.
3. **Manual smoke test (outbound):** configure `Smtp` in `appsettings.Development.json` (or user secrets) against a real or local test SMTP server (e.g. a local `smtp4dev`/`MailHog` instance), create a customer with a real `Email`, `POST /api/conversations` with `channel: "Email"`, then `POST /api/conversations/{id}/messages`, and confirm the test SMTP server actually received the message.
4. **Manual smoke test (inbound):** `POST /api/conversations/email/inbound` with header `X-Webhook-Secret: <the configured secret>` and body `{"fromEmail":"jane@example.com","fromName":"Jane Doe","subject":"Help","body":"I need help with my order"}`; confirm `202 Accepted`; confirm via `GET /api/conversations?channel=Email` (with an agent token) that a new conversation appears; repeat the same request without the correct header and confirm `401 Unauthorized`.

## Done Criteria

- [ ] `IEmailSender`/`SmtpEmailSender` and `EmailChannelMessageSender` exist and are registered in DI; sending a message on an `Email` conversation attempts SMTP dispatch.
- [ ] `POST /api/conversations/email/inbound` requires the configured `X-Webhook-Secret` header, creates or reuses a customer by email, and appends to (or creates) an open `Email` conversation.
- [ ] Retried webhook deliveries with the same `ExternalMessageId` do not create duplicate messages.
- [ ] All new unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 10, 11, or 12 (any order).**
