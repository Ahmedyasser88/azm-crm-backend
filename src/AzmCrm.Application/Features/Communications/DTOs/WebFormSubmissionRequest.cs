namespace AzmCrm.Application.Features.Communications.DTOs;

public sealed record WebFormSubmissionRequest(string Name, string Email, string? Phone, string? Subject, string Body);
