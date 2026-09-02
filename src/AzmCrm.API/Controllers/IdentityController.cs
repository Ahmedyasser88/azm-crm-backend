using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Identity.Commands.Login;
using AzmCrm.Application.Features.Identity.Commands.RefreshToken;
using AzmCrm.Application.Features.Identity.Commands.Register;
using AzmCrm.Application.Features.Identity.Commands.RevokeToken;
using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Features.Identity.Queries.GetCurrentUser;
using AzmCrm.Application.Features.Identity.Queries.SearchAgents;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AzmCrm.API.Controllers;

public sealed class IdentityController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Username,
            request.Email,
            request.MobileNumber,
            request.Password,
            request.ConfirmPassword
        );

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/identity/{id}");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(Result<AuthenticationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(
            request.UsernameOrEmail,
            request.Password,
            GetClientIpAddress()
        );

        var result = await mediator.Send(command, ct);

        return ToResult(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(Result<AuthenticationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(
            request.RefreshToken,
            GetClientIpAddress()
        );

        var result = await mediator.Send(command, ct);

        return ToResult(result);
    }

    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RevokeTokenCommand(
            request.RefreshToken,
            GetClientIpAddress()
        );

        var result = await mediator.Send(command, ct);

        return ToNoContentResult(result);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Result<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var query = new GetCurrentUserQuery();
        var result = await mediator.Send(query, ct);

        return ToResult(result);
    }

    [HttpGet("agents")]
    [Authorize]
    [ProducesResponseType(typeof(Result<List<AgentSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAgents(
        [FromQuery] string? search, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new SearchAgentsQuery(search, pageSize), ct);

        return ToResult(result);
    }
}
