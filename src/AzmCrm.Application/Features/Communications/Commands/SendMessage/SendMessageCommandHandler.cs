using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Communications.Commands.SendMessage;

internal sealed class SendMessageCommandHandler(
    IApplicationDbContext dbContext,
    IEnumerable<IChannelMessageSender> channelSenders,
    ILogger<SendMessageCommandHandler> logger)
    : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = request.Body
        };

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        // The message is already saved at this point — a delivery failure below must never
        // make this request look like it failed, since the agent's message genuinely was
        // recorded. See Story 08's Edge Cases for the reasoning.
        var sender = channelSenders.FirstOrDefault(s => s.Channel == conversation.Channel);
        if (sender is not null)
        {
            try
            {
                await sender.SendAsync(conversation, message, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to dispatch outbound message {MessageId} on channel {Channel}",
                    message.Id, conversation.Channel);
            }
        }

        var dto = new MessageDto(
            message.Id, message.ConversationId, message.Direction, message.Body,
            message.CreatedBy, message.CreatedOn);

        return Result<MessageDto>.Success(dto);
    }
}
