namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketCommentDto(
    Guid Id,
    Guid TicketId,
    string Content,
    Guid CreatedBy,
    string? CreatedByName,
    DateTime CreatedOn
);
