using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;

internal sealed class CreateSlaPolicyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateSlaPolicyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSlaPolicyCommand request, CancellationToken ct)
    {
        var alreadyExists = await dbContext.SlaPolicies
            .AnyAsync(p => p.Priority == request.Priority && p.IsActive, ct);
        if (alreadyExists)
            return Result<Guid>.Failure(
                $"An active SLA policy already exists for priority '{request.Priority}'.");

        var policy = new SlaPolicy
        {
            Name = request.Name,
            Priority = request.Priority,
            ResponseTimeMinutes = request.ResponseTimeMinutes,
            ResolutionTimeMinutes = request.ResolutionTimeMinutes
        };

        dbContext.SlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(policy.Id);
    }
}
