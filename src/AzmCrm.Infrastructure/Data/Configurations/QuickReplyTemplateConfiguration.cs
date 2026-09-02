using AzmCrm.Domain.Features.QuickReplies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class QuickReplyTemplateConfiguration : IEntityTypeConfiguration<QuickReplyTemplate>
{
    public void Configure(EntityTypeBuilder<QuickReplyTemplate> builder)
    {
        builder.ToTable("QuickReplyTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.Title);
    }
}
