using AzmCrm.Domain.Features.Communications;

namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Channel-specific outbound dispatch for an already-persisted outbound <see cref="Message"/>.
/// This story registers zero implementations — <c>SendMessageCommandHandler</c> resolves every
/// registered sender and, if one's <see cref="Channel"/> matches the conversation's channel,
/// calls it. Stories 09-11 (email, WhatsApp, SMS) each add exactly one new Infrastructure-layer
/// implementation and one new DI registration; none of them need to edit this interface or
/// SendMessageCommandHandler. LiveChat never gets an implementation of this interface — Story 12
/// delivers live-chat messages via a SignalR hub instead, not a request/response send.
/// </summary>
public interface IChannelMessageSender
{
    CommunicationChannel Channel { get; }
    Task SendAsync(Conversation conversation, Message message, CancellationToken ct = default);
}
