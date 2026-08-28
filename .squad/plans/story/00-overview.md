# story — plan overview

Entry point for the **story** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) | Customer Profile CRUD & Contact Details | KAN-1 | — |
| 02 | [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) | Customer Interaction History | KAN-1 | 01 |
| 03 | [03-story-customer-notes-KAN-1.md](03-story-customer-notes-KAN-1.md) | Customer Notes | KAN-1 | 01 |
| 04 | [04-story-customer-attachments-KAN-1.md](04-story-customer-attachments-KAN-1.md) | Customer Attachments | KAN-1 | 01 |

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
- **Out of scope across all four stories** (see each story's Story Goal for specifics): hard/permanent delete, customer merge/de-duplication, export, editing/deleting a note or interaction, attachment deletion/versioning, and any integration with KAN-2 (Ticket Management) or KAN-3 (Communication Channels) — those are separate, not-yet-planned Jira stories that may become additional sources of interaction records once built.
