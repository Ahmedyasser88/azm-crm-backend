using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPoliciesList;

public sealed record GetSlaPoliciesListQuery(
    int PageNumber = 1, int PageSize = 20, TicketPriority? Priority = null, bool? IsActive = null
) : IRequest<Result<PaginatedResult<SlaPolicyListItemDto>>>;
