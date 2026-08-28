using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CreateInteractionRequest(
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn
);
