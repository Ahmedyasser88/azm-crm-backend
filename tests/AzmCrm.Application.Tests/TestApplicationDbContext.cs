using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.AgentTasks;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Domain.Features.QuickReplies;
using AzmCrm.Domain.Features.Tickets;
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
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();

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
        modelBuilder.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<TicketHistory>().HasQueryFilter(h => !h.IsDeleted);
        modelBuilder.Entity<TicketComment>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Conversation>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Message>().HasQueryFilter(m => !m.IsDeleted);
        modelBuilder.Entity<AgentTask>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<QuickReplyTemplate>().HasQueryFilter(t => !t.IsDeleted);
    }

    public static TestApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
