using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;

public sealed record DeleteSlaPolicyCommand(Guid Id) : IRequest<Result>;
