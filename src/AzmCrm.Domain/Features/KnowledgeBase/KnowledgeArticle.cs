using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.KnowledgeBase;

public sealed class KnowledgeArticle : BaseEntity
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required KnowledgeArticleType Type { get; set; }
    public KnowledgeArticleStatus Status { get; set; } = KnowledgeArticleStatus.Draft;
    public string? Category { get; set; }
    public string? Tags { get; set; }

    // Stamped by Story 22's PublishKnowledgeArticleCommand/UnpublishKnowledgeArticleCommand;
    // both remain null for every article created by this story.
    public DateTime? PublishedOn { get; set; }
    public Guid? PublishedBy { get; set; }
}
