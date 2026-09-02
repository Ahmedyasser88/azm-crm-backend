using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

public sealed record CreateQuickReplyTemplateCommand(string Title, string Body) : IRequest<Result<Guid>>;
