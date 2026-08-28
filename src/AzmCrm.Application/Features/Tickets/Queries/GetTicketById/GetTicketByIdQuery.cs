using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketById;

public sealed record GetTicketByIdQuery(Guid Id) : IRequest<Result<TicketDto>>;
