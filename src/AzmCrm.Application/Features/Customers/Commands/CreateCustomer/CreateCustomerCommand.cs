using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
) : IRequest<Result<Guid>>;
