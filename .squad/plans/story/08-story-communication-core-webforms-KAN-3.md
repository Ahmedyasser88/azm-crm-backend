# Story 08 — Communication Core: Conversations, Messages & Web Form Submissions (Story: KAN-3)

## Prerequisites

- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: this story's `Conversation` entity references the existing `Customer` entity (`src/AzmCrm.Domain/Features/Customers/Customer.cs`) via a `CustomerId` foreign key, the same way `CustomerInteraction`/`Ticket` reference it. `Customer.cs` is edited by nothing here except being read from — no changes to it.
- This is the first story in the KAN-3 ("Communication Channels Integration") slice. It establishes the `Conversation`/`Message` aggregate and the `IChannelMessageSender` extension point that [09-story-email-channel-KAN-3.md](09-story-email-channel-KAN-3.md), [10-story-whatsapp-channel-KAN-3.md](10-story-whatsapp-channel-KAN-3.md), [11-story-sms-channel-KAN-3.md](11-story-sms-channel-KAN-3.md), and [12-story-live-chat-channel-KAN-3.md](12-story-live-chat-channel-KAN-3.md) each plug an outbound sender and an inbound webhook into, without editing any file this story creates.

## Story Goal

Give support agents a generic message-thread model — a `Conversation` (one per customer contact episode, tagged with a channel) containing an ordered list of `Message` rows (inbound from the customer, outbound from an agent) — and use it to satisfy one full acceptance criterion end-to-end: **"Accept submissions from web forms"**. The other four KAN-3 acceptance criteria (email, WhatsApp, live chat, SMS) are satisfied by Stories 09-12, which each add one channel's outbound sender and inbound webhook on top of this story's `Conversation`/`Message` tables without changing their schema.

Outcomes:
1. `POST /api/conversations` lets an agent start a conversation with an existing customer on any channel (`Email`, `WhatsApp`, `LiveChat`, `Sms`, `WebForm`), with an optional subject.
2. `GET /api/conversations/{id}` returns a single conversation.
3. `GET /api/conversations` returns a paginated, filterable (`customerId`, `channel`, `status`) list of conversations.
4. `POST /api/conversations/{id}/messages` lets an agent append an outbound message to an existing conversation. The message is always persisted; if a channel-specific outbound sender is registered for that conversation's channel (none is, until Stories 09-11 add one), it's invoked afterward and a delivery failure is logged but never rolls back or fails the request (see Edge Cases).
5. `GET /api/conversations/{id}/messages` returns the conversation's messages, paginated, **oldest first** (chat-thread reading order — a deliberate, documented deviation from the newest-first convention used by `GET /api/tickets/{id}/history` and `GET /api/customers/{id}/interactions`; see Edge Cases).
6. `POST /api/conversations/web-form` is a public, unauthenticated endpoint a marketing-site contact form posts to. It resolves the submitter to an existing `Customer` by email (case-insensitive) or creates a new one, then creates a new `WebForm`-channel `Conversation` plus one inbound `Message`, satisfying "Accept submissions from web forms" completely.

**Not in scope for this story**: linking a `Conversation` to a `Ticket` (no `TicketId` column — KAN-2 and KAN-3 stay independent for now; a future story could add this join), closing/reopening a conversation via a dedicated endpoint (`Status` exists on the entity for Stories 09-12 to set, but no `PUT .../status` action is added here since no KAN-3 acceptance criterion calls for it), editing or deleting a message, and actually dispatching a message on any channel (`IChannelMessageSender` has zero registered implementations until Stories 09-11; `LiveChat` never uses this interface at all — see Story 12).

## Context — Read These Files First

