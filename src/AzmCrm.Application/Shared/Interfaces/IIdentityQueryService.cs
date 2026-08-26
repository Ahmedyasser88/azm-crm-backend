namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Read-only identity queries for use in Application-layer handlers.
/// Avoids coupling to ASP.NET Identity types in the Application project.
/// </summary>
public interface IIdentityQueryService
{
    /// <summary>Returns (FullName, Email) for a user ID, or (null, null) if not found.</summary>
    Task<(string? FullName, string? Email)> GetUserInfoAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Batch-resolves user IDs to (FullName, Email) pairs. Values are non-null for found users.</summary>
    Task<Dictionary<Guid, (string? FullName, string? Email)>> GetUsersInfoAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default);
}
