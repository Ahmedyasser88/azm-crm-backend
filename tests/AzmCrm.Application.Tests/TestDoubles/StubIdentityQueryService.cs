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

    public Task<List<(Guid Id, string FullName, string? Email)>> SearchAgentsAsync(
        string? search, int take, CancellationToken ct = default)
    {
        var matches = Users
            .Where(kv => string.IsNullOrWhiteSpace(search) ||
                         (kv.Value.FullName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(take)
            .Select(kv => (kv.Key, kv.Value.FullName ?? string.Empty, kv.Value.Email))
            .ToList();

        return Task.FromResult(matches);
    }
}
