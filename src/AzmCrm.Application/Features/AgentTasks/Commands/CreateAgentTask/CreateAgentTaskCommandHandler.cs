using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.AgentTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;

internal sealed class CreateAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateAgentTaskCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAgentTaskCommand request, CancellationToken ct)
    {
        if (request.CustomerId is not null &&
            !await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        if (request.TicketId is not null &&
            !await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct))
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var task = new AgentTask
        {
            AssignedToUserId = currentUserService.UserId ?? Guid.Empty,
            Title = request.Title,
            Description = request.Description,
            DueOn = request.DueOn,
            CustomerId = request.CustomerId,
            TicketId = request.TicketId
        };

        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(task.Id);
    }
}
