using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class SmsGatewayProvider(HttpClient httpClient, IOptions<SmsSettings> settings) : ISmsProvider
{
    private readonly SmsSettings _settings = settings.Value;

    public async Task SendAsync(string toPhoneNumber, string body, CancellationToken ct = default)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var payload = new { from = _settings.SenderId, to = toPhoneNumber, body };

        var response = await httpClient.PostAsJsonAsync(_settings.ApiBaseUrl, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
