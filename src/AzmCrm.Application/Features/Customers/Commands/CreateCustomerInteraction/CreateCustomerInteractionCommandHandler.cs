using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

internal sealed class CreateCustomerInteractionCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerInteractionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerInteractionCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var interaction = new CustomerInteraction
        {
            CustomerId = request.CustomerId,
            Type = request.Type,
            Subject = request.Subject,
            Description = request.Description,
            OccurredOn = request.OccurredOn
        };

        dbContext.CustomerInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(interaction.Id);
    }
}
