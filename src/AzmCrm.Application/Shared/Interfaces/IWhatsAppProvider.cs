namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single WhatsApp text message. Shaped around the
/// Meta WhatsApp Cloud API (the most common way to integrate WhatsApp without an on-premise
/// client), but the Application layer never depends on Meta's SDK/HTTP contract directly.
/// </summary>
public interface IWhatsAppProvider
{
    Task SendMessageAsync(string toPhoneNumber, string body, CancellationToken ct = default);
}
