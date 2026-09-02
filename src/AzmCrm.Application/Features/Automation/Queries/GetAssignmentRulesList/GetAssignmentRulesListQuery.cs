using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Queries.GetAssignmentRulesList;

public sealed record GetAssignmentRulesListQuery(
    int PageNumber = 1, int PageSize = 20,
    TicketCategory? Category = null, TicketPriority? Priority = null, bool? IsActive = null
) : IRequest<Result<PaginatedResult<AssignmentRuleListItemDto>>>;
