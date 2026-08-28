using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

public sealed record StartLiveChatCommand(string Name, string Email, string Body) : IRequest<Result<Guid>>;
