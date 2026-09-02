using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;

internal sealed class UpdateSlaPolicyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateSlaPolicyCommand, Result>
{
    public async Task<Result> Handle(UpdateSlaPolicyCommand request, CancellationToken ct)
    {
        var policy = await dbContext.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA policy '{request.Id}' was not found.");

        if (request.IsActive)
        {
            var conflicts = await dbContext.SlaPolicies
                .AnyAsync(p => p.Id != request.Id && p.Priority == request.Priority && p.IsActive, ct);
            if (conflicts)
                return Result.Failure(
                    $"An active SLA policy already exists for priority '{request.Priority}'.");
        }

        policy.Name = request.Name;
        policy.Priority = request.Priority;
        policy.ResponseTimeMinutes = request.ResponseTimeMinutes;
        policy.ResolutionTimeMinutes = request.ResolutionTimeMinutes;
        policy.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