1. [src/AzmCrm.Domain/Common/BaseEntity.cs](../../../src/AzmCrm.Domain/Common/BaseEntity.cs) — read in full (19 lines). `Conversation` and `Message` both extend this: `Id` (client-assigned `Guid.CreateVersion7()`), `CreatedBy`/`CreatedOn` (auto-stamped by `ApplicationDbContext.SaveChangesAsync`), `IsDeleted`.
2. [src/AzmCrm.Domain/Features/Customers/Customer.cs](../../../src/AzmCrm.Domain/Features/Customers/Customer.cs) — read in full (17 lines). Only `FullName` is `required`; `Email`/`PhoneNumber`/everything else is `string?`. `SubmitWebFormCommandHandler`'s auto-create path (Task 2 below) relies on this — it only ever sets `FullName`, `Email`, `PhoneNumber`.
3. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) and [TicketHistory.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketHistory.cs) — read both in full. `Conversation`/`Message` follow the exact same shape: a required `CustomerId` FK plus a one-directional `Customer` navigation property, `required` vs. mutable property split.
4. [src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs) and [TicketHistoryConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketHistoryConfiguration.cs) — read both in full. Exact EF configuration shape `ConversationConfiguration`/`MessageConfiguration` mirror: `ToTable`, `HasKey`, `Property(...).ValueGeneratedNever()`, `Property(...).HasConversion<string>().HasMaxLength(N).IsRequired()` for each enum, `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)`, `HasQueryFilter(...)`, `HasIndex(...)`.
5. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs) — read in full (33 lines). `SubmitWebFormCommandHandler`'s "create a `Customer` with just `FullName`/`Email`/`PhoneNumber`" step mirrors the `new Customer { ... }` construction here exactly.
6. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs) and [CreateTicketCommandValidator.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs) — the "verify parent exists via `AnyAsync`, else `NotFoundException`, then construct-and-save" command/handler/validator triad `CreateConversationCommand` mirrors.
7. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) and [GetTicketHistory/GetTicketHistoryQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketHistory/GetTicketHistoryQueryHandler.cs) — read both in full. `GetConversationsListQueryHandler` mirrors the first (filterable paginated list); `GetConversationMessagesQueryHandler` mirrors the second (parent-existence check + paginated child list) **except it orders `OrderBy(m => m.CreatedOn)` ascending, not descending** — see Story Goal outcome 5 and Edge Cases for why.
8. [src/AzmCrm.Application/Features/Identity/Commands/Login/LoginCommandHandler.cs](../../../src/AzmCrm.Application/Features/Identity/Commands/Login/LoginCommandHandler.cs) — lines 1-15 (constructor/injected dependencies only). This codebase's existing precedent for a handler taking an `ILogger<T>` constructor dependency — `SendMessageCommandHandler` (Task 2) is the first Application handler in this codebase to inject `ILogger<T>` for a "log and swallow" try/catch rather than an auth-flow log, so read this for the injection pattern only, not the log content.
9. [src/AzmCrm.Application/Shared/Interfaces/IFileStorageService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IFileStorageService.cs) — read in full (16 lines). Precedent this story's `IChannelMessageSender` interface follows: a small Application-layer abstraction with a one-line doc-comment explaining *why* it exists and that Infrastructure supplies the real implementation(s) — here, zero implementations for now, by design (see Task 2).
10. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (25 lines, current end-state after KAN-2). Add `DbSet<Conversation> Conversations { get; }` and `DbSet<Message> Messages { get; }` next to the existing `Ticket*` members (after line 22, `DbSet<TicketHistory> TicketHistories { get; }`).
11. [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) — read in full (63 lines). Lines 24-30 are the `DbSet<T>` properties to extend; line 32's comment (`// Add DbSet properties here for new CRM aggregates (Leads, Deals, ...).`) marks where. Lines 41-62 (`SaveChangesAsync`) auto-stamp `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` for every tracked `BaseEntity` — this covers `Conversation` and `Message` automatically.
12. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) (3 lines) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 (`NotFoundException` → HTTP 404 at lines 33-37) — throw it from every handler when a route/body id doesn't resolve to an existing, non-deleted row.
13. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) — read in full (46 lines). This story reuses `Validation.Required`, `Validation.MaxLength`, `Validation.InvalidValue`, `Validation.EmailInvalid`, `Validation.InvalidPhoneNumber` — **no new keys or `Messages.*.json` edits are needed**.
14. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs) — read in full (39 lines). Lines 14-21: the exact `EmailAddress()`/phone-`Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")` rule pairs `SubmitWebFormCommandValidator` reuses for its `Email`/`Phone` fields.
15. [src/AzmCrm.Application/Shared/Models/PaginatedResult.cs](../../../src/AzmCrm.Application/Shared/Models/PaginatedResult.cs) — read in full (12 lines). Used for both the conversations list and the messages list responses.
16. [src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs](../../../src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs) — read in full (29 lines). `ConversationsController` inherits `[Authorize]` + `[Route("api/[controller]")]` (→ `api/conversations`) from here; the `web-form` action overrides this per-action with `[AllowAnonymous]` (see item 17).
17. [src/AzmCrm.API/Controllers/IdentityController.cs](../../../src/AzmCrm.API/Controllers/IdentityController.cs) — lines 18-35 (`Register`) and lines 37-54 (`Login`). Exact precedent for a public, unauthenticated action on an otherwise-`[Authorize]` controller: `[AllowAnonymous]` plus (for `Login`) `[EnableRateLimiting("fixed")]`. `POST /api/conversations/web-form` uses both attributes, the same way `Login` does, since it's an anonymous, internet-facing endpoint.
18. [src/AzmCrm.API/Extensions/RateLimitingExtensions.cs](../../../src/AzmCrm.API/Extensions/RateLimitingExtensions.cs) — read in full (63 lines). The `"fixed"` named policy (line 31) is already registered via `services.AddCustomRateLimiting(...)` in `Program.cs` — no change needed here, just reuse `[EnableRateLimiting("fixed")]`.
19. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — lines 82-105 (`AddInteraction`/`GetInteractions`). Exact controller-action shape most of `ConversationsController`'s actions mirror.
20. [src/AzmCrm.Application/AssemblyInfo.cs](../../../src/AzmCrm.Application/AssemblyInfo.cs) (3 lines) — `[assembly: InternalsVisibleTo("AzmCrm.Application.Tests")]` already covers every `internal sealed class` handler in this story; no change needed.
21. Grep for `ApplyConfigurationsFromAssembly` in `ApplicationDbContext.cs` (line 38) — confirms `ConversationConfiguration`/`MessageConfiguration` are discovered automatically; no manual registration call needed.
22. [src/AzmCrm.Infrastructure/Data/Migrations/20260828132833_AddTicketEscalation.cs](../../../src/AzmCrm.Infrastructure/Data/Migrations/20260828132833_AddTicketEscalation.cs) — the most recent migration; the new migration for this story adds the `Conversations` and `Messages` tables on top of this baseline without modifying it.
23. [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) — read in full (39 lines). Add `Conversations`/`Messages` `DbSet<T>` properties and mirror their query filters in `OnModelCreating`, following the exact pattern already used for every other aggregate.
24. [tests/AzmCrm.Application.Tests/TestDoubles/StubCurrentUserService.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubCurrentUserService.cs) and [StubLocalizationService.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubLocalizationService.cs) — read both in full. Reused as-is; no changes.
25. [tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs](../../../tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs) — read in full (63 lines). Precedent for constructing a handler directly against `TestApplicationDbContext.Create()` and `StubCurrentUserService`/`StubLocalizationService`.
26. [tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj](../../../tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj) — read in full (22 lines). No `Microsoft.Extensions.Logging.Abstractions` package reference is listed, but the Application project takes `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (confirmed in `src/AzmCrm.Application/AzmCrm.Application.csproj`) and the test project references the Application project, so `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>` (used in this story's tests, Task 5) resolves transitively without any `.csproj` edit — verify this resolves at `dotnet build` time (see Edge Cases) since no test in this codebase has exercised it yet.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Communications/CommunicationChannel.cs`**

