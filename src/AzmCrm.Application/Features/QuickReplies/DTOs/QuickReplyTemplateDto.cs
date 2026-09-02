namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record QuickReplyTemplateDto(
    Guid Id, string Title, string Body, DateTime CreatedOn, DateTime? UpdatedOn);
