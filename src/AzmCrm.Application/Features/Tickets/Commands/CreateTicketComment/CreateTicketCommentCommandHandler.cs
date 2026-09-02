using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;

internal sealed class CreateTicketCommentCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateTicketCommentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommentCommand request, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct);
        if (!ticketExists)
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var comment = new TicketComment
        {
            TicketId = request.TicketId,
            Content = request.Content
        };

        dbContext.TicketComments.Add(comment);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(comment.Id);
    }
}
