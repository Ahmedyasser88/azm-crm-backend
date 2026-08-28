namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for sending a single SMS text message via an HTTP-based SMS
/// gateway (e.g. Twilio). The Application layer never depends on a specific gateway's HTTP
/// contract directly.
/// </summary>
public interface ISmsProvider
{
    Task SendAsync(string toPhoneNumber, string body, CancellationToken ct = default);
}
