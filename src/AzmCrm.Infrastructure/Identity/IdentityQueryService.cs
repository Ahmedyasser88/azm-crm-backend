namespace AzmCrm.Infrastructure.Identity;

using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal sealed class IdentityQueryService(UserManager<ApplicationUser> userManager)
    : IIdentityQueryService
{
    public async Task<(string? FullName, string? Email)> GetUserInfoAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user is null ? (null, null) : (user.FullName, user.Email);
    }

    public async Task<Dictionary<Guid, (string? FullName, string? Email)>> GetUsersInfoAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var users = await userManager.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync(ct);

        return users.ToDictionary(
            u => u.Id,
            u => (u.FullName, u.Email ?? string.Empty));
    }
}
