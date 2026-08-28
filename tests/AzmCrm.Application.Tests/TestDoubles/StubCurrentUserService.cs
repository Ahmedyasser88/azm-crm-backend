using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

/// <summary>Hand-written <see cref="ICurrentUserService"/> stub for handler tests.</summary>
public sealed class StubCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string? Username { get; init; } = "test-user";
    public string? Email { get; init; } = "test-user@azm.com.sa";
    public string? MobileNumber { get; init; }
    public string? UserType { get; init; }
    public IEnumerable<string> Roles { get; init; } = [];
    public bool IsAuthenticated { get; init; } = true;
    public string? AccessToken { get; init; }
}
