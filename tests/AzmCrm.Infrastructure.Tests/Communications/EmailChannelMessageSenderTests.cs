using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Infrastructure.Communications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzmCrm.Infrastructure.Tests.Communications;

public class EmailChannelMessageSenderTests
{
    private sealed class RecordingEmailSender : IEmailSender
    {
        public int CallCount { get; private set; }
        public string? LastToEmail { get; private set; }
        public string? LastSubject { get; private set; }
        public string? LastBody { get; private set; }

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
        {
            CallCount++;
            LastToEmail = toEmail;
            LastSubject = subject;
            LastBody = body;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendAsync_with_customer_email_calls_IEmailSender_with_expected_arguments()
    {
        var recordingSender = new RecordingEmailSender();
        var sender = new EmailChannelMessageSender(recordingSender, NullLogger<EmailChannelMessageSender>.Instance);

        var customer = new Customer { FullName = "Jane Doe", Email = "jane@example.com" };
        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.Email,
            Subject = "Order question",
            Customer = customer
        };
        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = "Thanks for reaching out"
        };

        await sender.SendAsync(conversation, message);

        Assert.Equal(1, recordingSender.CallCount);
        Assert.Equal("jane@example.com", recordingSender.LastToEmail);
        Assert.Equal("Order question", recordingSender.LastSubject);
        Assert.Equal("Thanks for reaching out", recordingSender.LastBody);
    }

    [Fact]
    public async Task SendAsync_with_no_customer_email_does_not_call_IEmailSender()
    {
        var recordingSender = new RecordingEmailSender();
        var sender = new EmailChannelMessageSender(recordingSender, NullLogger<EmailChannelMessageSender>.Instance);

        var customer = new Customer { FullName = "Jane Doe" };
        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = CommunicationChannel.Email,
            Customer = customer
        };
        var message = new Message
        {
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = "Thanks for reaching out"
        };

        await sender.SendAsync(conversation, message);

        Assert.Equal(0, recordingSender.CallCount);
    }
}
