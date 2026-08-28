namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record SmsInboundWebhookRequest(string From, string Body, string? MessageSid);
