using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Automation;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

internal sealed class CreateEscalationRuleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateEscalationRuleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateEscalationRuleCommand request, CancellationToken ct)
    {
        var rule = new EscalationRule
        {
            Name = request.Name,
            Priority = request.Priority,
            OverdueMinutes = request.OverdueMinutes
        };

        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(rule.Id);
    }
}
