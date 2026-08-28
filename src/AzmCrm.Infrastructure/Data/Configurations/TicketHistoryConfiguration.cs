using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedNever();

        builder.Property(h => h.EventType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(h => h.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.OldValue)
            .HasMaxLength(500);

        builder.Property(h => h.NewValue)
            .HasMaxLength(500);

        builder.HasOne(h => h.Ticket)
            .WithMany()
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(h => !h.IsDeleted);

        builder.HasIndex(h => h.TicketId);
    }
}