```csharp
namespace AzmCrm.Domain.Features.Communications;

public enum CommunicationChannel
{
    Email,
    WhatsApp,
    LiveChat,
    Sms,
    WebForm
}
```

All five KAN-3 channels are defined upfront, exactly like KAN-2 Story 05 defined the full `TicketStatus` enum before Stories 06-07 existed — this lets Stories 09-12 each add a new `IChannelMessageSender`/webhook without ever needing to edit this enum.

**Create file: `src/AzmCrm.Domain/Features/Communications/MessageDirection.cs`**

```csharp
namespace AzmCrm.Domain.Features.Communications;

public enum MessageDirection
{
    Inbound,
    Outbound
}
```

**Create file: `src/AzmCrm.Domain/Features/Communications/ConversationStatus.cs`**

```csharp
namespace AzmCrm.Domain.Features.Communications;

public enum ConversationStatus
{
    Open,
    Closed
}
```

Nothing in this story ever sets `Status` to `Closed` — every command that creates a `Conversation` defaults it to `Open`, and no "close conversation" endpoint exists yet (see Story Goal, "Not in scope"). The property exists now so Stories 09-12 and any future closing workflow don't need to edit `Conversation.cs`.

**Create file: `src/AzmCrm.Domain/Features/Communications/Conversation.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Domain.Features.Communications;

public sealed class Conversation : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public string? Subject { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    public Customer Customer { get; init; } = null!;
}
```

