using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;

public sealed record SearchKnowledgeArticlesQuery(
    string Query, int PageNumber = 1, int PageSize = 20
) : IRequest<Result<PaginatedResult<KnowledgeArticlePublicListItemDto>>>;
