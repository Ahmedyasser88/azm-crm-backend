using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

public sealed record CreateCustomerInteractionCommand(
    Guid CustomerId,
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn
) : IRequest<Result<Guid>>;
