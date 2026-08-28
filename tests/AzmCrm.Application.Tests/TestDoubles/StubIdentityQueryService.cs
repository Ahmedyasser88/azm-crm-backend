using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

/// <summary>Hand-written <see cref="IIdentityQueryService"/> stub for handler tests.</summary>
public sealed class StubIdentityQueryService : IIdentityQueryService
{
    public Dictionary<Guid, (string? FullName, string? Email)> Users { get; } = [];

    public Task<(string? FullName, string? Email)> GetUserInfoAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Users.TryGetValue(userId, out var info) ? info : (null, null));

    public Task<Dictionary<Guid, (string? FullName, string? Email)>> GetUsersInfoAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult(userIds.Where(Users.ContainsKey).ToDictionary(id => id, id => Users[id]));
}
