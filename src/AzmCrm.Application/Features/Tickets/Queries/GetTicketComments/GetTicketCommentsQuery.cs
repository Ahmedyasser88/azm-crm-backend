using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;

public sealed record GetTicketCommentsQuery(
    Guid TicketId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<TicketCommentDto>>>;
