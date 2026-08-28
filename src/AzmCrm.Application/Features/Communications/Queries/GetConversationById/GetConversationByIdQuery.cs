using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Communications.Queries.GetConversationById;

public sealed record GetConversationByIdQuery(Guid Id) : IRequest<Result<ConversationDto>>;
