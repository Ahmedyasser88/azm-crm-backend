using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestTicketReply;

public sealed record SuggestTicketReplyQuery(Guid TicketId) : IRequest<Result<TicketReplySuggestionDto>>;
