using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Communications;

public sealed class Message : BaseEntity
{
    public required Guid ConversationId { get; init; }
    public required MessageDirection Direction { get; init; }
    public required string Body { get; set; }
    public string? ExternalMessageId { get; set; }

    public Conversation Conversation { get; init; } = null!;
}
