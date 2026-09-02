using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;

public sealed record GetQuickReplyTemplatesListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<Result<PaginatedResult<QuickReplyTemplateListItemDto>>>;
