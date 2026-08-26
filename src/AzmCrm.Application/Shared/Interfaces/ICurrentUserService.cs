namespace AzmCrm.Application.Shared.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? Email { get; }
    string? MobileNumber { get; }
    string? UserType { get; }
    IEnumerable<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
}
