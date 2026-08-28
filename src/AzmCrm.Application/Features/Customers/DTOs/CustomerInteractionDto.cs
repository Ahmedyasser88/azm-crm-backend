using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerInteractionDto(
    Guid Id,
    Guid CustomerId,
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn,
    DateTime CreatedOn
);
