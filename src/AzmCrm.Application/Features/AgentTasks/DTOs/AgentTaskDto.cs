namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record AgentTaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueOn,
    bool IsCompleted,
    DateTime? CompletedOn,
    Guid? CustomerId,
    Guid? TicketId,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
