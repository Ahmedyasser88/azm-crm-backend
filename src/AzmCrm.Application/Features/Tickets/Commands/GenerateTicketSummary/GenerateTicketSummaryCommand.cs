using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

public sealed record GenerateTicketSummaryCommand(Guid TicketId) : IRequest<Result<TicketAiSummaryDto>>;
