using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;

public sealed record PublishKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
