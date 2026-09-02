using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Queries.GetEscalationRulesList;

public sealed record GetEscalationRulesListQuery(
    int PageNumber = 1, int PageSize = 20, TicketPriority? Priority = null, bool? IsActive = null
) : IRequest<Result<PaginatedResult<EscalationRuleListItemDto>>>;
