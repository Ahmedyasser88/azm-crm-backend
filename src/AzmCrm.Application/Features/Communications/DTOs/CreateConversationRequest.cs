using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record CreateConversationRequest(Guid CustomerId, CommunicationChannel Channel, string? Subject);
