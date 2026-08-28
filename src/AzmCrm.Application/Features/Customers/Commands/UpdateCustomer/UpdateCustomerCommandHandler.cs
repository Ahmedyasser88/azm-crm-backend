using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.UpdateCustomer;

internal sealed class UpdateCustomerCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateCustomerCommand, Result>
{
    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");

        customer.FullName = request.FullName;
        customer.CompanyName = request.CompanyName;
        customer.Email = request.Email;
        customer.PhoneNumber = request.PhoneNumber;
        customer.AddressLine1 = request.AddressLine1;
        customer.AddressLine2 = request.AddressLine2;
        customer.City = request.City;
        customer.State = request.State;
        customer.PostalCode = request.PostalCode;
        customer.Country = request.Country;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
