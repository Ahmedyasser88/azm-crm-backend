namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single plain-text email. The Application layer
/// never touches SMTP directly — swap the Infrastructure-layer implementation (e.g. to a
/// transactional-email API) without changing <see cref="IChannelMessageSender"/> or any handler.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
