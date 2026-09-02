using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

public sealed class StubEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> SentEmails { get; } = [];
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        if (ThrowOnSend)
            throw new InvalidOperationException("Simulated SMTP failure.");

        SentEmails.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}
