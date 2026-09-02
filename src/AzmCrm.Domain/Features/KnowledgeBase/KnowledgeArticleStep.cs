using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.KnowledgeBase;

public sealed class KnowledgeArticleStep : BaseEntity
{
    public required Guid KnowledgeArticleId { get; init; }
    public required int StepNumber { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }

    public KnowledgeArticle KnowledgeArticle { get; init; } = null!;
}
