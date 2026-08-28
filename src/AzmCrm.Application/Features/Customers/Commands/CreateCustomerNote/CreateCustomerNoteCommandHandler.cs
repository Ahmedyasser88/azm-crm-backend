using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

internal sealed class CreateCustomerNoteCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerNoteCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerNoteCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var note = new CustomerNote
        {
            CustomerId = request.CustomerId,
            Content = request.Content
        };

        dbContext.CustomerNotes.Add(note);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(note.Id);
    }
}
