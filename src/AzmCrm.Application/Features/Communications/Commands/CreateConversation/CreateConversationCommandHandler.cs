using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

internal sealed class CreateConversationCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateConversationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateConversationCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var conversation = new Conversation
        {
            CustomerId = request.CustomerId,
            Channel = request.Channel,
            Subject = request.Subject
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(conversation.Id);
    }
}
