namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketAiSummaryDto(Guid TicketId, string Summary, DateTime GeneratedOn);
