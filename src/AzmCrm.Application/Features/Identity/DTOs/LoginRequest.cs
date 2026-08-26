namespace AzmCrm.Application.Features.Identity.DTOs;

public sealed record LoginRequest(
    string UsernameOrEmail,
    string Password
);
