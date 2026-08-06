using Microsoft.AspNetCore.WebUtilities;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DraftDatastore.API.Middleware;

public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (Exception exception) when (exception is ValidationException or UnauthorizedAccessException or InvalidOperationException)
        {
            var status = exception switch { ValidationException => StatusCodes.Status400BadRequest, UnauthorizedAccessException => StatusCodes.Status401Unauthorized, _ => StatusCodes.Status409Conflict };
            logger.LogWarning(exception, "Request failed with a handled exception. TraceId: {TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(context, status, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error. TraceId: {TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status), Detail = detail, Instance = context.Request.Path });
    }
}

