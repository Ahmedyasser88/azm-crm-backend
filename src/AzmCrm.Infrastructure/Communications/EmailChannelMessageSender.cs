using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class EmailChannelMessageSender(
    IEmailSender emailSender,
    ILogger<EmailChannelMessageSender> logger) : IChannelMessageSender
{
    public CommunicationChannel Channel => CommunicationChannel.Email;

    public async Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.Customer.Email))
        {
            logger.LogWarning(
                "Skipped email dispatch for message {MessageId}: customer {CustomerId} has no email on file",
                message.Id, conversation.CustomerId);
            return;
        }

        var subject = conversation.Subject ?? "Re: your support request";
        await emailSender.SendAsync(conversation.Customer.Email, subject, message.Body, ct);
    }
}
