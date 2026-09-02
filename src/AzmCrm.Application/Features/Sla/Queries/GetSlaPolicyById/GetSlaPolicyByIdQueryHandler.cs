using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPolicyById;

internal sealed class GetSlaPolicyByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSlaPolicyByIdQuery, Result<SlaPolicyDto>>
{
    public async Task<Result<SlaPolicyDto>> Handle(GetSlaPolicyByIdQuery request, CancellationToken ct)
    {
        var policy = await dbContext.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA policy '{request.Id}' was not found.");

        var dto = new SlaPolicyDto(
            policy.Id, policy.Name, policy.Priority, policy.ResponseTimeMinutes,
            policy.ResolutionTimeMinutes, policy.IsActive, policy.CreatedOn, policy.UpdatedOn);

        return Result<SlaPolicyDto>.Success(dto);
    }
}
