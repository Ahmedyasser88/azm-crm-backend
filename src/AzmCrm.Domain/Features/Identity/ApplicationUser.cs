using Microsoft.AspNetCore.Identity;

namespace AzmCrm.Domain.Features.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "Unknown";
    public required string MobileNumber { get; set; }
    public DateTime CreatedOn { get; init; } = DateTime.UtcNow;
    public DateTime? LastLoginOn { get; set; }
    public bool IsActive { get; set; } = true;

    public List<RefreshToken> RefreshTokens { get; init; } = [];
}
