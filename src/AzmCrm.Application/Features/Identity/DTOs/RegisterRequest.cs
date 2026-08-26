namespace AzmCrm.Application.Features.Identity.DTOs;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string MobileNumber,
    string Password,
    string ConfirmPassword
);
