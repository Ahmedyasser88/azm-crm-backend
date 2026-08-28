using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationsList;

public sealed record GetConversationsListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    CommunicationChannel? Channel = null,
    ConversationStatus? Status = null
) : IRequest<Result<PaginatedResult<ConversationListItemDto>>>;
