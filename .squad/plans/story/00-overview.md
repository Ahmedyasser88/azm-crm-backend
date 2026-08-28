# story — plan overview

Entry point for the **story** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) | Customer Profile CRUD & Contact Details | KAN-1 | — |
| 02 | [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) | Customer Interaction History | KAN-1 | 01 |
| 03 | [03-story-customer-notes-KAN-1.md](03-story-customer-notes-KAN-1.md) | Customer Notes | KAN-1 | 01 |
| 04 | [04-story-customer-attachments-KAN-1.md](04-story-customer-attachments-KAN-1.md) | Customer Attachments | KAN-1 | 01 |
| 05 | [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) | Ticket Core CRUD, Categorization & History | KAN-2 | 01 |
| 06 | [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) | Ticket Assignment to Agents | KAN-2 | 05 |
| 07 | [07-story-ticket-status-escalation-KAN-2.md](07-story-ticket-status-escalation-KAN-2.md) | Ticket Status Tracking & Escalation | KAN-2 | 05 |

## Dependency notes

All four stories implement KAN-1 ("Customer Management Module"), split by aggregate/sub-resource so each is independently reviewable and testable:

- **Story 01** creates the `Customer` entity, `IApplicationDbContext.Customers`, and `CustomersController` — every other story depends on it.
- **Stories 02-04** each add one child aggregate (`CustomerInteraction`, `CustomerNote`, `CustomerAttachment`) with its own `HasOne(...).WithMany()` FK relationship back to `Customer`. They only depend on Story 01, not on each other, but are numbered sequentially (02 → 03 → 04) because each edits the same shared files incrementally:
  - `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs` (one new `DbSet<T>` member per story)
  - `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs` (one new `DbSet<T>` property per story)
  - `src/AzmCrm.API/Controllers/CustomersController.cs` (new actions appended per story)
  - `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs` (one new `DbSet<T>` property per story, introduced in Story 01's Test Plan)

  Implementing them out of numeric order (e.g. 04 before 02) is possible but will produce a slightly different, still-correct, diff to the shared files above — the plans do not assume a specific merge order beyond "after Story 01".
- Story 02 additionally changes `services.AddControllers()` in `src/AzmCrm.API/Extensions/ApplicationExtensions.cs` to register a `JsonStringEnumConverter` (needed for `InteractionType`). This is a global JSON serialization change; Stories 03 and 04 don't depend on it but will inherit it once Story 02 lands.
- Story 04 introduces `IFileStorageService` (Application) / `LocalFileStorageService` (Infrastructure) and a new `App_Data/attachments`-rooted local disk store, plus a new `AzmCrm.Infrastructure.Tests` test project (none currently exists). No other story depends on this.
- **Out of scope across all four KAN-1 stories** (see each story's Story Goal for specifics): hard/permanent delete, customer merge/de-duplication, export, editing/deleting a note or interaction, attachment deletion/versioning, and any integration with KAN-2 (Ticket Management) or KAN-3 (Communication Channels) — those are separate, not-yet-planned Jira stories that may become additional sources of interaction records once built.

Stories 05-07 implement KAN-2 ("Ticket Management System"), split the same way as KAN-1 — a core-CRUD story others depend on, plus independent extension stories that each edit the core story's shared files incrementally:

- **Story 05** creates the `Ticket` and `TicketHistory` entities, `IApplicationDbContext.Tickets`/`TicketHistories`, and `TicketsController` (`api/tickets`) — every other KAN-2 story depends on it. It also covers three of KAN-2's five acceptance criteria in one pass (create/track, categories/priorities, and the history-viewing endpoint), since those map directly onto fields on the `Ticket` entity itself and a single child `TicketHistory` table, mirroring how KAN-1 Story 01 folded "contact details" into its core CRUD story rather than splitting it out.
- **Stories 06-07** each depend only on Story 05 (not on each other) but are numbered sequentially because both edit the same shared files incrementally: `Ticket.cs` (new properties), `TicketDto.cs`/`TicketListItemDto.cs` (new trailing DTO parameters), `GetTicketsListQuery.cs`/`GetTicketsListQueryHandler.cs` (new optional filter), `TicketConfiguration.cs` (new column/index config), and `TicketsController.cs` (new actions). Implementing them out of order produces a different, still-correct diff to those files.
  - Story 06 adds `Ticket.AssignedToUserId` and validates it via the existing `IIdentityQueryService` abstraction (`src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs`, already registered in DI but unused elsewhere in the codebase before this story) rather than exposing `ApplicationUser` through `IApplicationDbContext` — keeping Identity access behind that interface at the Application layer, while `TicketConfiguration` still declares a real DB-level FK to `ApplicationUser` since both live in the same `ApplicationDbContext`.
  - Story 07 adds `Ticket.IsEscalated`/`EscalatedOn` and two new commands (`ChangeTicketStatusCommand`, `EscalateTicketCommand`); it reuses `Ticket.Status` and the `StatusChanged`/`Escalated` members of `TicketHistoryEventType`, both already defined by Story 05.
  - Every mutating command across Stories 05-07 (create, update, assign, unassign, status change, escalate) writes to the shared `TicketHistory` table, so Story 05's `GET /api/tickets/{id}/history` endpoint needs no further changes to fully satisfy "View complete ticket history" once Stories 06-07 land.
- **Out of scope across all three KAN-2 stories**: ticket deletion, a formal status state machine/transition rules, de-escalation, enforcing that an assigned `ApplicationUser` is an active agent, and assignment/escalation notifications.
