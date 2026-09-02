using AzmCrm.Domain.Features.Sla;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record SlaBreachNotificationDto(
    Guid Id, Guid TicketId, SlaBreachType BreachType, Guid? NotifiedUserId,
    string? NotifiedUserName, string Message, bool EmailSent, DateTime CreatedOn);
