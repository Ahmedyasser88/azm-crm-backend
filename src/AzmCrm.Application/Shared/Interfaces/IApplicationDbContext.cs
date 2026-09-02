using AzmCrm.Domain.Features.AgentTasks;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.QuickReplies;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Application-layer abstraction over the EF Core DbContext. Add DbSet properties here
/// for each new CRM aggregate (Customers, Leads, Deals, ...) as they're introduced.
/// </summary>
public interface IApplicationDbContext
{
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerInteraction> CustomerInteractions { get; }
    DbSet<CustomerNote> CustomerNotes { get; }
    DbSet<CustomerAttachment> CustomerAttachments { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketHistory> TicketHistories { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<AgentTask> AgentTasks { get; }
    DbSet<QuickReplyTemplate> QuickReplyTemplates { get; }
    DbSet<SlaPolicy> SlaPolicies { get; }
    DbSet<SlaBreachNotification> SlaBreachNotifications { get; }
    DbSet<AssignmentRule> AssignmentRules { get; }
    DbSet<EscalationRule> EscalationRules { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
