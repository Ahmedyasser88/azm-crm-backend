using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;

public sealed record CreateKnowledgeArticleCommand(
    string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags)
    : IRequest<Result<Guid>>;
