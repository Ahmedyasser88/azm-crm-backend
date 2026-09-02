using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPolicyById;

public sealed record GetSlaPolicyByIdQuery(Guid Id) : IRequest<Result<SlaPolicyDto>>;
