namespace AzmCrm.Application.Features.Identity.DTOs;

public sealed record CurrentUserDto(
    Guid UserId,
    string FullName,
    string Username,
    string Email,
    string MobileNumber,
    IEnumerable<string> Roles,
    string AccessToken
);
