namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record EmailInboundWebhookRequest(
    string FromEmail,
    string? FromName,
    string? Subject,
    string Body,
    string? ExternalMessageId
);
