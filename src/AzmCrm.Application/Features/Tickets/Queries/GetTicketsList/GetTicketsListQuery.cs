using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketsList;

public sealed record GetTicketsListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    TicketStatus? Status = null,
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    string? Search = null,
    Guid? AssignedToUserId = null,
    bool? IsEscalated = null
) : IRequest<Result<PaginatedResult<TicketListItemDto>>>;
