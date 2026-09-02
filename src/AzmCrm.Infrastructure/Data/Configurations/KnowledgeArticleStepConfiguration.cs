using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class KnowledgeArticleStepConfiguration : IEntityTypeConfiguration<KnowledgeArticleStep>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleStep> builder)
    {
        builder.ToTable("KnowledgeArticleSteps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(s => s.KnowledgeArticle)
            .WithMany()
            .HasForeignKey(s => s.KnowledgeArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasIndex(s => s.KnowledgeArticleId);
    }
}
