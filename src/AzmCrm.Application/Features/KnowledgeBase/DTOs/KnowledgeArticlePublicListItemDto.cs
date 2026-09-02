using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticlePublicListItemDto(
    Guid Id, string Title, KnowledgeArticleType Type, string? Category, DateTime? PublishedOn);
