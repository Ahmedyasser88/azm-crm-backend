using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Communications.Commands.CreateConversation;
using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;
using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;
using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;
using AzmCrm.Application.Features.Communications.Commands.SendMessage;
using AzmCrm.Application.Features.Communications.Commands.StartLiveChat;
using AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;
using AzmCrm.Application.Features.Communications.DTOs;
using AzmCrm.Application.Features.Communications.Queries.GetConversationById;
using AzmCrm.Application.Features.Communications.Queries.GetConversationMessages;
using AzmCrm.Application.Features.Communications.Queries.GetConversationsList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Infrastructure.Communications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AzmCrm.API.Controllers;

public sealed class ConversationsController(
    IMediator mediator,
    IOptions<SmtpSettings> smtpSettings,
    IOptions<WhatsAppSettings> whatsAppSettings) : ApiControllerBase
{
    // Sms channel adds no per-request settings dependency to this controller — its inbound
    // webhook has no shared-secret/verify-token check (see Story 11's Edge Cases).
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var command = new CreateConversationCommand(request.CustomerId, request.Channel, request.Subject);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/conversations/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetConversationByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<ConversationListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null, [FromQuery] CommunicationChannel? channel = null,
        [FromQuery] ConversationStatus? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetConversationsListQuery(pageNumber, pageSize, customerId, channel, status), ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(Result<MessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SendMessageCommand(id, request.Body), ct);

        return ToCreatedResult(result, dto => $"/api/conversations/{id}/messages/{dto?.Id}");
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(Result<PaginatedResult<MessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetConversationMessagesQuery(id, pageNumber, pageSize), ct);
        return ToResult(result);
    }

    [HttpPost("web-form")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitWebForm([FromBody] WebFormSubmissionRequest request, CancellationToken ct)
    {
        var command = new SubmitWebFormCommand(request.Name, request.Email, request.Phone, request.Subject, request.Body);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/conversations/{id}");
    }

    [HttpPost("email/inbound")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveInboundEmail(
        [FromHeader(Name = "X-Webhook-Secret")] string? webhookSecret,
        [FromBody] EmailInboundWebhookRequest request,
        CancellationToken ct)
    {
        if (webhookSecret != smtpSettings.Value.InboundWebhookSecret)
            return Unauthorized();

        var command = new ReceiveInboundEmailCommand(
            request.FromEmail, request.FromName, request.Subject, request.Body, request.ExternalMessageId);

        await mediator.Send(command, ct);

        return Accepted();
    }

    [HttpGet("whatsapp/inbound")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult VerifyWhatsAppWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode != "subscribe" || verifyToken != whatsAppSettings.Value.WebhookVerifyToken)
            return Forbid();

        return Content(challenge ?? "", "text/plain");
    }

    [HttpPost("whatsapp/inbound")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveInboundWhatsAppMessage(
        [FromBody] WhatsAppInboundWebhookRequest request, CancellationToken ct)
    {
        var command = new ReceiveInboundWhatsAppMessageCommand(
            request.FromPhoneNumber, request.Body, request.ExternalMessageId);

        await mediator.Send(command, ct);

        return Accepted();
    }

    [HttpPost("sms/inbound")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveInboundSms([FromForm] SmsInboundWebhookRequest request, CancellationToken ct)
    {
        var command = new ReceiveInboundSmsCommand(request.From, request.Body, request.MessageSid);

        await mediator.Send(command, ct);

        return Accepted();
    }

    [HttpPost("live-chat/start")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartLiveChat([FromBody] StartLiveChatRequest request, CancellationToken ct)
    {
        var command = new StartLiveChatCommand(request.Name, request.Email, request.Body);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/conversations/{id}");
    }
}
