using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;

internal sealed class DeleteSlaPolicyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteSlaPolicyCommand, Result>
{
    public async Task<Result> Handle(DeleteSlaPolicyCommand request, CancellationToken ct)
    {
        var policy = await dbContext.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA policy '{request.Id}' was not found.");

        policy.IsDeleted = true;
        policy.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        policy.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
