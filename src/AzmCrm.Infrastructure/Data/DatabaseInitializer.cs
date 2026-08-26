using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

/// <summary>
/// Applies pending EF Core migrations on startup. Intentionally does not seed any
/// business data — a fresh CRM database starts empty.
/// </summary>
internal sealed class DatabaseInitializer(
    ApplicationDbContext context,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting database migration...");
            await context.Database.MigrateAsync(ct);
            logger.LogInformation("Database migration completed");

            await SeedRolesAsync();

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
}
