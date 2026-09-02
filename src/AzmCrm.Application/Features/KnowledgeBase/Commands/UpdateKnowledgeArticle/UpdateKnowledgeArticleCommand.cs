using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;

public sealed record UpdateKnowledgeArticleCommand(
    Guid Id, string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags)
    : IRequest<Result>;
