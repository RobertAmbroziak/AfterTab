using AfterTab.Common.Errors;
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AfterTab.API.Errors;

public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = exception is AppException error ? error.Kind switch
        {
            ErrorKind.Validation => 400, ErrorKind.NotFound => 404,
            ErrorKind.Forbidden => 403, ErrorKind.Conflict => 409, _ => 500
        } : StatusCodes.Status500InternalServerError;
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        if (status >= 500)
        {
            // Use the route template, not URL values/query strings that may contain secrets.
            var endpoint = context.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? context.GetEndpoint();
            var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            // The framework middleware category is suppressed in configuration: log once here.
            logger.LogError(exception, "Unhandled error for {RequestMethod} {Route}; TraceId={TraceId}",
                context.Request.Method, route, traceId);
        }
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status >= 500 ? "Wystąpił nieoczekiwany błąd serwera." : exception.Message
        };
        problem.Extensions["traceId"] = traceId;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
