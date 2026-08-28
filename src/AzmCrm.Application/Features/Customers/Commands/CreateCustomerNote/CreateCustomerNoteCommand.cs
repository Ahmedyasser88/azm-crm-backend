using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

public sealed record CreateCustomerNoteCommand(Guid CustomerId, string Content) : IRequest<Result<Guid>>;
