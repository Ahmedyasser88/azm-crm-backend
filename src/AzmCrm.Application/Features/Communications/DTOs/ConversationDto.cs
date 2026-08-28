using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record ConversationDto(
    Guid Id,
    Guid CustomerId,
    CommunicationChannel Channel,
    string? Subject,
    ConversationStatus Status,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
