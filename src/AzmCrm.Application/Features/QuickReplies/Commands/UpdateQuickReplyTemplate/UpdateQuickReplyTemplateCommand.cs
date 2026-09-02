using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;

public sealed record UpdateQuickReplyTemplateCommand(Guid Id, string Title, string Body) : IRequest<Result>;
