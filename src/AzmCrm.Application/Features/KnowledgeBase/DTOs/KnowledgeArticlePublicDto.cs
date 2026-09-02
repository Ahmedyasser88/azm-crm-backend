using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticlePublicDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type,
    string? Category, string? Tags, DateTime? PublishedOn, IReadOnlyList<KnowledgeArticleStepDto> Steps);
