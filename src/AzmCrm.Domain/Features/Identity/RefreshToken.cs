namespace AzmCrm.Domain.Features.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Token { get; init; }
    public required Guid UserId { get; init; }
    public DateTime CreatedOn { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresOn { get; init; }
    public DateTime? RevokedOn { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; init; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsRevoked => RevokedOn.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public ApplicationUser User { get; init; } = null!;
}
