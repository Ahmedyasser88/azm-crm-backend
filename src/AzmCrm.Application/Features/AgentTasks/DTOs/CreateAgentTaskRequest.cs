namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record CreateAgentTaskRequest(
    string Title, string? Description, DateTime? DueOn, Guid? CustomerId, Guid? TicketId);
