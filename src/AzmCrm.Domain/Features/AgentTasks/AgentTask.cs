using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.AgentTasks;

public sealed class AgentTask : BaseEntity
{
    public required Guid AssignedToUserId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueOn { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedOn { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TicketId { get; set; }
}
