using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;

public sealed record CreateTicketCommentCommand(Guid TicketId, string Content) : IRequest<Result<Guid>>;
