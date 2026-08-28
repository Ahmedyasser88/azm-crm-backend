namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record WhatsAppInboundWebhookRequest(
    string FromPhoneNumber,
    string Body,
    string? ExternalMessageId
);
