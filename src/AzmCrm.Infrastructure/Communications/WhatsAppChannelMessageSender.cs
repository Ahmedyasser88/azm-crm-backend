using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class WhatsAppChannelMessageSender(
    IWhatsAppProvider provider,
    ILogger<WhatsAppChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.WhatsApp;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.PhoneNumber))
        {
            logger.LogWarning(
                "Skipped WhatsApp dispatch for message {MessageId}: customer {CustomerId} has no phone number on file",
                message.Id, conversation.CustomerId);
            return;
        }

        await provider.SendMessageAsync(conversation.Customer.PhoneNumber, message.Body, ct);
    }
}
