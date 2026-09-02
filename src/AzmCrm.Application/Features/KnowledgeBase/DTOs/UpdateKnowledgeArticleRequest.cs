using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record UpdateKnowledgeArticleRequest(
    string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags);
