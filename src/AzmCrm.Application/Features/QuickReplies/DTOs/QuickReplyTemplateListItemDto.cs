namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record QuickReplyTemplateListItemDto(Guid Id, string Title, string Body, DateTime CreatedOn);
