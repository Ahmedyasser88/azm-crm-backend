using AzmCrm.Domain.Features.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.ToTable("EscalationRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasIndex(r => new { r.IsActive, r.Priority });
    }
}
