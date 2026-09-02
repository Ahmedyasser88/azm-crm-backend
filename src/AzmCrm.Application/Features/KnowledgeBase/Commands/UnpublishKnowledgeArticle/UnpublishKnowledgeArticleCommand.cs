using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;

public sealed record UnpublishKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
