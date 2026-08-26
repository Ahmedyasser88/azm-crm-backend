using AzmCrm.Application.Shared.Exceptions;
using FluentValidation;

namespace AzmCrm.API.Middleware;

internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "An unhandled exception occurred");

        var (statusCode, title, errors) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                validationEx.Errors.Select(e => e.ErrorMessage).ToList()
            ),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                new List<string> { exception.Message }
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                new List<string> { "An unexpected error occurred" }
            )
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc9110",
            title,
            status = statusCode,
            errors
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
