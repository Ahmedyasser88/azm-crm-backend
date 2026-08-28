using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;

public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<MessageDto>>>;
