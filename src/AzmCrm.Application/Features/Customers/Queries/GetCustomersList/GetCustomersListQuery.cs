using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomersList;

public sealed record GetCustomersListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<Result<PaginatedResult<CustomerListItemDto>>>;
