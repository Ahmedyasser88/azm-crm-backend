using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetConversationMessagesQuery, Result<PaginatedResult<MessageDto>>>
{
    public async Task<Result<PaginatedResult<MessageDto>>> Handle(
        GetConversationMessagesQuery request, CancellationToken ct)
    {
        var conversationExists = await dbContext.Conversations.AnyAsync(c => c.Id == request.ConversationId, ct);
        if (!conversationExists)
            throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var query = dbContext.Messages.Where(m => m.ConversationId == request.ConversationId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.CreatedOn) // oldest first — chat-thread reading order, see Story 08's Goal
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.Direction, m.Body, m.CreatedBy, m.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<MessageDto>>.Success(result);
    }
}
