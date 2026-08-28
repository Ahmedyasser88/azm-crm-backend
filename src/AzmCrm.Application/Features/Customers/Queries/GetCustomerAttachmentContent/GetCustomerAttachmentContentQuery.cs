using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;

public sealed record GetCustomerAttachmentContentQuery(
    Guid CustomerId, Guid AttachmentId
) : IRequest<Result<CustomerAttachmentContentDto>>;
