using AzmCrm.Domain.Features.AgentTasks;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("AgentTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.AssignedToUserId);
        builder.HasIndex(t => t.CustomerId);
        builder.HasIndex(t => t.TicketId);
        builder.HasIndex(t => t.DueOn);
    }
}