**Create file: `src/AzmCrm.Domain/Features/Communications/Message.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Communications;

public sealed class Message : BaseEntity
{
    public required Guid ConversationId { get; init; }
    public required MessageDirection Direction { get; init; }
    public required string Body { get; set; }
    public string? ExternalMessageId { get; set; }

    public Conversation Conversation { get; init; } = null!;
}
```

`ExternalMessageId` is unused by this story (always `null`) but defined now so Stories 09-11 can record a provider's message id for inbound-webhook idempotency (see each channel story's Edge Cases) without editing `Message.cs`. `Message` deliberately has no `SentByUserId`/`ChangedBy` field — `BaseEntity.CreatedBy`, auto-stamped by `ApplicationDbContext.SaveChangesAsync`, already records which agent sent an outbound message (or `Guid.Empty` for an anonymous inbound one — see Edge Cases), the same way `TicketHistory` reuses `CreatedBy` instead of a bespoke field.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IChannelMessageSender.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Channel-specific outbound dispatch for an already-persisted outbound <see cref="Message"/>.
/// This story registers zero implementations — <c>SendMessageCommandHandler</c> resolves every
/// registered sender and, if one's <see cref="Channel"/> matches the conversation's channel,
/// calls it. Stories 09-11 (email, WhatsApp, SMS) each add exactly one new Infrastructure-layer
/// implementation and one new DI registration; none of them need to edit this interface or
/// SendMessageCommandHandler. LiveChat never gets an implementation of this interface — Story 12
/// delivers live-chat messages via a SignalR hub instead, not a request/response send.
/// </summary>
public interface IChannelMessageSender
{
    CommunicationChannel Channel { get; }
    Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default);
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/ConversationDto.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record ConversationDto(
    Guid Id,
    Guid CustomerId,
    CommunicationChannel Channel,
    string? Subject,
    ConversationStatus Status,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/ConversationListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record ConversationListItemDto(
    Guid Id,
    Guid CustomerId,
    CommunicationChannel Channel,
    string? Subject,
    ConversationStatus Status,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/MessageDto.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    MessageDirection Direction,
    string Body,
    Guid CreatedBy,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/CreateConversationRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record CreateConversationRequest(Guid CustomerId, CommunicationChannel Channel, string? Subject);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/SendMessageRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record SendMessageRequest(string Body);
```

**Create file: `src/AzmCrm.Application/Features/Communications/DTOs/WebFormSubmissionRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record WebFormSubmissionRequest(string Name, string Email, string? Phone, string? Subject, string Body);
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/CreateConversation/CreateConversationCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

public sealed record CreateConversationCommand(
    Guid CustomerId,
    CommunicationChannel Channel,
    string? Subject
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/CreateConversation/CreateConversationCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

internal sealed class CreateConversationCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateConversationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateConversationCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var conversation = new Conversation
        {
            CustomerId = request.CustomerId,
            Channel = request.Channel,
            Subject = request.Subject
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/CreateConversation/CreateConversationCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Channel"]);

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Subject", 200]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SendMessage;

public sealed record SendMessageCommand(Guid ConversationId, string Body) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Communications.Commands.SendMessage;

internal sealed class SendMessageCommandHandler(
    IApplicationDbContext dbContext,
    IEnumerable<IChannelMessageSender> channelSenders,
    ILogger<SendMessageCommandHandler> logger)
    : IRequestHandler<SendMessageCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = request.Body
        };

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        // The message is already saved at this point — a delivery failure below must never
        // make this request look like it failed, since the agent's message genuinely was
        // recorded. See Edge Cases for the reasoning.
        var sender = channelSenders.FirstOrDefault(s => s.Channel == conversation.Channel);
        if (sender is not null)
        {
            try
            {
                await sender.SendAsync(conversation, message, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to dispatch outbound message {MessageId} on channel {Channel}",
                    message.Id, conversation.Channel);
            }
        }

        return Result<Guid>.Success(message.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SendMessage/SendMessageCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Conversation Id"]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;

public sealed record SubmitWebFormCommand(
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string Body
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;

internal sealed class SubmitWebFormCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SubmitWebFormCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SubmitWebFormCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail, ct);

        if (customer is null)
        {
            customer = new Customer
            {
                FullName = request.Name,
                Email = request.Email,
                PhoneNumber = request.Phone
            };
            dbContext.Customers.Add(customer);
        }

        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.WebForm,
            Subject = request.Subject
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

**Create file: `src/AzmCrm.Application/Features/Communications/Commands/SubmitWebForm/SubmitWebFormCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;

public sealed class SubmitWebFormCommandValidator : AbstractValidator<SubmitWebFormCommand>
{
    public SubmitWebFormCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid]);

        RuleFor(x => x.Phone)
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")
            .WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber])
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Subject", 200]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationById/GetConversationByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationById;

public sealed record GetConversationByIdQuery(Guid Id) : IRequest<Result<ConversationDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationById/GetConversationByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationById;

internal sealed class GetConversationByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationByIdQuery, Result<ConversationDto>>
{
    public async Task<Result<ConversationDto>> Handle(GetConversationByIdQuery request, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Conversation '{request.Id}' was not found.");

        var dto = new ConversationDto(
            conversation.Id, conversation.CustomerId, conversation.Channel, conversation.Subject,
            conversation.Status, conversation.CreatedOn, conversation.UpdatedOn);

        return Result<ConversationDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationsList/GetConversationsListQuery.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationsList;

public sealed record GetConversationsListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    CommunicationChannel? Channel = null,
    ConversationStatus? Status = null
) : IRequest<Result<PaginatedResult<ConversationListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationsList/GetConversationsListQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationsList;

internal sealed class GetConversationsListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationsListQuery, Result<PaginatedResult<ConversationListItemDto>>>
{
    public async Task<Result<PaginatedResult<ConversationListItemDto>>> Handle(
        GetConversationsListQuery request, CancellationToken ct)
    {
        var query = dbContext.Conversations.AsQueryable();

        if (request.CustomerId is not null)
            query = query.Where(c => c.CustomerId == request.CustomerId);

        if (request.Channel is not null)
            query = query.Where(c => c.Channel == request.Channel);

        if (request.Status is not null)
            query = query.Where(c => c.Status == request.Status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ConversationListItemDto(
                c.Id, c.CustomerId, c.Channel, c.Subject, c.Status, c.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<ConversationListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<ConversationListItemDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationsList/GetConversationsListQueryValidator.cs`** — same paging-range rules as `GetTicketsListQueryValidator` (`PageNumber >= 1`, `PageSize` between 1 and 100).

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationMessages/GetConversationMessagesQuery.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;

public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<MessageDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationMessages/GetConversationMessagesQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationMessagesQuery, Result<PaginatedResult<MessageDto>>>
{
    public async Task<Result<PaginatedResult<MessageDto>>> Handle(
        GetConversationMessagesQuery request, CancellationToken ct)
    {
        var conversationExists = await dbContext.Conversations.AnyAsync(c => c.Id == request.ConversationId, ct);
        if (!conversationExists)
            throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var query = dbContext.Messages.Where(m => m.ConversationId == request.ConversationId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.CreatedOn) // oldest first — chat-thread reading order, see Story Goal
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.Direction, m.Body, m.CreatedBy, m.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<MessageDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Communications/Queries/GetConversationMessages/GetConversationMessagesQueryValidator.cs`** — same paging-range rules as `GetConversationsListQueryValidator`, plus `RuleFor(x => x.ConversationId).NotEmpty()...`.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Communications;` and, after the existing `Ticket*` members:

```csharp
DbSet<Conversation> Conversations { get; }
DbSet<Message> Messages { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/ConversationConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Subject)
            .HasMaxLength(200);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.CustomerId);
        builder.HasIndex(c => c.Channel);
    }
}
```

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/MessageConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Communications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.Direction)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.ExternalMessageId)
            .HasMaxLength(200);

        builder.HasOne(m => m.Conversation)
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(m => !m.IsDeleted);

        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.ExternalMessageId);
    }
}
```

`ExternalMessageId` gets a non-unique index (it's `null` for every message this story ever creates, and SQL treats every `NULL` as distinct for uniqueness purposes anyway) so Stories 09-11's inbound-webhook idempotency checks (`WHERE ExternalMessageId = @id`) are indexed from day one.

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Communications;` and, replacing the placeholder comment:

```csharp
public DbSet<Conversation> Conversations => Set<Conversation>();
public DbSet<Message> Messages => Set<Message>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddCommunications --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

This produces a new file under `src/AzmCrm.Infrastructure/Data/Migrations/` that creates the `Conversations` and `Messages` tables on top of `20260828132833_AddTicketEscalation.cs`. Do not edit that or any earlier migration.

No change to `DependencyInjection.cs` in this story — `services.AddScoped<IEnumerable<IChannelMessageSender>>` needs no explicit registration; the built-in container resolves an empty `IEnumerable<T>` when zero implementations are registered, which is exactly the state Story 08 leaves it in.

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/ConversationsController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Communications.Commands.CreateConversation;
using AzmCrm.Application.Features.Communications.Commands.SendMessage;
using AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Features.Communications.Queries.GetConversationById;
using AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;
using AzmCrm.Application.Features.Communications.Queries.GetConversationsList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AzmCrm.API.Controllers;

public sealed class ConversationsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var command = new CreateConversationCommand(request.CustomerId, request.Channel, request.Subject);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/conversations/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetConversationByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<ConversationListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null, [FromQuery] CommunicationChannel? channel = null,
        [FromQuery] ConversationStatus? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetConversationsListQuery(pageNumber, pageSize, customerId, channel, status), ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SendMessageCommand(id, request.Body), ct);

        return ToCreatedResult(result, messageId => $"/api/conversations/{id}/messages/{messageId}");
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(Result<PaginatedResult<MessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetConversationMessagesQuery(id, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpPost("web-form")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitWebForm([FromBody] WebFormSubmissionRequest request, CancellationToken ct)
    {
        var command = new SubmitWebFormCommand(request.Name, request.Email, request.Phone, request.Subject, request.Body);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/conversations/{id}");
    }
}
```

Every action except `SubmitWebForm` relies on the base class's default `[Authorize]`, matching `TicketsController`'s convention — internal conversation data always requires an authenticated agent. Stories 09-12 add further `[AllowAnonymous]` inbound-webhook actions to this same file.

## Edge Cases & Failure Modes

- **`CustomerId` on `POST /api/conversations` does not resolve to an existing, non-deleted customer** — `CreateConversationCommandHandler` checks `dbContext.Customers.AnyAsync(...)` (query filter excludes soft-deleted rows) and throws `NotFoundException` → HTTP 404, identical to KAN-2 Story 05's `CustomerId` guard on ticket create.
- **`SendMessage` on a non-existent or soft-deleted conversation id** — `Conversations.FirstOrDefaultAsync` returns nothing because `ConversationConfiguration.HasQueryFilter(c => !c.IsDeleted)` excludes soft-deleted rows; the handler throws `NotFoundException` → 404.
- **A channel sender is registered but the outbound dispatch throws** (e.g. an SMTP connection fails in Story 09) — `SendMessageCommandHandler` already called `SaveChangesAsync` before invoking `sender.SendAsync(...)`, so the `Message` row exists in the database regardless of whether dispatch succeeds. The `catch` block logs a warning via `ILogger<SendMessageCommandHandler>` and the command still returns `Result<Guid>.Success(message.Id)` with HTTP 201. **This is a deliberate choice**: an agent's outbound reply is a real, recorded event even if the underlying channel is temporarily unreachable; failing the whole request would make the agent re-send and potentially double-post once the channel recovers. There is currently no delivery-status field on `Message` to surface "sent but not delivered" back to the UI — flag this as a follow-up if delivery confirmation becomes a requirement.
- **`GetConversationMessages` orders oldest-first, unlike every other paginated list in this codebase** (`GetTicketsListQueryHandler`, `GetTicketHistoryQueryHandler`, `GetCustomerInteractionsQueryHandler` all order `OrderByDescending(x => x.CreatedOn)`) — this is intentional: a message thread reads top-to-bottom in chronological order like any chat UI, whereas an audit/history list reads newest-first. Call this out explicitly to API consumers/frontend developers so they don't assume the same ordering convention applies everywhere.
- **`SubmitWebFormCommandHandler`'s customer email match is case-insensitive via `.ToLower()` comparison** — not translated to a database-side case-insensitive index; same acceptable-for-now .NET-side tradeoff already flagged in KAN-1 Story 01 for `GetCustomersListQueryHandler`'s search. Two customers with emails differing only by case cannot both exist and be matched deterministically by this query — if one already has mixed-case and another submission arrives with different casing, the first match wins.
- **Every web form submission creates a brand-new `Conversation`**, even if the same customer already has an open `WebForm` conversation from an earlier submission — no "reuse the open conversation" logic exists in this story. This is a deliberate simplicity choice for the first pass (a marketing-site contact form is typically a one-shot submission, not a running thread); Stories 09-11's inbound-email/WhatsApp/SMS handlers use a different, "reuse the open conversation for that channel" rule instead (see their own Edge Cases) because those channels are inherently ongoing conversations.
- **Two web form submissions arrive concurrently with the exact same new email** — no unique constraint or transaction serializes this; both requests could each fail to find an existing customer and both insert a new `Customer` row with the same email, producing two customers with duplicate emails. This mirrors the existing, already-accepted risk in `CreateCustomerCommandHandler` (which has no uniqueness check on `Email` either) — not a new gap introduced by this story, but worth flagging together with it.
- **`PageNumber`/`PageSize` out of range on either list endpoint** — enforced by `GetConversationsListQueryValidator`/`GetConversationMessagesQueryValidator` via the existing `ValidationBehavior` pipeline, turned into a 400 before the handler runs.
- **`CreatedBy` on an inbound `Message`** (from `SubmitWebFormCommandHandler`, or any future inbound-webhook handler) **is stamped `Guid.Empty`**, not a real user id — `ApplicationDbContext.SaveChangesAsync` stamps `CreatedBy = _currentUserService.UserId ?? Guid.Empty`, and an anonymous request has no authenticated user. `MessageDto.CreatedBy` will surface as `00000000-0000-0000-0000-000000000000` for every inbound message; document this for frontend consumers rather than trying to special-case it, since `Direction == Inbound` already fully explains why.
- **`Microsoft.Extensions.Logging.Abstractions.NullLogger<T>` used in this story's tests (Task 5) may need an explicit `PackageReference`** if it doesn't resolve transitively through the Application project's `FrameworkReference` at `dotnet build` time — verify this the first time the test project builds; if it fails, add `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.3" />` to `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` (matching the `10.0.3` version already used by this codebase's other `Microsoft.Extensions.*`/`Microsoft.EntityFrameworkCore` packages).

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, following the existing `TestApplicationDbContext`/`StubCurrentUserService`/`StubLocalizationService` infrastructure (no mocking library referenced in this codebase).

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<Conversation> Conversations => Set<Conversation>();` and `public DbSet<Message> Messages => Set<Message>();`, and add `modelBuilder.Entity<Conversation>().HasQueryFilter(c => !c.IsDeleted);` / `modelBuilder.Entity<Message>().HasQueryFilter(m => !m.IsDeleted);` to `OnModelCreating`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/CreateConversationCommandHandlerTests.cs`** — `Create_persists_conversation_with_Open_status`; `Create_for_missing_customer_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/SendMessageCommandHandlerTests.cs`** — construct `SendMessageCommandHandler` with `NullLogger<SendMessageCommandHandler>.Instance` (from `Microsoft.Extensions.Logging.Abstractions`) for the logger parameter. Tests: `Send_persists_outbound_message_and_returns_success_when_no_sender_registered` (pass an empty `List<IChannelMessageSender>()`); `Send_for_missing_conversation_throws_NotFoundException`; `Send_returns_success_even_when_registered_sender_throws` — define a small private `ThrowingChannelMessageSender : IChannelMessageSender` in the test file itself (`Channel` set to match the test conversation's channel, `SendAsync` throws `InvalidOperationException`), assert the command still returns `IsSuccess == true` and the `Message` row is still persisted; `Send_invokes_matching_sender_only` — define a private `RecordingChannelMessageSender : IChannelMessageSender` that records whether `SendAsync` was called, register two of them (one matching the conversation's channel, one for a different channel), assert only the matching one was invoked.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/SubmitWebFormCommandHandlerTests.cs`** — `Submit_with_new_email_creates_customer_conversation_and_inbound_message`; `Submit_with_existing_email_reuses_customer_case_insensitively` (seed a `Customer` with `Email = "Jane@Example.com"`, submit with `"jane@example.com"`, assert exactly one `Customer` row exists afterward and the new `Conversation.CustomerId` matches the seeded customer); `Submit_always_creates_a_new_conversation_even_for_repeat_submitter` (submit twice with the same email, assert two `Conversation` rows exist).
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/GetConversationsListQueryHandlerTests.cs`** — `List_returns_paginated_results_ordered_by_CreatedOn_desc`; `List_filters_by_customerId_channel_and_status`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/GetConversationMessagesQueryHandlerTests.cs`** — `Messages_return_ordered_oldest_first` (seed three messages with distinct `CreatedOn` values out of chronological insertion order, assert the returned order is ascending by `CreatedOn`); `Messages_for_missing_conversation_throws_NotFoundException`.
7. **Create file: `tests/AzmCrm.Application.Tests/Features/Communications/SubmitWebFormCommandValidatorTests.cs`** — `Empty_Name_fails`; `Invalid_Email_fails`; `Empty_Body_fails`; `Invalid_Phone_fails_when_provided`; `Valid_command_with_no_phone_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

