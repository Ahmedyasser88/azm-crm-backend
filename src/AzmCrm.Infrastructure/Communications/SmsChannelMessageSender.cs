using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class SmsChannelMessageSender(
    ISmsProvider provider,
    ILogger<SmsChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.Sms;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.PhoneNumber))
        {
            logger.LogWarning(
                "Skipped SMS dispatch for message {MessageId}: customer {CustomerId} has no phone number on file",
                message.Id, conversation.CustomerId);
            return;
        }

        await provider.SendAsync(conversation.Customer.PhoneNumber, message.Body, ct);
    }
}
