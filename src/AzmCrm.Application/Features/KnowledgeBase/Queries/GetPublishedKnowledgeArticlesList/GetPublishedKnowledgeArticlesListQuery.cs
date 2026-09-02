using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticlesList;

public sealed record GetPublishedKnowledgeArticlesListQuery(
    int PageNumber = 1, int PageSize = 20,
    KnowledgeArticleType? Type = null, string? Category = null
) : IRequest<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>;
