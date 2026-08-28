using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    MessageDirection Direction,
    string Body,
    Guid CreatedBy,
    DateTime CreatedOn
);
