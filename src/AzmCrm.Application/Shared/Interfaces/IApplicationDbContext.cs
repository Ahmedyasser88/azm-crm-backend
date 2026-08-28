using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
