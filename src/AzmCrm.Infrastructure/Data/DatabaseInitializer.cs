using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AzmCrm.Infrastructure.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

/// <summary>
/// Applies pending EF Core migrations on startup, then seeds baseline roles. In the
/// Development environment only, also seeds a login-ready test agent plus a handful of
/// dummy customers (with interactions, notes, and attachments) so every Customer API can
/// be exercised manually without hand-crafting data first. Both seeding steps are
/// idempotent — safe to run on every restart.
/// </summary>
internal sealed class DatabaseInitializer(
    ApplicationDbContext context,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IFileStorageService fileStorage,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    private const string SeedUsername = "testagent";
    private const string SeedPassword = "Test@1234";

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting database migration...");
            await context.Database.MigrateAsync(ct);
            logger.LogInformation("Database migration completed");

            await SeedRolesAsync();

            if (environment.IsDevelopment())
            {
                await SeedDevelopmentDataAsync(ct);
            }

            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database initialization - {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] requiredRoles = ["Admin", "User"];
        foreach (var role in requiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
                logger.LogInformation("Seeded role: {Role}", role);
            }
        }
    }

    /// <summary>
    /// Dev-only convenience data: one login-ready agent and a handful of customers, each
    /// with interactions, notes, and one downloadable attachment, so a fresh local database
    /// is immediately useful for exercising every Customer endpoint by hand (Swagger/Postman)
    /// without registering a user or creating records first.
    /// </summary>
    private async Task SeedDevelopmentDataAsync(CancellationToken ct)
    {
        var seedUser = await SeedTestAgentAsync();

        // Guard against reseeding: if any customer already exists (including soft-deleted
        // ones), assume this database was already seeded and skip re-inserting sample data.
        if (await context.Customers.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Seeding dummy customer data...");

        var customers = new[]
        {
            new Customer
            {
                FullName = "Ahmed Al-Fahad",
                CompanyName = "Al-Fahad Trading Co.",
                Email = "ahmed.alfahad@example.com",
                PhoneNumber = "0501234567",
                AddressLine1 = "King Fahd Road, Building 12",
                City = "Riyadh",
                State = "Riyadh Province",
                PostalCode = "12345",
                Country = "SA"
            },
            new Customer
            {
                FullName = "Sara Al-Otaibi",
                Email = "sara.alotaibi@example.com",
                PhoneNumber = "0559876543",
                City = "Jeddah",
                Country = "SA"
            },
            new Customer
            {
                FullName = "Fatima Al-Zahrani",
                CompanyName = "Zahrani Consulting",
                Email = "fatima.zahrani@example.com",
                PhoneNumber = "0541122334",
                AddressLine1 = "Prince Sultan Street",
                AddressLine2 = "Office 7",
                City = "Dammam",
                State = "Eastern Province",
                PostalCode = "31411",
                Country = "SA"
            },
            new Customer
            {
                // No email/phone on purpose — exercises the optional-contact-details path.
                FullName = "Omar Al-Harbi",
                CompanyName = "Al-Harbi Logistics"
            },
            new Customer
            {
                FullName = "Mona Al-Qahtani",
                Email = "mona.qahtani@example.com",
                PhoneNumber = "+966512345678",
                City = "Mecca",
                Country = "SA"
            }
        };

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(ct);

        var interactionTypes = Enum.GetValues<InteractionType>();
        var random = new Random(Seed: 42); // deterministic across restarts, purely cosmetic

        foreach (var (customer, index) in customers.Select((c, i) => (c, i)))
        {
            context.CustomerInteractions.AddRange(
                new CustomerInteraction
                {
                    CustomerId = customer.Id,
                    Type = interactionTypes[index % interactionTypes.Length],
                    Subject = "Initial outreach",
                    Description = $"First contact with {customer.FullName} to introduce our services.",
                    OccurredOn = DateTime.UtcNow.AddDays(-10 - index)
                },
                new CustomerInteraction
                {
                    CustomerId = customer.Id,
                    Type = interactionTypes[(index + 1) % interactionTypes.Length],
                    Subject = "Follow-up call",
                    Description = "Discussed pricing and next steps.",
                    OccurredOn = DateTime.UtcNow.AddDays(-2 - index)
                });

            context.CustomerNotes.Add(new CustomerNote
            {
                CustomerId = customer.Id,
                Content = $"{customer.FullName} prefers to be contacted in the morning."
            });

            var fileContent = Encoding.UTF8.GetBytes(
                $"Welcome letter for {customer.FullName}\nGenerated by dummy data seeder.\n");
            var storageKey = await fileStorage.SaveAsync(
                new MemoryStream(fileContent), "welcome-letter.txt", ct);

            context.CustomerAttachments.Add(new CustomerAttachment
            {
                CustomerId = customer.Id,
                FileName = "welcome-letter.txt",
                ContentType = "text/plain",
                FileSizeBytes = fileContent.Length,
                StorageKey = storageKey
            });
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {Count} dummy customers with interactions, notes, and attachments", customers.Length);
    }

    private async Task<ApplicationUser> SeedTestAgentAsync()
    {
        var existing = await userManager.FindByNameAsync(SeedUsername);
        if (existing != null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = SeedUsername,
            FullName = "Test Agent",
            Email = "testagent@azm-crm.test",
            MobileNumber = "0500000000",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, SeedPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Failed to seed test agent user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            throw new InvalidOperationException(
                $"Failed to seed test agent user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, "User");

        logger.LogInformation(
            "Seeded test agent — username: '{Username}', password: '{Password}'", SeedUsername, SeedPassword);

        return user;
    }
}
