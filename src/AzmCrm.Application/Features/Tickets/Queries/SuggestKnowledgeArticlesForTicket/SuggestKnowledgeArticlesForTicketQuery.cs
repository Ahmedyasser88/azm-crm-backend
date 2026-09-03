using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

public sealed record SuggestKnowledgeArticlesForTicketQuery(Guid TicketId, int MaxResults = 5)
    : IRequest<Result<IReadOnlyList<KnowledgeArticlePublicListItemDto>>>;
