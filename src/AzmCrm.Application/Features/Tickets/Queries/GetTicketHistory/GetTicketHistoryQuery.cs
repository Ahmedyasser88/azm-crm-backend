using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketHistory;

public sealed record GetTicketHistoryQuery(
    Guid TicketId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<TicketHistoryDto>>>;
