using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;

public sealed record GetMyTicketsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    TicketStatus? Status = null
) : IRequest<Result<PaginatedResult<DashboardTicketDto>>>;
