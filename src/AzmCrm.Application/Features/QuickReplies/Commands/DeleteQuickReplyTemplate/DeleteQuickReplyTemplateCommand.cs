using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;

public sealed record DeleteQuickReplyTemplateCommand(Guid Id) : IRequest<Result>;
