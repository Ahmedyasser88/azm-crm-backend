using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;

public sealed record GetCustomerInteractionsQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<CustomerInteractionDto>>>;
