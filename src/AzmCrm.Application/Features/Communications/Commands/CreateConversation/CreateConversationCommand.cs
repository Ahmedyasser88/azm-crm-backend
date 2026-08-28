using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

public sealed record CreateConversationCommand(
    Guid CustomerId,
    CommunicationChannel Channel,
    string? Subject
) : IRequest<Result<Guid>>;
