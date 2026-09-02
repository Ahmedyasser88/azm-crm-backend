using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticleDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type, KnowledgeArticleStatus Status,
    string? Category, string? Tags, DateTime? PublishedOn, Guid? PublishedBy,
    DateTime CreatedOn, DateTime? UpdatedOn, IReadOnlyList<KnowledgeArticleStepDto> Steps);
