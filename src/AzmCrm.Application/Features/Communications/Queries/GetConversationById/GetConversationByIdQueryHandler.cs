using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationById;

internal sealed class GetConversationByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationByIdQuery, Result<ConversationDto>>
{
    public async Task<Result<ConversationDto>> Handle(GetConversationByIdQuery request, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Conversation '{request.Id}' was not found.");

        var dto = new ConversationDto(
            conversation.Id, conversation.CustomerId, conversation.Channel, conversation.Subject,
            conversation.Status, conversation.CreatedOn, conversation.UpdatedOn);

        return Result<ConversationDto>.Success(dto);
    }
}
