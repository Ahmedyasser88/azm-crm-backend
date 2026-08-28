using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Tests;

public sealed class TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    // Mirrors the soft-delete query filters from the Infrastructure-layer *Configuration classes
    // (not referenced here) so handler tests exercise the same "deleted rows are invisible by
    // default" contract the real ApplicationDbContext enforces via ApplyConfigurationsFromAssembly.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<CustomerInteraction>().HasQueryFilter(i => !i.IsDeleted);
        modelBuilder.Entity<CustomerNote>().HasQueryFilter(n => !n.IsDeleted);
        modelBuilder.Entity<CustomerAttachment>().HasQueryFilter(a => !a.IsDeleted);
    }

    public static TestApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