- The EF Core migration generated in Task 3 only **adds** the `Conversations` and `Messages` tables — additive, safe to apply on top of `20260828132833_AddTicketEscalation`.
- **Rollback**: `dotnet ef database update 20260828132833_AddTicketEscalation --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops both new tables. No other table has a foreign key into `Conversations`/`Messages` yet, so this is a clean rollback with no orphaned data.
- **Half-applied state**: same existing behavior as every prior migration — `DatabaseInitializer.InitializeAsync` logs and rethrows on failure (`src/AzmCrm.Infrastructure/Data/DatabaseInitializer.cs`), so the app fails to start rather than running against a partially-migrated schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` against a local Postgres instance (or let `dotnet run --project src/AzmCrm.API` apply it automatically on startup).
4. **Manual smoke test (agent-authored flow):** create a customer (KAN-1 Story 01), obtain a bearer token via `POST /api/identity/login`, then `POST /api/conversations` with `{"customerId":"<id>","channel":"Email","subject":"Order question"}`, confirm 201; `POST /api/conversations/{id}/messages` with `{"body":"Thanks for reaching out"}`, confirm 201; `GET /api/conversations/{id}/messages` returns it; `GET /api/conversations?channel=Email` returns the conversation filtered.
5. **Manual smoke test (anonymous web form flow):** without any bearer token, `POST /api/conversations/web-form` with `{"name":"Jane Doe","email":"jane@example.com","body":"I have a billing question"}`, confirm 201 and the response's `Location` header points at `/api/conversations/{id}`; `GET /api/customers?search=jane@example.com` (with a valid agent token) shows the auto-created customer; repeat the same submission and confirm a *second* new customer is **not** created (still just Jane Doe) but a *second* conversation is.

## Done Criteria

- [ ] `Conversation` and `Message` entities, EF configurations, and migration exist and `dotnet ef database update` applies cleanly.
- [ ] `POST /api/conversations`, `GET /api/conversations/{id}`, `GET /api/conversations`, `POST /api/conversations/{id}/messages`, `GET /api/conversations/{id}/messages` all work end-to-end against a real Postgres database, requiring authentication.
- [ ] `POST /api/conversations/web-form` works anonymously, auto-creating a `Customer` on first contact and reusing an existing one by email on repeat contact, satisfying "Accept submissions from web forms" completely.
- [ ] `GetConversationMessagesQueryHandler` returns messages oldest-first.
- [ ] A `SendMessage` call always persists the outbound message and returns success even if a (future) channel sender throws.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09.**
