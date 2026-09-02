using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.QuickReplies;

public sealed class QuickReplyTemplate : BaseEntity
{
    public required string Title { get; set; }
    public required string Body { get; set; }
}
