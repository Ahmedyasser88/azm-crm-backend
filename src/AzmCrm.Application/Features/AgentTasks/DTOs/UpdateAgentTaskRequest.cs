namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record UpdateAgentTaskRequest(string Title, string? Description, DateTime? DueOn);
