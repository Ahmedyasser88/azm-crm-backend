using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.QuickReplies;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

internal sealed class CreateQuickReplyTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateQuickReplyTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = new QuickReplyTemplate
        {
            Title = request.Title,
            Body = request.Body
        };

        dbContext.QuickReplyTemplates.Add(template);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(template.Id);
    }
}
