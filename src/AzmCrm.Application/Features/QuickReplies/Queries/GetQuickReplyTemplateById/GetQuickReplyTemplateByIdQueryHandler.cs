using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;

internal sealed class GetQuickReplyTemplateByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetQuickReplyTemplateByIdQuery, Result<QuickReplyTemplateDto>>
{
    public async Task<Result<QuickReplyTemplateDto>> Handle(
        GetQuickReplyTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        var dto = new QuickReplyTemplateDto(
            template.Id, template.Title, template.Body, template.CreatedOn, template.UpdatedOn);

        return Result<QuickReplyTemplateDto>.Success(dto);
    }
}
