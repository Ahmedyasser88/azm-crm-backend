using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;

public sealed record GetKnowledgeArticleByIdQuery(Guid Id) : IRequest<Result<KnowledgeArticleDto>>;
