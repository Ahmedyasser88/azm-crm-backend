using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticles");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Content)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Category)
            .HasMaxLength(100);

        builder.Property(a => a.Tags)
            .HasMaxLength(500);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => a.Type);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Category);
    }
}
