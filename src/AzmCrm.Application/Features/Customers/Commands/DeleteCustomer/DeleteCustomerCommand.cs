using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Result>;
