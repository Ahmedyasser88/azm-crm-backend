using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AzmCrm.Infrastructure.Communications;

internal sealed class WhatsAppCloudApiProvider(HttpClient httpClient, IOptions<WhatsAppSettings> settings)
    : IWhatsAppProvider
{
    private readonly WhatsAppSettings _settings = settings.Value;

    public async Task SendMessageAsync(string toPhoneNumber, string body, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiBaseUrl}/{_settings.PhoneNumberId}/messages";

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new { body }
        };

        var response = await httpClient.PostAsJsonAsync(url, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
