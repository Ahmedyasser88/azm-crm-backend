using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;

public sealed record GetPublishedKnowledgeArticleByIdQuery(Guid Id) : IRequest<Result<KnowledgeArticlePublicDto>>;
