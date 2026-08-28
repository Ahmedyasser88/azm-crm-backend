using AzmCrm.Application.Features.Communications.Commands.SendMessage;
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.API.Hubs;

/// <summary>
/// No <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/> at the class level —
/// both an anonymous customer widget and an authenticated agent connect to this same hub. A
/// conversation's own Guid id is the group key and the widget's de facto access credential (see
/// Story 12's Goal). Every method below is restricted to the LiveChat channel specifically —
/// knowing any other channel's conversation id must never grant hub access to it.
/// </summary>
public sealed class ChatHub(IMediator mediator, IApplicationDbContext dbContext) : Hub
{
    public async Task JoinConversation(Guid conversationId)
    {
        await GetLiveChatConversationOrThrowAsync(conversationId);

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task SendMessage(Guid conversationId, string body)
    {
        await GetLiveChatConversationOrThrowAsync(conversationId);

        MessageDto dto;

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            try
            {
                var result = await mediator.Send(new SendMessageCommand(conversationId, body));
                if (!result.IsSuccess || result.Data is null)
                    throw new HubException(string.Join(" ", result.Errors));

                dto = result.Data;
            }
            catch (NotFoundException ex)
            {
                throw new HubException(ex.Message);
            }
        }
        else
        {
            var message = new Message
            {
                ConversationId = conversationId,
                Direction = MessageDirection.Inbound,
                Body = body
            };
            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();

            dto = new MessageDto(
                message.Id, conversationId, MessageDirection.Inbound, body, message.CreatedBy, message.CreatedOn);
        }

        await Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", dto);
    }

    private async Task<Conversation> GetLiveChatConversationOrThrowAsync(Guid conversationId)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation is null)
            throw new HubException($"Conversation '{conversationId}' was not found.");

        if (conversation.Channel != CommunicationChannel.LiveChat)
            throw new HubException($"Conversation '{conversationId}' is not a LiveChat conversation.");

        return conversation;
    }
}
