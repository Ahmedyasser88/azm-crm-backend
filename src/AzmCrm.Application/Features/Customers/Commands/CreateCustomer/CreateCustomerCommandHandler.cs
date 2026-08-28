using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

internal sealed class CreateCustomerCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var customer = new Customer
        {
            FullName = request.FullName,
            CompanyName = request.CompanyName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(customer.Id);
    }
}
