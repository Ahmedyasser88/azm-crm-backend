using AzmCrm.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers.Base;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToResult(Result result) =>
        result.IsSuccess ? Ok(result) : BadRequest(result);

    protected IActionResult ToResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result) : BadRequest(result);

    protected IActionResult ToCreatedResult<T>(Result<T> result, Func<T?, string> locationFactory) =>
        result.IsSuccess ? Created(locationFactory(result.Data), result) : BadRequest(result);

    protected IActionResult ToNotFoundResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result) : NotFound(result);

    protected IActionResult ToNoContentResult(Result result) =>
        result.IsSuccess ? NoContent() : BadRequest(result);

    protected string GetClientIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
}
