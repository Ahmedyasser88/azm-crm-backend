using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticleListItemDto(
    Guid Id, string Title, KnowledgeArticleType Type, KnowledgeArticleStatus Status,
    string? Category, DateTime CreatedOn);
