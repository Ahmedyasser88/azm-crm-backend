using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;

public sealed record GetQuickReplyTemplateByIdQuery(Guid Id) : IRequest<Result<QuickReplyTemplateDto>>;
