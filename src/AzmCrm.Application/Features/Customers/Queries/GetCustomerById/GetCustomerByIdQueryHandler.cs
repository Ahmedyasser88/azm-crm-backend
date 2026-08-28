using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerById;

internal sealed class GetCustomerByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");

        var dto = new CustomerDto(
            customer.Id, customer.FullName, customer.CompanyName, customer.Email, customer.PhoneNumber,
            customer.AddressLine1, customer.AddressLine2, customer.City, customer.State,
            customer.PostalCode, customer.Country, customer.CreatedOn, customer.UpdatedOn);

        return Result<CustomerDto>.Success(dto);
    }
}
