using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;

public sealed record DeleteKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
