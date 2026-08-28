using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Infrastructure.Communications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzmCrm.Infrastructure.Tests.Communications;

public class WhatsAppChannelMessageSenderTests
{
    private sealed class RecordingWhatsAppProvider : IWhatsAppProvider
    {
        public int CallCount { get; private set; }
        public string? LastPhoneNumber { get; private set; }
        public string? LastBody { get; private set; }

        public Task SendMessageAsync(string toPhoneNumber, string body, CancellationToken ct = default)
        {
            CallCount++;
            LastPhoneNumber = toPhoneNumber;
            LastBody = body;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendAsync_with_customer_phone_calls_provider_with_expected_arguments()
    {
        var recordingProvider = new RecordingWhatsAppProvider();
        var sender = new WhatsAppChannelMessageSender(recordingProvider, NullLogger<WhatsAppChannelMessageSender>.Instance);

        var customer = new Customer { FullName = "Jane Doe", PhoneNumber = "+966512345678" };
        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.WhatsApp,
            Customer = customer
        };
        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = "Your order has shipped"
        };

        await sender.SendAsync(conversation, message);

        Assert.Equal(1, recordingProvider.CallCount);
        Assert.Equal("+966512345678", recordingProvider.LastPhoneNumber);
        Assert.Equal("Your order has shipped", recordingProvider.LastBody);
    }

    [Fact]
    public async Task SendAsync_with_no_customer_phone_does_not_call_provider()
    {
        var recordingProvider = new RecordingWhatsAppProvider();
        var sender = new WhatsAppChannelMessageSender(recordingProvider, NullLogger<WhatsAppChannelMessageSender>.Instance);

        var customer = new Customer { FullName = "Jane Doe" };
        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.WhatsApp,
            Customer = customer
        };
        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = "Your order has shipped"
        };

        await sender.SendAsync(conversation, message);

        Assert.Equal(0, recordingProvider.CallCount);
    }
}
