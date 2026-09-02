using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class SlaBreachNotificationConfiguration : IEntityTypeConfiguration<SlaBreachNotification>
{
    public void Configure(EntityTypeBuilder<SlaBreachNotification> builder)
    {
        builder.ToTable("SlaBreachNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.BreachType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.HasIndex(n => n.TicketId);
        builder.HasIndex(n => n.NotifiedUserId);
    }
}
